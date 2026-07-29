using System.Linq;

namespace Content.Shared.Ember.Materials;

public readonly record struct EmberMaterialProcessResult(
    string? Material,
    int Sheets,
    int ConsumedUnits,
    bool ProducedWaste);

public readonly record struct EmberMaterialAlloyResult(
    string? Material,
    int Sheets,
    IReadOnlyDictionary<string, int> ConsumedMaterials);

public readonly record struct EmberMaterialProcessorOutput(
    string? Material,
    int Sheets,
    bool ProducedWaste);

public readonly record struct EmberMaterialProcessorTickResult(
    IReadOnlyList<EmberMaterialProcessorOutput> Outputs,
    int ProcessedSheets);

public static class EmberMaterialProcessing
{
    public static bool TrySmelt(
        EmberMaterialPrototype ore,
        EmberMaterialPrototype target,
        int storedUnits,
        int maxSheets,
        out EmberMaterialProcessResult output)
    {
        return TryProcessOre(ore.OreSmeltsTo, ore, target, storedUnits, maxSheets, false, out output);
    }

    public static bool TryCompress(
        EmberMaterialPrototype ore,
        EmberMaterialPrototype target,
        int storedUnits,
        int maxSheets,
        out EmberMaterialProcessResult output)
    {
        return TryProcessOre(ore.OreCompressesTo, ore, target, storedUnits, maxSheets, true, out output);
    }

    public static bool CanAlloy(IReadOnlyDictionary<string, int> storedMaterials, EmberMaterialPrototype product)
    {
        if (product.AlloyMaterials.Count == 0)
            return false;

        foreach (var (material, amount) in product.AlloyMaterials)
        {
            if (!storedMaterials.TryGetValue(material, out var stored) || stored < amount)
                return false;
        }

        return true;
    }

    public static bool TryAlloy(
        IReadOnlyDictionary<string, int> storedMaterials,
        IReadOnlySet<string> alloyingMaterials,
        EmberMaterialPrototype product,
        int maxSheets,
        out EmberMaterialAlloyResult output)
    {
        output = default;

        if (maxSheets <= 0 || product.AlloyMaterials.Count == 0)
            return false;

        int? making = null;
        foreach (var (material, requiredUnits) in product.AlloyMaterials)
        {
            if (requiredUnits <= 0 ||
                !alloyingMaterials.Contains(material) ||
                !storedMaterials.TryGetValue(material, out var storedUnits) ||
                storedUnits < requiredUnits)
            {
                return false;
            }

            var possible = storedUnits / requiredUnits;
            making = making == null
                ? possible
                : Math.Min(making.Value, possible);
        }

        var sheets = Math.Min(maxSheets, making ?? 0);
        if (sheets <= 0)
            return false;

        var consumed = new Dictionary<string, int>();
        foreach (var (material, requiredUnits) in product.AlloyMaterials)
        {
            consumed[material] = requiredUnits * sheets;
        }

        output = new EmberMaterialAlloyResult(product.ID, sheets, consumed);
        return true;
    }

