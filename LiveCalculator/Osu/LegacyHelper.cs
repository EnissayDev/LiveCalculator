// Portions adapted from ppy/osu-tools (MIT Licence) — PerformanceCalculator/LegacyHelper.cs.

using System;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Taiko;

namespace LiveCalculator.Osu;

public static class LegacyHelper
{
    /// <summary>
    /// Maps a legacy ruleset id (0 = osu, 1 = taiko, 2 = catch, 3 = mania) to a ruleset instance.
    /// </summary>
    public static Ruleset GetRulesetFromLegacyId(int id) => id switch
    {
        0 => new OsuRuleset(),
        1 => new TaikoRuleset(),
        2 => new CatchRuleset(),
        3 => new ManiaRuleset(),
        _ => throw new ArgumentException($"Invalid ruleset ID: {id}", nameof(id))
    };
}
