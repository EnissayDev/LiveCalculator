// Portions adapted from ppy/osu-tools (MIT Licence) — PerformanceCalculator/ProcessorWorkingBeatmap.cs.

using System.IO;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Skinning;

namespace LiveCalculator.Osu;

/// <summary>
/// A <see cref="WorkingBeatmap"/> that reads from a .osu file on disk. Graphics/audio accessors are never
/// used by difficulty/performance calculation, so they intentionally throw.
/// </summary>
public class ProcessorWorkingBeatmap : WorkingBeatmap
{
    private readonly Beatmap beatmap;

    public ProcessorWorkingBeatmap(string file)
        : this(readFromFile(file))
    {
    }

    private ProcessorWorkingBeatmap(Beatmap beatmap)
        : base(beatmap.BeatmapInfo, null)
    {
        this.beatmap = beatmap;
        beatmap.BeatmapInfo.Ruleset = LegacyHelper.GetRulesetFromLegacyId(beatmap.BeatmapInfo.Ruleset.OnlineID).RulesetInfo;
    }

    private static Beatmap readFromFile(string filename)
    {
        using var stream = File.OpenRead(filename);
        using var reader = new LineBufferedReader(stream);
        return Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
    }

    protected override IBeatmap GetBeatmap() => beatmap;
    public override Texture GetBackground() => throw new System.NotImplementedException();
    protected override Track GetBeatmapTrack() => throw new System.NotImplementedException();
    protected override ISkin GetSkin() => throw new System.NotImplementedException();
    public override Stream GetStream(string storagePath) => throw new System.NotImplementedException();
}
