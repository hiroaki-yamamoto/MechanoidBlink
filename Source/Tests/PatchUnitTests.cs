using System;
using System.Collections.Generic;
using System.Reflection;
using MechanoidBlink.Core;
using MechanoidBlink.Patches;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RimWorld;
using Verse;

namespace MechanoidBlink.Tests;

[TestClass]
public class PatchUnitTests
{
    private class SpyHediffManager : IMechanoidBlinkHediffManager
    {
        public List<Pawn> UpdatedPawns { get; } = new();

        public HediffDef? GetHediffDef() => null;
        public void ResetCache() { }
        public bool HasHediff(Pawn? pawn) => false;
        public bool ApplyHediff(Pawn? pawn) => true;
        public bool RemoveHediff(Pawn? pawn) => true;

        public void UpdateMechanoid(Pawn? pawn)
        {
            if (pawn != null)
            {
                UpdatedPawns.Add(pawn);
            }
        }

        public void UpdateAllMechanoids() { }
    }

    private IMechanoidBlinkHediffManager originalManager = null!;
    private SpyHediffManager spyManager = null!;

    [TestInitialize]
    public void Setup()
    {
        if (FleshTypeDefOf.Mechanoid == null)
        {
            FleshTypeDefOf.Mechanoid = new FleshTypeDef { defName = "Mechanoid" };
        }
        if (FleshTypeDefOf.Normal == null)
        {
            FleshTypeDefOf.Normal = new FleshTypeDef { defName = "Normal" };
        }

        originalManager = MechanoidBlinkHediffManager.Instance;
        spyManager = new SpyHediffManager();
        MechanoidBlinkHediffManager.Instance = spyManager;
    }

    [TestCleanup]
    public void Cleanup()
    {
        MechanoidBlinkHediffManager.Instance = originalManager;
    }

    [TestMethod]
    public void Test_Patch_Pawn_SpawnSetup_InvokesUpdateForMechanoidsOnly()
    {
        var mech = CreateTestPawn(isMechanoid: true);
        var human = CreateTestPawn(isMechanoid: false);

        Patch_Pawn_SpawnSetup.Postfix(mech);
        Assert.AreEqual(1, spyManager.UpdatedPawns.Count);
        Assert.AreSame(mech, spyManager.UpdatedPawns[0]);

        Patch_Pawn_SpawnSetup.Postfix(human);
        Assert.AreEqual(1, spyManager.UpdatedPawns.Count, "Non-mechanoids should not trigger UpdateMechanoid in SpawnSetup patch");
    }

    [TestMethod]
    public void Test_Patch_CompCanBeDormant_WakeUp_InvokesUpdateForMechanoidsOnly()
    {
        var mech = CreateTestPawn(isMechanoid: true);
        var human = CreateTestPawn(isMechanoid: false);

        var mechComp = new CompCanBeDormant { parent = mech };
        Patch_CompCanBeDormant_WakeUp.Postfix(mechComp);
        Assert.AreEqual(1, spyManager.UpdatedPawns.Count);
        Assert.AreSame(mech, spyManager.UpdatedPawns[0]);

        var humanComp = new CompCanBeDormant { parent = human };
        Patch_CompCanBeDormant_WakeUp.Postfix(humanComp);
        Assert.AreEqual(1, spyManager.UpdatedPawns.Count, "Non-mechanoids should not trigger UpdateMechanoid on WakeUp");
    }

    [TestMethod]
    public void Test_Patch_Pawn_SetFaction_InvokesUpdateForMechanoidsOnly()
    {
        var mech = CreateTestPawn(isMechanoid: true);
        var human = CreateTestPawn(isMechanoid: false);
        var dummyFaction = new Faction();

        Patch_Pawn_SetFaction.Postfix(mech, dummyFaction);
        Assert.AreEqual(1, spyManager.UpdatedPawns.Count);
        Assert.AreSame(mech, spyManager.UpdatedPawns[0]);

        Patch_Pawn_SetFaction.Postfix(human, dummyFaction);
        Assert.AreEqual(1, spyManager.UpdatedPawns.Count, "Non-mechanoids should not trigger UpdateMechanoid on SetFaction");
    }

    [TestMethod]
    public void Test_Patch_MentalState_TryStartMentalState_RequiresResultTrueAndMechanoid()
    {
        var mech = CreateTestPawn(isMechanoid: true);

        // When __result is false, should not update
        Patch_MentalState.Postfix_TryStartMentalState(__result: false, ___pawn: mech);
        Assert.AreEqual(0, spyManager.UpdatedPawns.Count, "Failed mental state transition should not trigger update");

        // When __result is true, should update
        Patch_MentalState.Postfix_TryStartMentalState(__result: true, ___pawn: mech);
        Assert.AreEqual(1, spyManager.UpdatedPawns.Count);
        Assert.AreSame(mech, spyManager.UpdatedPawns[0]);

        // Non-mechanoid pawn
        var human = CreateTestPawn(isMechanoid: false);
        Patch_MentalState.Postfix_TryStartMentalState(__result: true, ___pawn: human);
        Assert.AreEqual(1, spyManager.UpdatedPawns.Count);
    }

    [TestMethod]
    public void Test_Patch_MentalState_ClearMentalStateDirect_UpdatesMechanoid()
    {
        var mech = CreateTestPawn(isMechanoid: true);
        var human = CreateTestPawn(isMechanoid: false);

        Patch_MentalState.Postfix_ClearMentalStateDirect(___pawn: mech);
        Assert.AreEqual(1, spyManager.UpdatedPawns.Count);

        Patch_MentalState.Postfix_ClearMentalStateDirect(___pawn: human);
        Assert.AreEqual(1, spyManager.UpdatedPawns.Count);
    }

    [TestMethod]
    public void Test_Patch_MentalState_Reset_UpdatesMechanoid()
    {
        var mech = CreateTestPawn(isMechanoid: true);
        var human = CreateTestPawn(isMechanoid: false);

        Patch_MentalState.Postfix_Reset(___pawn: mech);
        Assert.AreEqual(1, spyManager.UpdatedPawns.Count);

        Patch_MentalState.Postfix_Reset(___pawn: human);
        Assert.AreEqual(1, spyManager.UpdatedPawns.Count);
    }

    private static Pawn CreateTestPawn(bool isMechanoid)
    {
        var raceProps = new RaceProperties();
        var fleshField = typeof(RaceProperties).GetField("fleshType", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        fleshField?.SetValue(raceProps, isMechanoid ? FleshTypeDefOf.Mechanoid : FleshTypeDefOf.Normal);

        var def = new ThingDef
        {
            defName = isMechanoid ? "Test_Mech_Pawn" : "Test_Human_Pawn",
            category = ThingCategory.Pawn,
            race = raceProps,
            thingClass = typeof(Pawn)
        };

        return new Pawn { def = def };
    }
}
