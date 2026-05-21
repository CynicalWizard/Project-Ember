namespace Content.Shared.Ember.Doors;

public static class EmberAirlockPaintStyle
{
    private static readonly Dictionary<string, string> StyleIds = new()
    {
        ["atmospherics"] = "EmberAirlockAtmospherics",
        ["basic"] = "EmberAirlockBasic",
        ["cargo"] = "EmberAirlockLogistics",
        ["command"] = "EmberAirlockCommand",
        ["engineering"] = "EmberAirlockEngineering",
        ["external"] = "EmberAirlockExternal",
        ["freezer"] = "EmberAirlockFreezer",
        ["justice"] = "EmberAirlockJustice",
        ["maintenance"] = "EmberAirlockMaintenance",
        ["medical"] = "EmberAirlockMedical",
        ["mining"] = "EmberAirlockMining",
        ["roboticist"] = "EmberAirlockRoboticist",
        ["science"] = "EmberAirlockEpistemics",
        ["security"] = "EmberAirlockSecurity",
        ["shuttle"] = "EmberAirlockBasic",
        ["syndicate"] = "EmberAirlockSyndicate",
        ["virology"] = "EmberAirlockVirology",
    };

    private static readonly Dictionary<string, string> PreviewPrototypes = new()
    {
        ["atmospherics"] = "AirlockAtmospherics",
        ["basic"] = "Airlock",
        ["cargo"] = "AirlockCargo",
        ["command"] = "AirlockCommand",
        ["engineering"] = "AirlockEngineering",
        ["external"] = "AirlockExternal",
        ["freezer"] = "AirlockFreezer",
        ["justice"] = "AirlockJustice",
        ["maintenance"] = "AirlockMaint",
        ["medical"] = "AirlockMedical",
        ["mining"] = "AirlockMining",
        ["roboticist"] = "AirlockRoboticist",
        ["science"] = "AirlockScience",
        ["security"] = "AirlockSecurity",
        ["shuttle"] = "AirlockShuttle",
        ["syndicate"] = "AirlockSyndicate",
        ["virology"] = "AirlockVirology",
    };

    public static bool TryGetStyle(string style, out string emberStyle)
    {
        return StyleIds.TryGetValue(Normalize(style), out emberStyle!);
    }

    public static bool TryGetPreviewPrototype(string style, out string prototype)
    {
        return PreviewPrototypes.TryGetValue(Normalize(style), out prototype!);
    }

    private static string Normalize(string style)
    {
        return style.ToLowerInvariant();
    }
}
