using System.Collections.Generic;
using MechanoidBlink.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MechanoidBlink.Tests;

[TestClass]
public class MechanoidBlinkSettingsTests
{
    [TestMethod]
    public void Test_DefaultValues()
    {
        var settings = new MechanoidBlinkSettings();
        Assert.IsTrue(settings.enableFriendly, "Default enableFriendly should be true");
        Assert.IsFalse(settings.enableHostile, "Default enableHostile should be false");
        Assert.IsFalse(settings.initialized, "Default initialized should be false");
        Assert.AreEqual(0, settings.allowedMechDefs.Count, "Default allowedMechDefs should be empty");
    }

    [TestMethod]
    public void Test_Uninitialized_AllowsAll()
    {
        var settings = new MechanoidBlinkSettings();
        // When not initialized, any defName should be allowed
        Assert.IsTrue(settings.IsMechTypeAllowed("Mech_Centipede"), "Uninitialized should allow Mech_Centipede");
        Assert.IsTrue(settings.IsMechTypeAllowed("Mech_Lancer"), "Uninitialized should allow Mech_Lancer");
        Assert.IsFalse(settings.IsMechTypeAllowed(""), "Empty defName should not be allowed");
        Assert.IsFalse(settings.IsMechTypeAllowed(null!), "Null defName should not be allowed");
    }

    [TestMethod]
    public void Test_SelectAll()
    {
        var settings = new MechanoidBlinkSettings();
        var allDefs = new List<string> { "Mech_Centipede", "Mech_Lancer", "Mech_Scyther" };
        settings.SelectAll(allDefs);

        Assert.IsTrue(settings.initialized, "SelectAll should mark initialized as true");
        Assert.IsTrue(settings.IsMechTypeAllowed("Mech_Centipede"), "Centipede should be allowed");
        Assert.IsTrue(settings.IsMechTypeAllowed("Mech_Lancer"), "Lancer should be allowed");
        Assert.IsTrue(settings.IsMechTypeAllowed("Mech_Scyther"), "Scyther should be allowed");
        Assert.IsFalse(settings.IsMechTypeAllowed("Mech_Pikeman"), "Pikeman was not in list, should not be allowed");
    }

    [TestMethod]
    public void Test_DeselectAll()
    {
        var settings = new MechanoidBlinkSettings();
        var allDefs = new List<string> { "Mech_Centipede", "Mech_Lancer" };
        settings.SelectAll(allDefs);
        settings.DeselectAll();

        Assert.IsTrue(settings.initialized, "DeselectAll should mark initialized as true");
        Assert.IsFalse(settings.IsMechTypeAllowed("Mech_Centipede"), "Centipede should not be allowed after DeselectAll");
        Assert.IsFalse(settings.IsMechTypeAllowed("Mech_Lancer"), "Lancer should not be allowed after DeselectAll");
    }

    [TestMethod]
    public void Test_SetMechTypeAllowed()
    {
        var settings = new MechanoidBlinkSettings();
        settings.SelectAll(new[] { "Mech_Centipede" });

        // Add Lancer
        settings.SetMechTypeAllowed("Mech_Lancer", true);
        Assert.IsTrue(settings.IsMechTypeAllowed("Mech_Lancer"), "Lancer should now be allowed");

        // Remove Centipede
        settings.SetMechTypeAllowed("Mech_Centipede", false);
        Assert.IsFalse(settings.IsMechTypeAllowed("Mech_Centipede"), "Centipede should now be disallowed");
    }

    [TestMethod]
    public void Test_EnsureInitialized()
    {
        var settings = new MechanoidBlinkSettings();
        var availableDefs = new[] { "Mech_Centipede", "Mech_Lancer" };

        settings.EnsureInitialized(availableDefs);
        Assert.IsTrue(settings.initialized, "EnsureInitialized should mark initialized as true");
        Assert.IsTrue(settings.IsMechTypeAllowed("Mech_Centipede"), "Centipede should be allowed");

        // Calling it again when already initialized should NOT overwrite existing settings
        settings.SetMechTypeAllowed("Mech_Centipede", false);
        settings.EnsureInitialized(availableDefs);
        Assert.IsFalse(settings.IsMechTypeAllowed("Mech_Centipede"), "Second EnsureInitialized should not reset user setting");
    }

    [TestMethod]
    public void Test_SelectAll_NullCollection_DoesNotThrow()
    {
        var settings = new MechanoidBlinkSettings();
        settings.SelectAll(null);
        Assert.IsTrue(settings.initialized);
        Assert.AreEqual(0, settings.allowedMechDefs.Count);
    }

    [TestMethod]
    public void Test_SelectAll_FiltersOutNullOrEmpty()
    {
        var settings = new MechanoidBlinkSettings();
        settings.SelectAll(new[] { "Mech_Centipede", "", null!, "Mech_Lancer" });
        Assert.IsTrue(settings.IsMechTypeAllowed("Mech_Centipede"));
        Assert.IsTrue(settings.IsMechTypeAllowed("Mech_Lancer"));
        Assert.AreEqual(2, settings.allowedMechDefs.Count);
    }

    [TestMethod]
    public void Test_SetMechTypeAllowed_NullOrEmpty_Ignored()
    {
        var settings = new MechanoidBlinkSettings();
        settings.SetMechTypeAllowed("", true);
        settings.SetMechTypeAllowed(null!, true);
        Assert.AreEqual(0, settings.allowedMechDefs.Count);
    }

    [TestMethod]
    public void Test_IsMechTypeAllowed_CaseInsensitive()
    {
        var settings = new MechanoidBlinkSettings();
        settings.SelectAll(new[] { "Mech_Centipede" });
        Assert.IsTrue(settings.IsMechTypeAllowed("mech_centipede"), "Lookup should be case-insensitive");
        Assert.IsTrue(settings.IsMechTypeAllowed("MECH_CENTIPEDE"), "Lookup should be case-insensitive");
    }
}

