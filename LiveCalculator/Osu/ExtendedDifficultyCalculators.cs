using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch.Difficulty;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Taiko.Difficulty;

namespace LiveCalculator.Osu;

public interface IExtendedDifficultyCalculator
{
    Skill[] GetSkills();
    DifficultyHitObject[] GetDifficultyHitObjects();
}

public class ExtendedOsuDifficultyCalculator : OsuDifficultyCalculator, IExtendedDifficultyCalculator
{
    private Skill[] _skills = [];
    private DifficultyHitObject[] _difficultyHitObjects = [];

    public ExtendedOsuDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap) : base(ruleset, beatmap) { }
    public Skill[] GetSkills() => _skills;
    public DifficultyHitObject[] GetDifficultyHitObjects() => _difficultyHitObjects;

    protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, Mod[] mods)
        => _difficultyHitObjects = base.CreateDifficultyHitObjects(beatmap, mods).ToArray();

    protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods)
        => _skills = base.CreateSkills(beatmap, mods);
}

public class ExtendedTaikoDifficultyCalculator : TaikoDifficultyCalculator, IExtendedDifficultyCalculator
{
    private Skill[] _skills = [];
    private DifficultyHitObject[] _difficultyHitObjects = [];

    public ExtendedTaikoDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap) : base(ruleset, beatmap) { }
    public Skill[] GetSkills() => _skills;
    public DifficultyHitObject[] GetDifficultyHitObjects() => _difficultyHitObjects;

    protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, Mod[] mods)
        => _difficultyHitObjects = base.CreateDifficultyHitObjects(beatmap, mods).ToArray();

    protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods)
        => _skills = base.CreateSkills(beatmap, mods);
}

public class ExtendedCatchDifficultyCalculator : CatchDifficultyCalculator, IExtendedDifficultyCalculator
{
    private Skill[] _skills = [];
    private DifficultyHitObject[] _difficultyHitObjects = [];

    public ExtendedCatchDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap) : base(ruleset, beatmap) { }
    public Skill[] GetSkills() => _skills;
    public DifficultyHitObject[] GetDifficultyHitObjects() => _difficultyHitObjects;

    protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, Mod[] mods)
        => _difficultyHitObjects = base.CreateDifficultyHitObjects(beatmap, mods).ToArray();

    protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods)
        => _skills = base.CreateSkills(beatmap, mods);
}

public class ExtendedManiaDifficultyCalculator : ManiaDifficultyCalculator, IExtendedDifficultyCalculator
{
    private Skill[] _skills = [];
    private DifficultyHitObject[] _difficultyHitObjects = [];

    public ExtendedManiaDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap) : base(ruleset, beatmap) { }
    public Skill[] GetSkills() => _skills;
    public DifficultyHitObject[] GetDifficultyHitObjects() => _difficultyHitObjects;

    protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, Mod[] mods)
        => _difficultyHitObjects = base.CreateDifficultyHitObjects(beatmap, mods).ToArray();

    protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods)
        => _skills = base.CreateSkills(beatmap, mods);
}

public static class ExtendedCalculatorFactory
{
    public static DifficultyCalculator Create(Ruleset ruleset, IWorkingBeatmap working) => ruleset.RulesetInfo.OnlineID switch
    {
        0 => new ExtendedOsuDifficultyCalculator(ruleset.RulesetInfo, working),
        1 => new ExtendedTaikoDifficultyCalculator(ruleset.RulesetInfo, working),
        2 => new ExtendedCatchDifficultyCalculator(ruleset.RulesetInfo, working),
        3 => new ExtendedManiaDifficultyCalculator(ruleset.RulesetInfo, working),
        _ => ruleset.CreateDifficultyCalculator(working)
    };
}
