using System;
using System.Collections.Generic;
using MechanoidBlink.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Verse;

namespace MechanoidBlink.Tests;

[TestClass]
public class MechanoidBlinkSettingsUiTests
{
    [TestInitialize]
    public void Setup()
    {
        MechanoidBlinkSettingsUi.ResetFilterCache();
    }

    [TestMethod]
    public void Test_FilterMechanoids_EmptyOrNullFilter_ReturnsOriginalListDirectly()
    {
        var list = new List<ThingDef>
        {
            new() { defName = "Mech_Centipede", label = "Centipede" },
            new() { defName = "Mech_Lancer", label = "Lancer" }
        };

        // When filter is null or whitespace, it should return the exact same list instance (zero allocations)
        var resultNull = MechanoidBlinkSettingsUi.FilterMechanoids(list, null);
        Assert.AreSame(list, resultNull, "Null filter should return original instance");

        var resultEmpty = MechanoidBlinkSettingsUi.FilterMechanoids(list, "");
        Assert.AreSame(list, resultEmpty, "Empty filter should return original instance");

        var resultWhitespace = MechanoidBlinkSettingsUi.FilterMechanoids(list, "   ");
        Assert.AreSame(list, resultWhitespace, "Whitespace filter should return original instance");
    }

    [TestMethod]
    public void Test_FilterMechanoids_NullOrEmptyList_ReturnsEmptyArray()
    {
        var resultNull = MechanoidBlinkSettingsUi.FilterMechanoids(null!, "mech");
        Assert.AreEqual(0, resultNull.Count);

        var resultEmpty = MechanoidBlinkSettingsUi.FilterMechanoids(Array.Empty<ThingDef>(), "mech");
        Assert.AreEqual(0, resultEmpty.Count);
    }

    [TestMethod]
    public void Test_FilterMechanoids_MatchesLabel_CaseInsensitive()
    {
        var centipede = new ThingDef { defName = "Mech_Centipede", label = "Heavy Centipede" };
        var lancer = new ThingDef { defName = "Mech_Lancer", label = "Sniper Lancer" };
        var scyther = new ThingDef { defName = "Mech_Scyther", label = "Blade Scyther" };

        var list = new List<ThingDef> { centipede, lancer, scyther };

        var results = MechanoidBlinkSettingsUi.FilterMechanoids(list, "heavy");
        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Heavy Centipede", results[0].label);

        var resultsUpper = MechanoidBlinkSettingsUi.FilterMechanoids(list, "LANCER");
        Assert.AreEqual(1, resultsUpper.Count);
        Assert.AreEqual("Sniper Lancer", resultsUpper[0].label);
    }

    [TestMethod]
    public void Test_FilterMechanoids_MatchesDefName()
    {
        var mechaA = new ThingDef { defName = "Special_Mechanoid_Alpha", label = "Alpha" };
        var mechaB = new ThingDef { defName = "Special_Mechanoid_Beta", label = "Beta" };
        var walker = new ThingDef { defName = "Combat_Walker", label = "Walker" };

        var list = new List<ThingDef> { mechaA, mechaB, walker };

        var results = MechanoidBlinkSettingsUi.FilterMechanoids(list, "special_mechanoid");
        Assert.AreEqual(2, results.Count);
        Assert.IsTrue(results.Contains(mechaA));
        Assert.IsTrue(results.Contains(mechaB));
    }

    [TestMethod]
    public void Test_FilterMechanoids_NoMatch_ReturnsEmptyList()
    {
        var list = new List<ThingDef>
        {
            new() { defName = "Mech_Centipede", label = "Centipede" }
        };

        var results = MechanoidBlinkSettingsUi.FilterMechanoids(list, "NonExistentTerm12345");
        Assert.AreEqual(0, results.Count);
    }
}
