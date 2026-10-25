using System;
using System.Collections.Generic;
using System.Reflection;
using MechanoidBlink.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RimWorld;
using Verse;

namespace MechanoidBlink.Tests;

[TestClass]
public class MechanoidBlinkPermissionCheckerTests
{
    private class FakeSettingsProvider : ISettingsProvider
    {
        public bool EnableFriendly { get; set; } = true;
        public bool EnableHostile { get; set; } = false;
        public HashSet<string> AllowedDefs { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool IsMechTypeAllowed(string defName)
        {
            return AllowedDefs.Contains(defName);
        }
    }

    private class FakeHostilityChecker : IFactionHostilityChecker
    {
        public bool Hostile { get; set; } = false;
        public bool IsHostileToPlayer(Pawn pawn) => Hostile;
    }

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
    public void Test_NullPawn_ReturnsFalse()
    {
        var settings = new FakeSettingsProvider();
        var checker = new MechanoidBlinkPermissionChecker(settings);
        Assert.IsFalse(checker.IsBlinkAllowed(null!), "Null pawn should return false");
    }

    [TestMethod]
    public void Test_Friendly_Allowed()
    {
        var settings = new FakeSettingsProvider { EnableFriendly = true };
        settings.AllowedDefs.Add("Mech_Centipede");
        var hostility = new FakeHostilityChecker { Hostile = false };

        var checker = new MechanoidBlinkPermissionChecker(settings, hostility);
        var pawn = CreateMockPawn("Mech_Centipede", isMechanoid: true);

        Assert.IsTrue(checker.IsBlinkAllowed(pawn), "Friendly mech should be allowed when EnableFriendly is true and type is allowed");
    }

    [TestMethod]
    public void Test_Friendly_Disallowed_WhenSettingFalse()
    {
        var settings = new FakeSettingsProvider { EnableFriendly = false };
        settings.AllowedDefs.Add("Mech_Centipede");
        var hostility = new FakeHostilityChecker { Hostile = false };

        var checker = new MechanoidBlinkPermissionChecker(settings, hostility);
        var pawn = CreateMockPawn("Mech_Centipede", isMechanoid: true);

        Assert.IsFalse(checker.IsBlinkAllowed(pawn), "Friendly mech should be disallowed when EnableFriendly is false");
    }

    [TestMethod]
    public void Test_Hostile_Allowed_WhenSettingTrue()
    {
        var settings = new FakeSettingsProvider { EnableHostile = true };
        settings.AllowedDefs.Add("Mech_Centipede");
        var hostility = new FakeHostilityChecker { Hostile = true };

        var checker = new MechanoidBlinkPermissionChecker(settings, hostility);
        var pawn = CreateMockPawn("Mech_Centipede", isMechanoid: true);

        Assert.IsTrue(checker.IsBlinkAllowed(pawn), "Hostile mech should be allowed when EnableHostile is true and type is allowed");
    }

    [TestMethod]
    public void Test_Hostile_Disallowed_WhenSettingFalse()
    {
        var settings = new FakeSettingsProvider { EnableHostile = false };
        settings.AllowedDefs.Add("Mech_Centipede");
        var hostility = new FakeHostilityChecker { Hostile = true };

        var checker = new MechanoidBlinkPermissionChecker(settings, hostility);
        var pawn = CreateMockPawn("Mech_Centipede", isMechanoid: true);

        Assert.IsFalse(checker.IsBlinkAllowed(pawn), "Hostile mech should be disallowed when EnableHostile is false");
    }

    [TestMethod]
    public void Test_TypeDisallowed_BlocksFriendlyAndHostile()
    {
        var settings = new FakeSettingsProvider
        {
            EnableFriendly = true,
            EnableHostile = true
        };
        // Not adding "Mech_Centipede" to AllowedDefs
        var hostility = new FakeHostilityChecker();

        var checker = new MechanoidBlinkPermissionChecker(settings, hostility);
        var pawn = CreateMockPawn("Mech_Centipede", isMechanoid: true);

        // Friendly test
        hostility.Hostile = false;
        Assert.IsFalse(checker.IsBlinkAllowed(pawn), "Friendly mech should be blocked if its type is not allowed");

        // Hostile test
        hostility.Hostile = true;
        Assert.IsFalse(checker.IsBlinkAllowed(pawn), "Hostile mech should be blocked if its type is not allowed");
    }

    [TestMethod]
    public void Test_NonMechanoid_NotRestricted()
    {
        var settings = new FakeSettingsProvider
        {
            EnableFriendly = false,
            EnableHostile = false
        };
        var hostility = new FakeHostilityChecker();

        var checker = new MechanoidBlinkPermissionChecker(settings, hostility);
        var humanPawn = CreateMockPawn("Human", isMechanoid: false);

        Assert.IsTrue(checker.IsBlinkAllowed(humanPawn), "Non-mechanoid pawn should not be restricted by MechanoidBlink");
    }

    [TestMethod]
    public void Test_CorpseDefMechanoid_Disallowed()
    {
        var settings = new FakeSettingsProvider { EnableFriendly = true };
        settings.AllowedDefs.Add("Corpse_Mech_Centipede");

        var checker = new MechanoidBlinkPermissionChecker(settings);
        var pawn = CreateMockPawn("Corpse_Mech_Centipede", isMechanoid: true, isCorpse: true);

        Assert.IsFalse(checker.IsBlinkAllowed(pawn), "Corpse mechanoid should not be allowed to blink");
    }

    private static Pawn CreateMockPawn(string defName, bool isMechanoid, bool isCorpse = false)
    {
        var raceProps = new RaceProperties();
        var fleshField = typeof(RaceProperties).GetField("fleshType", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        fleshField?.SetValue(raceProps, isMechanoid ? FleshTypeDefOf.Mechanoid : FleshTypeDefOf.Normal);

        var def = new ThingDef
        {
            defName = defName,
            race = raceProps,
            category = isCorpse ? ThingCategory.Item : ThingCategory.Pawn,
            thingClass = isCorpse ? typeof(Corpse) : typeof(Pawn)
        };

        return new Pawn
        {
            def = def
        };
    }
}

