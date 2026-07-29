#pragma warning disable RA0039
using System.Collections.Generic;
using Content.Shared.Ember.Materials;
using NUnit.Framework;

namespace Content.Tests.Shared.Ember.Materials;

[TestFixture]
[TestOf(typeof(EmberMaterialProcessing))]
public sealed class EmberMaterialProcessingTest
{
    [Test]
    public void SmeltingUsesStoredUnitsAndIgnoresOreResultAmount()
    {
        var hematite = new EmberMaterialPrototype
        {
            ID = "Hematite",
            OreSmeltsTo = "Iron",
            OreResultAmount = 5,
            UnitsPerSheet = 2000,
        };

        var iron = new EmberMaterialPrototype
        {
            ID = "Iron",
        };

        Assert.That(EmberMaterialProcessing.TrySmelt(hematite, iron, 10000, 10, out var output), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(output.Material, Is.EqualTo("Iron"));
            Assert.That(output.Sheets, Is.EqualTo(5));
            Assert.That(output.ConsumedUnits, Is.EqualTo(10000));
            Assert.That(output.ProducedWaste, Is.False);
        });
    }

    [Test]
    public void SmeltingFailsWhenOreHasNoTarget()
    {
        var graphite = new EmberMaterialPrototype
        {
            ID = "Graphite",
        };

        var plastic = new EmberMaterialPrototype
        {
            ID = "Plastic",
        };

        Assert.That(EmberMaterialProcessing.TrySmelt(graphite, plastic, 2000, 10, out _), Is.False);
    }

    [Test]
    public void SmeltingRequiresAtLeastOneFullStoredSheet()
    {
        var hematite = new EmberMaterialPrototype
        {
            ID = "Hematite",
            OreSmeltsTo = "Iron",
            UnitsPerSheet = 2000,
        };

        var iron = new EmberMaterialPrototype
        {
            ID = "Iron",
        };

        Assert.That(EmberMaterialProcessing.TrySmelt(hematite, iron, 1999, 10, out var output), Is.False);
        Assert.That(output.Sheets, Is.EqualTo(0));
    }

    [Test]
    public void CompressionRequiresAtLeastTwoStoredSheetsAndOutputsHalf()
    {
        var phoronOre = new EmberMaterialPrototype
        {
            ID = "PhoronOre",
            OreCompressesTo = "Phoron",
            UnitsPerSheet = 2000,
        };

        var phoron = new EmberMaterialPrototype
        {
            ID = "Phoron",
        };

        Assert.That(EmberMaterialProcessing.TryCompress(phoronOre, phoron, 2000, 10, out var oneSheet), Is.False);
        Assert.That(oneSheet.Sheets, Is.EqualTo(0));

        Assert.That(EmberMaterialProcessing.TryCompress(phoronOre, phoron, 10000, 10, out var output), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(output.Material, Is.EqualTo("Phoron"));
            Assert.That(output.Sheets, Is.EqualTo(2));
            Assert.That(output.ConsumedUnits, Is.EqualTo(10000));
            Assert.That(output.ProducedWaste, Is.False);
        });
    }

    [Test]
    public void AlloyRequiresEveryComponent()
    {
        var steel = new EmberMaterialPrototype
        {
            ID = "Steel",
            AlloyMaterials =
            {
                ["Hematite"] = 1875,
                ["Graphite"] = 1875,
            },
        };

        var stored = new Dictionary<string, int>
        {
            ["Hematite"] = 1875,
            ["Graphite"] = 1874,
        };

        Assert.That(EmberMaterialProcessing.CanAlloy(stored, steel), Is.False);

        stored["Graphite"] = 1875;
        Assert.That(EmberMaterialProcessing.CanAlloy(stored, steel), Is.True);
    }

    [Test]
    public void AlloyUsesOnlyAlloyingInputsAndConsumesMinimumBatch()
    {
        var steel = new EmberMaterialPrototype
        {
            ID = "Steel",
            AlloyMaterials =
            {
                ["Hematite"] = 1875,
                ["Graphite"] = 1875,
            },
        };

        var stored = new Dictionary<string, int>
        {
            ["Hematite"] = 1875 * 4,
            ["Graphite"] = 1875 * 3,
        };

        Assert.That(EmberMaterialProcessing.TryAlloy(stored, new HashSet<string> { "Hematite" }, steel, 10, out _), Is.False);

        Assert.That(EmberMaterialProcessing.TryAlloy(
            stored,
            new HashSet<string> { "Hematite", "Graphite" },
            steel,
            2,
            out var output), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(output.Material, Is.EqualTo("Steel"));
            Assert.That(output.Sheets, Is.EqualTo(2));
            Assert.That(output.ConsumedMaterials["Hematite"], Is.EqualTo(1875 * 2));
            Assert.That(output.ConsumedMaterials["Graphite"], Is.EqualTo(1875 * 2));
        });
    }

    [Test]
    public void ProcessorTickSmeltsStoredOreUpToSheetLimit()
    {
        var materials = new[]
        {
            new EmberMaterialPrototype
            {
                ID = "Hematite",
                OreSmeltsTo = "Iron",
                UnitsPerSheet = 2000,
            },
            new EmberMaterialPrototype { ID = "Iron" },
        };

        var stored = new Dictionary<string, int>
        {
            ["Hematite"] = 12 * 2000,
        };

        var modes = new Dictionary<string, EmberMaterialProcessingMode>
        {
            ["Hematite"] = EmberMaterialProcessingMode.Smelt,
        };

        var result = EmberMaterialProcessing.ProcessTick(materials, stored, modes, 10);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outputs, Has.Count.EqualTo(1));
            Assert.That(result.Outputs[0].Material, Is.EqualTo("Iron"));
            Assert.That(result.Outputs[0].Sheets, Is.EqualTo(10));
            Assert.That(result.Outputs[0].ProducedWaste, Is.False);
            Assert.That(stored["Hematite"], Is.EqualTo(2 * 2000));
        });
    }

    [Test]
    public void ProcessorTickCompressesStoredOreIntoHalfSheets()
    {
        var materials = new[]
        {
            new EmberMaterialPrototype
            {
                ID = "PhoronOre",
                OreCompressesTo = "Phoron",
                UnitsPerSheet = 2000,
            },
            new EmberMaterialPrototype { ID = "Phoron" },
        };

        var stored = new Dictionary<string, int>
        {
            ["PhoronOre"] = 5 * 2000,
        };

        var modes = new Dictionary<string, EmberMaterialProcessingMode>
        {
            ["PhoronOre"] = EmberMaterialProcessingMode.Compress,
        };

        var result = EmberMaterialProcessing.ProcessTick(materials, stored, modes, 10);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outputs, Has.Count.EqualTo(1));
            Assert.That(result.Outputs[0].Material, Is.EqualTo("Phoron"));
            Assert.That(result.Outputs[0].Sheets, Is.EqualTo(2));
            Assert.That(result.Outputs[0].ProducedWaste, Is.False);
            Assert.That(stored["PhoronOre"], Is.EqualTo(0));
        });
    }

    [Test]
    public void ProcessorTickAlloysOnlyInputsMarkedForAlloy()
    {
        var materials = new[]
        {
            new EmberMaterialPrototype { ID = "Hematite", UnitsPerSheet = 2000 },
            new EmberMaterialPrototype { ID = "Graphite", UnitsPerSheet = 2000 },
            new EmberMaterialPrototype
            {
                ID = "Steel",
                AlloyProduct = true,
                AlloyMaterials =
                {
                    ["Hematite"] = 1875,
                    ["Graphite"] = 1875,
                },
            },
        };

        var stored = new Dictionary<string, int>
        {
            ["Hematite"] = 1875 * 4,
            ["Graphite"] = 1875 * 3,
        };

        var modes = new Dictionary<string, EmberMaterialProcessingMode>
        {
            ["Hematite"] = EmberMaterialProcessingMode.Alloy,
            ["Graphite"] = EmberMaterialProcessingMode.Alloy,
        };

        var result = EmberMaterialProcessing.ProcessTick(materials, stored, modes, 2);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outputs, Has.Count.EqualTo(1));
            Assert.That(result.Outputs[0].Material, Is.EqualTo("Steel"));
            Assert.That(result.Outputs[0].Sheets, Is.EqualTo(2));
            Assert.That(result.Outputs[0].ProducedWaste, Is.False);
            Assert.That(stored["Hematite"], Is.EqualTo(1875 * 2));
            Assert.That(stored["Graphite"], Is.EqualTo(1875));
        });
    }

    [Test]
    public void ProcessorTickTurnsInvalidAlloyInputIntoWaste()
    {
        var materials = new[]
        {
            new EmberMaterialPrototype
            {
                ID = "Bauxite",
                UnitsPerSheet = 2000,
            },
            new EmberMaterialPrototype
            {
                ID = "Steel",
                AlloyProduct = true,
                AlloyMaterials =
                {
                    ["Hematite"] = 1875,
                    ["Graphite"] = 1875,
                },
            },
        };

        var stored = new Dictionary<string, int>
        {
            ["Bauxite"] = 3 * 2000,
        };

        var modes = new Dictionary<string, EmberMaterialProcessingMode>
        {
            ["Bauxite"] = EmberMaterialProcessingMode.Alloy,
        };

        var result = EmberMaterialProcessing.ProcessTick(materials, stored, modes, 10);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outputs, Has.Count.EqualTo(1));
            Assert.That(result.Outputs[0].Material, Is.Null);
            Assert.That(result.Outputs[0].Sheets, Is.EqualTo(3));
            Assert.That(result.Outputs[0].ProducedWaste, Is.True);
            Assert.That(stored["Bauxite"], Is.EqualTo(0));
        });
    }

    [Test]
    public void ProcessorTickLeavesPartialInvalidInputStored()
    {
        var materials = new[]
        {
            new EmberMaterialPrototype
            {
                ID = "Bauxite",
                UnitsPerSheet = 2000,
            },
        };

        var stored = new Dictionary<string, int>
        {
            ["Bauxite"] = 1999,
        };

        var modes = new Dictionary<string, EmberMaterialProcessingMode>
        {
            ["Bauxite"] = EmberMaterialProcessingMode.Smelt,
        };

        var result = EmberMaterialProcessing.ProcessTick(materials, stored, modes, 10);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outputs, Is.Empty);
            Assert.That(stored["Bauxite"], Is.EqualTo(1999));
        });
    }
}