    public static EmberMaterialProcessorTickResult ProcessTick(
        IEnumerable<EmberMaterialPrototype> materials,
        IDictionary<string, int> storedMaterials,
        IReadOnlyDictionary<string, EmberMaterialProcessingMode> modes,
        int sheetsPerTick)
    {
        var materialList = materials.ToList();
        var byId = new Dictionary<string, EmberMaterialPrototype>();
        var alloyComponents = new HashSet<string>();

        foreach (var material in materialList)
        {
            byId[material.ID] = material;

            foreach (var alloyMaterial in material.AlloyMaterials.Keys)
            {
                alloyComponents.Add(alloyMaterial);
            }
        }

        var outputs = new List<EmberMaterialProcessorOutput>();
        var alloyingMaterials = new HashSet<string>();
        var processedSheets = 0;

        foreach (var material in materialList)
        {
            if (processedSheets >= sheetsPerTick)
                break;

            if (!storedMaterials.TryGetValue(material.ID, out var stored) || stored <= 0)
                continue;

            if (!modes.TryGetValue(material.ID, out var mode) ||
                mode == EmberMaterialProcessingMode.Disabled)
            {
                continue;
            }

            var remainingSheets = sheetsPerTick - processedSheets;

            switch (mode)
            {
                case EmberMaterialProcessingMode.Smelt:
                    ProcessOre(material, material.OreSmeltsTo, byId, storedMaterials, remainingSheets, false, outputs, ref processedSheets);
                    break;
                case EmberMaterialProcessingMode.Compress:
                    ProcessOre(material, material.OreCompressesTo, byId, storedMaterials, remainingSheets, true, outputs, ref processedSheets);
                    break;
                case EmberMaterialProcessingMode.Alloy:
                    if (alloyComponents.Contains(material.ID))
                        alloyingMaterials.Add(material.ID);
                    else
                        ProcessWaste(material, storedMaterials, remainingSheets, false, outputs, ref processedSheets);
                    break;
            }
        }

        if (processedSheets < sheetsPerTick)
        {
            foreach (var material in materialList)
            {
                var remainingSheets = sheetsPerTick - processedSheets;
                if (!material.AlloyProduct)
                    continue;

                var storedSnapshot = new Dictionary<string, int>(storedMaterials);
                if (!TryAlloy(storedSnapshot, alloyingMaterials, material, remainingSheets, out var alloy))
                {
                    continue;
                }

                foreach (var (id, amount) in alloy.ConsumedMaterials)
                {
                    storedMaterials[id] -= amount;
                }

                outputs.Add(new EmberMaterialProcessorOutput(alloy.Material, alloy.Sheets, false));
                processedSheets += alloy.Sheets;
                break;
            }
        }

        return new EmberMaterialProcessorTickResult(outputs, processedSheets);
    }

    private static bool TryProcessOre(
        string? targetId,
        EmberMaterialPrototype ore,
        EmberMaterialPrototype target,
        int storedUnits,
        int maxSheets,
        bool compress,
        out EmberMaterialProcessResult output)
    {
        output = default;

        if (targetId == null || targetId != target.ID)
            return false;

        if (storedUnits <= 0 || maxSheets <= 0)
            return false;

        var unitsPerSheet = Math.Max(1, ore.UnitsPerSheet);
        var storedSheets = Math.Min(storedUnits / unitsPerSheet, maxSheets);
        if (storedSheets <= 0)
            return false;

        if (compress && storedSheets < 2)
            return false;

        var sheets = compress ? storedSheets / 2 : storedSheets;
        output = new EmberMaterialProcessResult(target.ID, sheets, storedSheets * unitsPerSheet, false);
        return true;
    }

    private static void ProcessOre(
        EmberMaterialPrototype ore,
        string? targetId,
        IReadOnlyDictionary<string, EmberMaterialPrototype> byId,
        IDictionary<string, int> storedMaterials,
        int maxSheets,
        bool compress,
        List<EmberMaterialProcessorOutput> outputs,
        ref int processedSheets)
    {
        if (targetId != null &&
            byId.TryGetValue(targetId, out var target) &&
            TryProcessOre(targetId, ore, target, storedMaterials[ore.ID], maxSheets, compress, out var output))
        {
            storedMaterials[ore.ID] -= output.ConsumedUnits;
            outputs.Add(new EmberMaterialProcessorOutput(output.Material, output.Sheets, false));
            processedSheets += output.Sheets;
            return;
        }

        ProcessWaste(ore, storedMaterials, maxSheets, compress, outputs, ref processedSheets);
    }

    private static void ProcessWaste(
        EmberMaterialPrototype ore,
        IDictionary<string, int> storedMaterials,
        int maxSheets,
        bool compress,
        List<EmberMaterialProcessorOutput> outputs,
        ref int processedSheets)
    {
        if (!storedMaterials.TryGetValue(ore.ID, out var storedUnits) || storedUnits <= 0 || maxSheets <= 0)
            return;

        var unitsPerSheet = Math.Max(1, ore.UnitsPerSheet);
        var storedSheets = Math.Min(storedUnits / unitsPerSheet, maxSheets);
        if (storedSheets <= 0)
            return;

        if (compress && storedSheets < 2)
            return;

        var producedSheets = compress
            ? storedSheets / 2
            : storedSheets;

        if (producedSheets <= 0)
            return;

        storedMaterials[ore.ID] -= storedSheets * unitsPerSheet;
        outputs.Add(new EmberMaterialProcessorOutput(null, producedSheets, true));
        processedSheets += producedSheets;
    }
}
