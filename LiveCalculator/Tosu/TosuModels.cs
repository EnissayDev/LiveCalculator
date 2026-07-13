using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LiveCalculator.Tosu;

public class TosuPayload
{
    [JsonPropertyName("state")] public TosuState? State { get; set; }
    [JsonPropertyName("beatmap")] public TosuBeatmap? Beatmap { get; set; }
    [JsonPropertyName("play")] public TosuPlay? Play { get; set; }
    [JsonPropertyName("directPath")] public TosuDirectPath? DirectPath { get; set; }
    [JsonPropertyName("folders")] public TosuFolders? Folders { get; set; }
    [JsonPropertyName("files")] public TosuFiles? Files { get; set; }
}

public class TosuFolders
{
    [JsonPropertyName("songs")] public string? Songs { get; set; }
    [JsonPropertyName("beatmap")] public string? Beatmap { get; set; }
}

public class TosuFiles
{
    [JsonPropertyName("beatmap")] public string? Beatmap { get; set; }
}

public class TosuState
{
    [JsonPropertyName("number")] public int Number { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
}

public class TosuBeatmap
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("mode")] public TosuMode? Mode { get; set; }
    [JsonPropertyName("artist")] public string? Artist { get; set; }
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("mapper")] public string? Mapper { get; set; }
    [JsonPropertyName("version")] public string? Version { get; set; }
}

public class TosuMode
{
    [JsonPropertyName("number")] public int Number { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
}

public class TosuPlay
{
    [JsonPropertyName("accuracy")] public double Accuracy { get; set; }
    [JsonPropertyName("combo")] public TosuCombo? Combo { get; set; }
    [JsonPropertyName("hits")] public TosuHits? Hits { get; set; }
    [JsonPropertyName("mods")] public TosuMods? Mods { get; set; }
}

public class TosuCombo
{
    [JsonPropertyName("current")] public int Current { get; set; }
    [JsonPropertyName("max")] public int Max { get; set; }
}

public class TosuHits
{
    [JsonPropertyName("300")] public int Count300 { get; set; }
    [JsonPropertyName("geki")] public int CountGeki { get; set; }
    [JsonPropertyName("100")] public int Count100 { get; set; }
    [JsonPropertyName("katu")] public int CountKatu { get; set; }
    [JsonPropertyName("50")] public int Count50 { get; set; }
    [JsonPropertyName("0")] public int CountMiss { get; set; }
}

public class TosuMods
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("array")] public List<TosuMod>? Array { get; set; }
}

public class TosuMod
{
    [JsonPropertyName("acronym")] public string? Acronym { get; set; }
}

public class TosuDirectPath
{
    [JsonPropertyName("beatmapFile")] public string? BeatmapFile { get; set; }
}
