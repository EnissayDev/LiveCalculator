using System;
using System.Collections.Generic;
using System.Linq;
using LiveCalculator.Tosu;
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace LiveCalculator.Osu;

public record SkillSeries(string Name, IReadOnlyList<double> Difficulties, double Value);

public record LiveResult(double CurrentStars, double MaxStars, double Pp, int MaxCombo, IReadOnlyList<SkillSeries> Skills);

public class LiveDifficultyCalculator
{
    private PreparedMap? prepared;

    public string? PreparedKey => prepared?.Key;

    public string Status { get; private set; } = "Waiting for a beatmap…";

    public void Prepare(LiveSnapshot snapshot)
    {
        prepared = null;

        if (string.IsNullOrEmpty(snapshot.BeatmapFile))
        {
            Status = "tosu did not report a beatmap file path (directPath.beatmapFile).";
            return;
        }

        if (!System.IO.File.Exists(snapshot.BeatmapFile))
        {
            Status = $"Beatmap file not found on disk: {snapshot.BeatmapFile}";
            return;
        }

        try
        {
            var ruleset = LegacyHelper.GetRulesetFromLegacyId(snapshot.RulesetId);
            var working = new ProcessorWorkingBeatmap(snapshot.BeatmapFile);
            var mods = parseMods(ruleset, snapshot.Mods);

            var playable = working.GetPlayableBeatmap(ruleset.RulesetInfo, mods);

            var difficultyCalculator = ExtendedCalculatorFactory.Create(ruleset, working);

            var full = difficultyCalculator.Calculate(mods);
            var timed = difficultyCalculator.CalculateTimed(mods);

            prepared = new PreparedMap
            {
                Key = snapshot.MapKey,
                Ruleset = ruleset,
                Mods = mods,
                Playable = playable,
                FullAttributes = full,
                TimedAttributes = timed,
                PerformanceCalculator = ruleset.CreatePerformanceCalculator(),
                MaxCombo = playable.GetMaxCombo(),
                Skills = extractSkills(difficultyCalculator)
            };

            Status = "";
        }
        catch (Exception ex)
        {
            prepared = null;
            Status = $"Calc failed for {System.IO.Path.GetFileName(snapshot.BeatmapFile)}: {ex.GetType().Name}: {ex.Message}";
        }
    }

    public LiveResult? CalculateLive(LiveSnapshot snapshot)
    {
        var map = prepared;
        if (map == null || map.Key != snapshot.MapKey)
            return null;

        try
        {
            double maxStars = map.FullAttributes.StarRating;

            int judged = snapshot.JudgedObjects;
            DifficultyAttributes attributes = map.FullAttributes;
            double currentStars = maxStars;

            if (map.TimedAttributes.Count > 0)
            {
                if (judged <= 0)
                {
                    attributes = map.TimedAttributes[0].Attributes;
                    currentStars = snapshot.IsPlaying ? attributes.StarRating : maxStars;
                }
                else
                {
                    int index = Math.Min(judged - 1, map.TimedAttributes.Count - 1);
                    attributes = map.TimedAttributes[index].Attributes;
                    currentStars = attributes.StarRating;
                }
            }

            double pp = 0;

            if (judged > 0 && map.PerformanceCalculator != null)
            {
                var score = new ScoreInfo(map.Playable.BeatmapInfo, map.Ruleset.RulesetInfo)
                {
                    Accuracy = snapshot.Accuracy > 0 ? snapshot.Accuracy / 100.0 : 1.0,
                    MaxCombo = snapshot.MaxCombo,
                    Statistics = buildStatistics(snapshot),
                    Mods = map.Mods
                };

                pp = map.PerformanceCalculator.Calculate(score, attributes).Total;
            }

            return new LiveResult(currentStars, maxStars, pp, map.MaxCombo, map.Skills);
        }
        catch (Exception ex)
        {
            Status = $"Live calc error: {ex.GetType().Name}: {ex.Message}";
            return null;
        }
    }

    private static IReadOnlyList<SkillSeries> extractSkills(DifficultyCalculator calculator)
    {
        if (calculator is not IExtendedDifficultyCalculator extended)
            return Array.Empty<SkillSeries>();

        var series = new List<SkillSeries>();
        var seenNames = new HashSet<string>();

        foreach (var skill in extended.GetSkills())
        {
            string name = skill.GetType().Name;

            if (!seenNames.Add(name))
                continue;

            var difficulties = skill.GetObjectDifficulties().ToArray();
            series.Add(new SkillSeries(name, difficulties, skill.DifficultyValue()));
        }

        return series;
    }

    private static Dictionary<HitResult, int> buildStatistics(LiveSnapshot s) => s.RulesetId switch
    {
        // osu!
        0 => new Dictionary<HitResult, int>
        {
            [HitResult.Great] = s.Count300,
            [HitResult.Ok] = s.Count100,
            [HitResult.Meh] = s.Count50,
            [HitResult.Miss] = s.CountMiss
        },
        // osu!taiko
        1 => new Dictionary<HitResult, int>
        {
            [HitResult.Great] = s.Count300,
            [HitResult.Ok] = s.Count100,
            [HitResult.Miss] = s.CountMiss
        },
        // osu!catch
        2 => new Dictionary<HitResult, int>
        {
            [HitResult.Great] = s.Count300,
            [HitResult.LargeTickHit] = s.Count100,
            [HitResult.SmallTickHit] = s.Count50,
            [HitResult.Miss] = s.CountMiss
        },
        // osu!mania
        3 => new Dictionary<HitResult, int>
        {
            [HitResult.Perfect] = s.CountGeki,
            [HitResult.Great] = s.Count300,
            [HitResult.Good] = s.CountKatu,
            [HitResult.Ok] = s.Count100,
            [HitResult.Meh] = s.Count50,
            [HitResult.Miss] = s.CountMiss
        },
        _ => new Dictionary<HitResult, int>()
    };

    private static Mod[] parseMods(Ruleset ruleset, IReadOnlyList<string> acronyms)
    {
        var mods = new List<Mod>();

        foreach (string acronym in acronyms)
        {
            if (acronym is "CL" or "NF" or "SD" or "PF" or "AT" or "CM")
                continue;

            try
            {
                mods.Add(new APIMod { Acronym = acronym }.ToMod(ruleset));
            }
            catch
            {
            }
        }

        return mods.ToArray();
    }

    private class PreparedMap
    {
        public required string Key { get; init; }
        public required Ruleset Ruleset { get; init; }
        public required Mod[] Mods { get; init; }
        public required IBeatmap Playable { get; init; }
        public required DifficultyAttributes FullAttributes { get; init; }
        public required IReadOnlyList<TimedDifficultyAttributes> TimedAttributes { get; init; }
        public required PerformanceCalculator? PerformanceCalculator { get; init; }
        public required int MaxCombo { get; init; }
        public required IReadOnlyList<SkillSeries> Skills { get; init; }
    }
}
