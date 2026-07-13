using System.IO;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Skinning;

namespace LiveCalculator.Osu;

public class ProcessorWorkingBeatmap : WorkingBeatmap
{
    private readonly Beatmap _beatmap;

    public ProcessorWorkingBeatmap(string file)
        : this(ReadFromFile(file))
    {
    }

    private ProcessorWorkingBeatmap(Beatmap beatmap)
        : base(beatmap.BeatmapInfo, null)
    {
        _beatmap = beatmap;
        beatmap.BeatmapInfo.Ruleset = LegacyHelper.GetRulesetFromLegacyId(beatmap.BeatmapInfo.Ruleset.OnlineID).RulesetInfo;
    }

    private static Beatmap ReadFromFile(string filename)
    {
        using var stream = File.OpenRead(filename);
        using var reader = new LineBufferedReader(stream);
        return Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
    }

    protected override IBeatmap GetBeatmap() => _beatmap;
    public override Texture GetBackground() => throw new System.NotImplementedException();
    protected override Track GetBeatmapTrack() => throw new System.NotImplementedException();
    protected override ISkin GetSkin() => throw new System.NotImplementedException();
    public override Stream GetStream(string storagePath) => throw new System.NotImplementedException();
}
