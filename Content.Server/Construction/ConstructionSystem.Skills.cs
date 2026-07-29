using Content.Shared.Construction;
using System;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Construction.Steps;
using Content.Shared.Ember.Materials;
using Content.Shared.Ember.Skills;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;
using Content.Shared.Popups;
using Robust.Shared.Random;

namespace Content.Server.Construction
{
    public sealed partial class ConstructionSystem
    {
        [Dependency] private readonly SharedSkillsSystem _skillsSystem = default!;

        /// <summary>
        /// Retrieves the construction difficulty of the recipe based on the materials it requires.
        /// Defaults to 1 (Basic) if no materials with difficulty are found.
        /// </summary>
        private int GetConstructionDifficulty(ConstructionGraphEdge edge)
        {
            var matDiff = 0;
            foreach (var step in edge.Steps)
            {
                if (step is MaterialConstructionGraphStep matStep)
                {
                    if (PrototypeManager.TryIndex<StackPrototype>(matStep.MaterialPrototypeId, out var stackProto) &&
                        PrototypeManager.TryIndex<EntityPrototype>(stackProto.Spawn, out var entProto) &&
                        entProto.TryGetComponent<EmberMaterialStackComponent>(out var matStackComp, _factory) &&
                        PrototypeManager.TryIndex<EmberMaterialPrototype>(matStackComp.Material, out var emberMatProto))
                    {
                        matDiff = Math.Max(matDiff, emberMatProto.ConstructionDifficulty);
                    }
                }
            }
            return Math.Clamp(1 + matDiff, 0, 3);
        }

        private float GetConstructionSpeedModifier(EntityUid user, int difficulty)
        {
            var currentSkill = (int)_skillsSystem.GetSkillValue(user, "construction");
            // Bay formula: final_time = base_time * max(0, 1 + (3 - current_skill) * 0.3)
            return Math.Max(0f, 1f + (3 - currentSkill) * 0.3f);
        }

        private bool TryConstructionFail(EntityUid user, int difficulty)
        {
            var currentSkill = (int)_skillsSystem.GetSkillValue(user, "construction");

            if (currentSkill >= difficulty)
                return false;

            // 90 * (2 ^ (1 - current_skill)) %
            var chance = 90f * (float)Math.Pow(2, 1 - currentSkill);
            return _robustRandom.Prob(chance / 100f);
        }
    }
}
