using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Examine;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared.Ember.Medical.Surgery.Tools;

/// <summary>
///     Examining a surgical or ghetto tool shows everything it can be used for.
/// </summary>
public sealed class EmberSurgeryToolExamineSystem : EntitySystem
{
    [Dependency] private readonly ExamineSystemShared _examine = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<EmberSurgeryToolComponent, GetVerbsEvent<ExamineVerb>>(OnGetVerbs);

        SubscribeLocalEvent<EmberBoneGelComponent, EmberSurgeryToolExaminedEvent>(OnExamined);
        SubscribeLocalEvent<EmberBoneSawComponent, EmberSurgeryToolExaminedEvent>(OnExamined);
        SubscribeLocalEvent<EmberCauteryComponent, EmberSurgeryToolExaminedEvent>(OnExamined);
        SubscribeLocalEvent<EmberHemostatComponent, EmberSurgeryToolExaminedEvent>(OnExamined);
        SubscribeLocalEvent<EmberRetractorComponent, EmberSurgeryToolExaminedEvent>(OnExamined);
        SubscribeLocalEvent<EmberScalpelComponent, EmberSurgeryToolExaminedEvent>(OnExamined);
        SubscribeLocalEvent<EmberDrillComponent, EmberSurgeryToolExaminedEvent>(OnExamined);
        SubscribeLocalEvent<EmberTendingComponent, EmberSurgeryToolExaminedEvent>(OnExamined);
        SubscribeLocalEvent<EmberTweezersComponent, EmberSurgeryToolExaminedEvent>(OnExamined);
        SubscribeLocalEvent<EmberBoneSetterComponent, EmberSurgeryToolExaminedEvent>(OnExamined);
        SubscribeLocalEvent<BodyPartComponent, EmberSurgeryToolExaminedEvent>(OnExamined);
        SubscribeLocalEvent<OrganComponent, EmberSurgeryToolExaminedEvent>(OnExamined);
    }

    private void OnGetVerbs(Entity<EmberSurgeryToolComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var msg = FormattedMessage.FromMarkupOrThrow(Loc.GetString("surgery-tool-header"));
        msg.PushNewline();
        var ev = new EmberSurgeryToolExaminedEvent(msg);
        RaiseLocalEvent(ent, ref ev);

        _examine.AddDetailedExamineVerb(args, ent.Comp, ev.Message,
            Loc.GetString("surgery-tool-examinable-verb-text"), "/Textures/Objects/Specific/Medical/Surgery/scalpel.rsi/scalpel.png",
            Loc.GetString("surgery-tool-examinable-verb-message"));
    }

    private void OnExamined(EntityUid uid, IEmberSurgeryToolComponent comp, ref EmberSurgeryToolExaminedEvent args)
    {
        var msg = args.Message;
        var color = comp.Speed switch
        {
            < 1f => "red",
            > 1f => "green",
            _ => "white"
        };
        var key = "surgery-tool-" + (comp.Used == true ? "used" : "unlimited");
        var speed = comp.Speed.ToString("N2"); // 2 decimal places to not get trolled by float
        msg.PushMarkup(Loc.GetString(key, ("tool", comp.ToolName), ("speed", speed), ("color", color)));
    }
}

[ByRefEvent]
public record struct EmberSurgeryToolExaminedEvent(FormattedMessage Message);
