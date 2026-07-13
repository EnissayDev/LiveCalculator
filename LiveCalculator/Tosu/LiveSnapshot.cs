using System;
using System.Collections.Generic;
using System.Linq;

namespace LiveCalculator.Tosu;

/// <summary>
/// A normalised, calculator-friendly view of one tosu update.
/// </summary>
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

    /// <summary>Total judged objects so far (used to index timed difficulty attributes).</summary>
    public int JudgedObjects => Count300 + CountGeki + Count100 + CountKatu + Count50 + CountMiss;

    public string ModsSignature => Mods.Count == 0 ? "" : string.Join(",", Mods.OrderBy(m => m, StringComparer.Ordinal));

    /// <summary>Identity of the currently-loaded map+mods; when this changes the heavy recalc must re-run.</summary>
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
            BeatmapFile = p.DirectPath?.BeatmapFile,
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
}
