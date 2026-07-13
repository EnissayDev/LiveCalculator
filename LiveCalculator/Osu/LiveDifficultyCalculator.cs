using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

public record LiveResult(double Stars, double? CurrentStars, bool CurrentReady, double Pp, double MaxPp, int MaxCombo, IReadOnlyList<SkillSeries> Skills);

public class LiveDifficultyCalculator
{
    private PreparedMap? _prepared;

    private CancellationTokenSource? _timedCts;
    private volatile TimedData? _timedData;

    public string? PreparedKey => _prepared?.Key;

    public string Status { get; private set; } = "Waiting for a beatmap…";

    public void Prepare(LiveSnapshot snapshot)
    {
        _prepared = null;
        CancelTimed();

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
            var mods = ParseMods(ruleset, snapshot.Mods);

            var playable = working.GetPlayableBeatmap(ruleset.RulesetInfo, mods);

            var difficultyCalculator = ExtendedCalculatorFactory.Create(ruleset, working);

            var full = difficultyCalculator.Calculate(mods);

            var performanceCalculator = ruleset.CreatePerformanceCalculator();

            _prepared = new PreparedMap
            {
                Key = snapshot.MapKey,
                Ruleset = ruleset,
                Mods = mods,
                Playable = playable,
                FullAttributes = full,
                PerformanceCalculator = performanceCalculator,
                MaxCombo = playable.GetMaxCombo(),
                MaxPp = CalculateMaxPp(ruleset, playable, mods, full, performanceCalculator),
                Skills = ExtractSkills(difficultyCalculator)
            };

            Status = "";

            // Progressive per-position SR is expensive (CalculateTimed is ~O(n^2) on long maps),
            // so compute it off-thread. The main SR/PP is already available above.
            StartTimedComputation(snapshot);
        }
        catch (Exception ex)
        {
            _prepared = null;
            Status = $"Calc failed for {System.IO.Path.GetFileName(snapshot.BeatmapFile)}: {ex.GetType().Name}: {ex.Message}";
        }
    }

    private void CancelTimed()
    {
        _timedCts?.Cancel();
        _timedCts?.Dispose();
        _timedCts = null;
        _timedData = null;
    }

    private void StartTimedComputation(LiveSnapshot snapshot)
    {
        var cts = new CancellationTokenSource();
        _timedCts = cts;
        string key = snapshot.MapKey;
        string file = snapshot.BeatmapFile!;
        int rulesetId = snapshot.RulesetId;
        var modAcronyms = snapshot.Mods;

        Task.Run(() =>
        {
            try
            {
                // Fully isolated from the foreground calc: fresh beatmap, ruleset and mods.
                var ruleset = LegacyHelper.GetRulesetFromLegacyId(rulesetId);
                var working = new ProcessorWorkingBeatmap(file);
                var mods = ParseMods(ruleset, modAcronyms);

                var timed = ruleset.CreateDifficultyCalculator(working).CalculateTimed(mods, cts.Token);

                if (!cts.Token.IsCancellationRequested)
                    _timedData = new TimedData(key, timed);
            }
            catch
            {
                // Cancelled (map changed) or failed — leave progressive SR unavailable.
            }
        }, cts.Token);
    }

    public LiveResult? CalculateLive(LiveSnapshot snapshot)
    {
        var map = _prepared;
        if (map == null || map.Key != snapshot.MapKey)
            return null;

        try
        {
            double stars = map.FullAttributes.StarRating;
            int judged = snapshot.JudgedObjects;

            var timed = _timedData;
            bool currentReady = timed != null && timed.Key == map.Key;
            double? currentStars = null;

            if (currentReady && timed!.Timed.Count > 0)
            {
                // At the very start (judged 0) show the start-of-map difficulty, not the full-map SR.
                int index = Math.Clamp(judged - 1, 0, timed.Timed.Count - 1);
                currentStars = timed.Timed[index].Attributes.StarRating;
            }

            double pp = 0;

            if (judged > 0 && map.PerformanceCalculator != null)
            {
                var score = new ScoreInfo(map.Playable.BeatmapInfo, map.Ruleset.RulesetInfo)
                {
                    Accuracy = snapshot.Accuracy > 0 ? snapshot.Accuracy / 100.0 : 1.0,
                    MaxCombo = snapshot.MaxCombo,
                    Statistics = BuildStatistics(snapshot),
                    Mods = map.Mods
                };

                pp = map.PerformanceCalculator.Calculate(score, map.FullAttributes).Total;
            }

            return new LiveResult(stars, currentStars, currentReady, pp, map.MaxPp, map.MaxCombo, map.Skills);
        }
        catch (Exception ex)
        {
            Status = $"Live calc error: {ex.GetType().Name}: {ex.Message}";
            return null;
        }
    }

    // Max PP = the SS/perfect play on this map with the current mods. Constant per map, so we
    // compute it once in Prepare using the ruleset's own "maximum statistics" (a real perfect play).
    private static double CalculateMaxPp(Ruleset ruleset, IBeatmap playable, Mod[] mods, DifficultyAttributes attributes, PerformanceCalculator? performanceCalculator)
    {
        if (performanceCalculator == null)
            return 0;

        try
        {
            var scoreProcessor = ruleset.CreateScoreProcessor();
            scoreProcessor.ApplyBeatmap(playable);

            var perfectScore = new ScoreInfo(playable.BeatmapInfo, ruleset.RulesetInfo)
            {
                Accuracy = 1.0,
                MaxCombo = playable.GetMaxCombo(),
                Statistics = scoreProcessor.MaximumStatistics,
                Mods = mods
            };

            return performanceCalculator.Calculate(perfectScore, attributes).Total;
        }
        catch
        {
            return 0;
        }
    }

    private static IReadOnlyList<SkillSeries> ExtractSkills(DifficultyCalculator calculator)
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

    private static Dictionary<HitResult, int> BuildStatistics(LiveSnapshot s) => s.RulesetId switch
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

    private static Mod[] ParseMods(Ruleset ruleset, IReadOnlyList<string> acronyms)
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
        public required PerformanceCalculator? PerformanceCalculator { get; init; }
        public required int MaxCombo { get; init; }
        public required double MaxPp { get; init; }
        public required IReadOnlyList<SkillSeries> Skills { get; init; }
    }

    private class TimedData
    {
        public TimedData(string key, IReadOnlyList<TimedDifficultyAttributes> timed)
        {
            Key = key;
            Timed = timed;
        }

        public string Key { get; }
        public IReadOnlyList<TimedDifficultyAttributes> Timed { get; }
    }
}
