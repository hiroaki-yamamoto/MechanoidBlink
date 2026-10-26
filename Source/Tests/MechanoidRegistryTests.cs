using System;
using System.Collections.Generic;
using System.Reflection;
using MechanoidBlink.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RimWorld;
using Verse;

namespace MechanoidBlink.Tests;

[TestClass]
public class MechanoidRegistryTests
{
    [TestInitialize]
    public void InitDefOfs()
    {
        if (FleshTypeDefOf.Mechanoid == null)
        {
            FleshTypeDefOf.Mechanoid = new FleshTypeDef { defName = "Mechanoid" };
        }
        if (FleshTypeDefOf.Normal == null)
        {
            FleshTypeDefOf.Normal = new FleshTypeDef { defName = "Normal" };
        }

        MechanoidRegistry.ResetCache();
    }

    [TestMethod]
    public void Test_IsMechanoidDef_Null_ReturnsFalse()
    {
        Assert.IsFalse(MechanoidRegistry.IsMechanoidDef(null));
    }

    [TestMethod]
    public void Test_IsMechanoidDef_ValidMechanoidPawn_ReturnsTrue()
    {
        var def = CreateThingDef(
            defName: "Mech_Centipede_Test",
            category: ThingCategory.Pawn,
            isMechanoid: true,
            thingClass: typeof(Pawn));

        Assert.IsTrue(MechanoidRegistry.IsMechanoidDef(def), "Valid mechanoid pawn def should be recognized as mechanoid");
    }

    [TestMethod]
    public void Test_IsMechanoidDef_Corpse_ReturnsFalse()
    {
        // RimWorld generated corpses inherit race from the pawn, but have category Item and thingClass Corpse
        var corpseDef = CreateThingDef(
            defName: "Corpse_Mech_Centipede_Test",
            category: ThingCategory.Item,
            isMechanoid: true,
            thingClass: typeof(Corpse));

        Assert.IsFalse(MechanoidRegistry.IsMechanoidDef(corpseDef), "Mechanoid corpse should be excluded from mechanoids list");
    }

    [TestMethod]
    public void Test_IsMechanoidDef_NonMechanoidPawn_ReturnsFalse()
    {
        var def = CreateThingDef(
            defName: "Human_Test",
            category: ThingCategory.Pawn,
            isMechanoid: false,
            thingClass: typeof(Pawn));

        Assert.IsFalse(MechanoidRegistry.IsMechanoidDef(def), "Non-mechanoid pawn should not be recognized as mechanoid");
    }

    [TestMethod]
    public void Test_IsMechanoidDef_BuildingOrItem_ReturnsFalse()
    {
        var buildingDef = new ThingDef
        {
            defName = "MechAssembler_Test",
            category = ThingCategory.Building
        };
        Assert.IsFalse(MechanoidRegistry.IsMechanoidDef(buildingDef), "Building should not be recognized as mechanoid");

        var itemDef = new ThingDef
        {
            defName = "Steel_Test",
            category = ThingCategory.Item
        };
        Assert.IsFalse(MechanoidRegistry.IsMechanoidDef(itemDef), "Item should not be recognized as mechanoid");
    }

    [TestMethod]
    public void Test_IsMechanoidDef_NullRaceProps_ReturnsFalse()
    {
        var def = new ThingDef
        {
            defName = "NoRace_Test",
            category = ThingCategory.Pawn,
            race = null
        };
        Assert.IsFalse(MechanoidRegistry.IsMechanoidDef(def));
    }

    [TestMethod]
    public void Test_AllMechanoidDefNames_ReturnsCachedListWithoutAllocations()
    {
        // Ensure cache is populated
        var names1 = MechanoidRegistry.AllMechanoidDefNames;
        var names2 = MechanoidRegistry.AllMechanoidDefNames;

        Assert.IsNotNull(names1);
        Assert.IsNotNull(names2);
        // Verify O(1) zero-allocation cache reuse (same instance returned)
        Assert.AreSame(names1, names2, "AllMechanoidDefNames should return the same cached instance on repeated calls");
    }

    [TestMethod]
    public void Test_IsMechanoidDefName_CaseInsensitiveLookup()
    {
        var def = CreateThingDef(
            defName: "Mech_Lancer_LookupTest",
            category: ThingCategory.Pawn,
            isMechanoid: true,
            thingClass: typeof(Pawn));

        // Add to DefDatabase
        if (DefDatabase<ThingDef>.GetNamedSilentFail(def.defName) == null)
        {
            DefDatabase<ThingDef>.Add(def);
        }

        MechanoidRegistry.ResetCache();

        Assert.IsTrue(MechanoidRegistry.IsMechanoidDefName("Mech_Lancer_LookupTest"));
        Assert.IsTrue(MechanoidRegistry.IsMechanoidDefName("mech_lancer_lookuptest"), "Lookup should be case-insensitive");
        Assert.IsFalse(MechanoidRegistry.IsMechanoidDefName("NonExistent_Def"));
        Assert.IsFalse(MechanoidRegistry.IsMechanoidDefName(null));
        Assert.IsFalse(MechanoidRegistry.IsMechanoidDefName(string.Empty));
    }

    [TestMethod]
    public void Test_CustomRegistryInstance_CanBeInjected()
    {
        var original = MechanoidRegistry.Instance;
        try
        {
            var mock = new FakeMechanoidRegistry();
            MechanoidRegistry.Instance = mock;

            Assert.AreSame(mock, MechanoidRegistry.Instance);
            Assert.IsTrue(MechanoidRegistry.IsMechanoidDefName("MockMech"));
            Assert.IsFalse(MechanoidRegistry.IsMechanoidDefName("OtherMech"));
        }
        finally
        {
            MechanoidRegistry.Instance = original;
        }
    }

    private class FakeMechanoidRegistry : IMechanoidRegistry
    {
        public IReadOnlyList<ThingDef> AllMechanoids => Array.Empty<ThingDef>();
        public IReadOnlyList<string> AllMechanoidDefNames => new[] { "MockMech" };
        public bool IsMechanoidDef(ThingDef? def) => def?.defName == "MockMech";
        public bool IsMechanoidDefName(string? defName) => defName == "MockMech";
        public void ResetCache() { }
    }

    private static ThingDef CreateThingDef(string defName, ThingCategory category, bool isMechanoid, Type thingClass)
    {
        var raceProps = new RaceProperties();
        var fleshField = typeof(RaceProperties).GetField("fleshType", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        fleshField?.SetValue(raceProps, isMechanoid ? FleshTypeDefOf.Mechanoid : FleshTypeDefOf.Normal);

        return new ThingDef
        {
            defName = defName,
            category = category,
            race = raceProps,
            thingClass = thingClass
        };
    }
}
