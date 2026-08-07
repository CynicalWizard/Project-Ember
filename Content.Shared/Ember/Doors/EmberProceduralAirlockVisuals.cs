using Content.Shared.Doors.Components;
using Content.Shared.Roles;
using Robust.Shared.Maths;

namespace Content.Shared.Ember.Doors;

public readonly record struct EmberProceduralAirlockLayerVisuals(
    Color? DoorColor,
    EmberAirlockFill Fill,
    Color FillColor,
    Color? StripeColor,
    bool ShowStripeFill,
    Color? WindowColor);

public enum EmberAirlockFill : byte
{
    Steel,
    Color,
    Glass,
}

public static class EmberProceduralAirlockVisuals
{
    public static EmberProceduralAirlockLayerVisuals Resolve(
        EmberProceduralAirlockComponent airlock,
        EmberAirlockStylePrototype style,
        DepartmentPrototype? doorDepartment,
        DepartmentPrototype? stripeDepartment,
        DepartmentPrototype? windowDepartment = null)
    {
        var doorColor = airlock.DoorColor ?? style.DoorColor ?? doorDepartment?.Color;
        var stripeColor = airlock.StripeColor ?? style.StripeColor ?? stripeDepartment?.Color;
        var windowColor = airlock.WindowColor ?? style.WindowColor ?? windowDepartment?.Color;

        if (airlock.Glass)
        {
            var fillColor = windowColor ?? Color.White;
            return new EmberProceduralAirlockLayerVisuals(
                doorColor,
                EmberAirlockFill.Glass,
                fillColor,
                stripeColor,
                false,
                windowColor);
        }

        return new EmberProceduralAirlockLayerVisuals(
            doorColor,
            doorColor == null ? EmberAirlockFill.Steel : EmberAirlockFill.Color,
            doorColor ?? Color.White,
            stripeColor,
            stripeColor != null,
            null);
    }

    public static string SpriteStateFor(DoorState state)
    {
        return state switch
        {
            DoorState.Open => "open",
            DoorState.Opening => "opening",
            DoorState.Closing => "closing",
            DoorState.Denying => "deny",
            DoorState.Emagging => "deny",
            _ => "closed",
        };
    }

    public static bool IsTransitionState(DoorState state)
    {
        return state is DoorState.Opening or DoorState.Closing or DoorState.Denying or DoorState.Emagging;
    }

}
