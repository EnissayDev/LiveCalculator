using System;
using System.Collections.Generic;
using System.Linq;

namespace LiveCalculator.Tosu;

public class LiveSnapshot
{
    public string? BeatmapFile { get; init; }
    public int RulesetId { get; init; }
    public string Artist { get; init; } = "";
    public string Title { get; init; } = "";
    public string Version { get; init; } = "";
    public string Mapper { get; init; } = "";

    public string StateName { get; init; } = "";
    public bool IsPlaying { get; init; }

    public double Accuracy { get; init; }
    public int CurrentCombo { get; init; }
    public int MaxCombo { get; init; }

    public int Count300 { get; init; }
    public int CountGeki { get; init; }
    public int Count100 { get; init; }
    public int CountKatu { get; init; }
    public int Count50 { get; init; }
    public int CountMiss { get; init; }

    public IReadOnlyList<string> Mods { get; init; } = System.Array.Empty<string>();

    public int JudgedObjects => Count300 + CountGeki + Count100 + CountKatu + Count50 + CountMiss;

    public string ModsSignature => Mods.Count == 0 ? "" : string.Join(",", Mods.OrderBy(m => m, StringComparer.Ordinal));

    public string MapKey => $"{BeatmapFile}|{RulesetId}|{ModsSignature}";

    public static LiveSnapshot FromPayload(TosuPayload p)
    {
        var beatmap = p.Beatmap;
        var play = p.Play;
        var hits = play?.Hits;

        var mods = play?.Mods?.Array?
            .Select(m => m.Acronym)
            .Where(a => !string.IsNullOrEmpty(a))
            .Select(a => a!)
            .ToList() ?? new List<string>();

        string state = p.State?.Name ?? "";

        return new LiveSnapshot
        {
            BeatmapFile = resolveBeatmapFile(p),
            RulesetId = beatmap?.Mode?.Number ?? 0,
            Artist = beatmap?.Artist ?? "",
            Title = beatmap?.Title ?? "",
            Version = beatmap?.Version ?? "",
            Mapper = beatmap?.Mapper ?? "",
            StateName = state,
            IsPlaying = string.Equals(state, "play", StringComparison.OrdinalIgnoreCase),
            Accuracy = play?.Accuracy ?? 0,
            CurrentCombo = play?.Combo?.Current ?? 0,
            MaxCombo = play?.Combo?.Max ?? 0,
            Count300 = hits?.Count300 ?? 0,
            CountGeki = hits?.CountGeki ?? 0,
            Count100 = hits?.Count100 ?? 0,
            CountKatu = hits?.CountKatu ?? 0,
            Count50 = hits?.Count50 ?? 0,
            CountMiss = hits?.CountMiss ?? 0,
            Mods = mods
        };
    }

    private static string? resolveBeatmapFile(TosuPayload p)
    {
        // tosu reports directPath.beatmapFile relative to the Songs directory (folders.songs),
        // so relative candidates must be rooted against it.
        string? songs = p.Folders?.Songs;

        string? makeAbsolute(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            if (System.IO.Path.IsPathRooted(path))
                return path;
            return string.IsNullOrEmpty(songs) ? path : System.IO.Path.Combine(songs, path);
        }

        string? best = null;

        string? direct = makeAbsolute(p.DirectPath?.BeatmapFile);
        if (direct != null)
        {
            if (System.IO.File.Exists(direct))
                return direct;
            best = direct;
        }

        if (!string.IsNullOrEmpty(p.Folders?.Beatmap) && !string.IsNullOrEmpty(p.Files?.Beatmap))
        {
            string? combined = makeAbsolute(System.IO.Path.Combine(p.Folders.Beatmap, p.Files.Beatmap));
            if (combined != null)
            {
                if (System.IO.File.Exists(combined))
                    return combined;
                best ??= combined;
            }
        }

        return best;
    }
}
