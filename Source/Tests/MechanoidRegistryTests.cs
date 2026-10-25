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
            defName: "Mech_Centipede",
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
            defName: "Corpse_Mech_Centipede",
            category: ThingCategory.Item,
            isMechanoid: true,
            thingClass: typeof(Corpse));

        Assert.IsFalse(MechanoidRegistry.IsMechanoidDef(corpseDef), "Mechanoid corpse should be excluded from mechanoids list");
    }

    [TestMethod]
    public void Test_IsMechanoidDef_NonMechanoidPawn_ReturnsFalse()
    {
        var def = CreateThingDef(
            defName: "Human",
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
            defName = "MechAssembler",
            category = ThingCategory.Building
        };
        Assert.IsFalse(MechanoidRegistry.IsMechanoidDef(buildingDef), "Building should not be recognized as mechanoid");

        var itemDef = new ThingDef
        {
            defName = "Steel",
            category = ThingCategory.Item
        };
        Assert.IsFalse(MechanoidRegistry.IsMechanoidDef(itemDef), "Item should not be recognized as mechanoid");
    }

    private static ThingDef CreateThingDef(string defName, ThingCategory category, bool isMechanoid, System.Type thingClass)
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
