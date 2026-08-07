using Content.Shared.Construction;
using Content.Shared.Ember.Skills;
using Robust.Shared.Random;

namespace Content.Server.Construction
{
    public sealed partial class ConstructionSystem
    {
        [Dependency] private readonly SharedSkillsSystem _skillsSystem = default!;

        private int GetConstructionDifficulty(ConstructionGraphEdge edge)
        {
            return EmberConstructionSkill.GetDifficulty(edge, PrototypeManager, _factory);
        }

        /// <summary>
        /// Bay's <c>skill_delay_mult</c>: every level below Trained costs 30% more time, every level above saves
        /// the same. It applies to each step of a build as well as to raising the thing in the first place.
        /// </summary>
        private float GetConstructionSpeedModifier(EntityUid user)
        {
            return _skillsSystem.GetSkillDelayMultiplier(user, EmberConstructionSkill.Skill);
        }

        /// <summary>
        /// Bay's <c>skill_fail_prob(SKILL_CONSTRUCTION, 90, recipe.difficulty)</c>. The third argument is the level
        /// at which failure stops, which is why difficulty is measured on the skill scale.
        /// </summary>
        private bool TryConstructionFail(EntityUid user, int difficulty)
        {
            var chance = _skillsSystem.GetSkillFailChance(
                user,
                EmberConstructionSkill.Skill,
                EmberConstructionSkill.UnskilledFailChance,
                EmberConstructionSkill.GetRequiredLevel(difficulty));

            return _robustRandom.Prob(chance / 100f);
        }
    }
}
