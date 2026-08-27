using Content.Shared.Kitchen.Components;
using Content.Shared.Ember.Medical.Surgery.Tools;

namespace Content.Server.Ember.Medical.Surgery;

/// <summary>
/// Makes all sharp things usable for incisions and sawing through bones, though worse than any other kind of ghetto analogue.
/// </summary>
public sealed partial class EmberGhettoSurgerySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SharpComponent, MapInitEvent>(OnSharpInit);
        SubscribeLocalEvent<SharpComponent, ComponentShutdown>(OnSharpShutdown);
    }

    private void OnSharpInit(Entity<SharpComponent> ent, ref MapInitEvent args)
    {
        if (EnsureComp<EmberScalpelComponent>(ent, out var scalpel))
        {
            ent.Comp.HadScalpel = true;
        }
        else
        {
            scalpel.Speed = 0.3f;
            Dirty(ent.Owner, scalpel);
        }

        if (EnsureComp<EmberBoneSawComponent>(ent, out var saw))
        {
            ent.Comp.HadBoneSaw = true;
        }
        else
        {
            saw.Speed = 0.2f;
            Dirty(ent.Owner, saw);
        }
    }

    private void OnSharpShutdown(Entity<SharpComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.HadScalpel)
            RemComp<EmberScalpelComponent>(ent);

        if (ent.Comp.HadBoneSaw)
            RemComp<EmberBoneSawComponent>(ent);
    }
}
