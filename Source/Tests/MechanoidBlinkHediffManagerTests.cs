using System;
using System.Collections.Generic;
using MechanoidBlink.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RimWorld;
using Verse;

namespace MechanoidBlink.Tests;

[TestClass]
public class MechanoidBlinkHediffManagerTests
{
    private class MockHediffManager : IMechanoidBlinkHediffManager
    {
        public bool HasHediffResult { get; set; } = false;
        public bool ApplyHediffCalled { get; private set; }
        public bool RemoveHediffCalled { get; private set; }
        public Pawn? LastUpdatedPawn { get; private set; }
        public bool UpdateAllCalled { get; private set; }

        public HediffDef? GetHediffDef() => null;
        public void ResetCache() { }

        public bool HasHediff(Pawn? pawn) => HasHediffResult;

        public bool ApplyHediff(Pawn? pawn)
        {
            ApplyHediffCalled = true;
            return true;
        }

        public bool RemoveHediff(Pawn? pawn)
        {
            RemoveHediffCalled = true;
            return true;
        }

        public void UpdateMechanoid(Pawn? pawn)
        {
            LastUpdatedPawn = pawn;
        }

        public void UpdateAllMechanoids()
        {
            UpdateAllCalled = true;
        }
    }

    [TestMethod]
    public void Test_StaticFacade_DelegatesToInstance()
    {
        var original = MechanoidBlinkHediffManager.Instance;
        try
        {
            var mock = new MockHediffManager();
            MechanoidBlinkHediffManager.Instance = mock;

            Assert.AreSame(mock, MechanoidBlinkHediffManager.Instance);

            var pawn = new Pawn();
            MechanoidBlinkHediffManager.UpdateMechanoid(pawn);
            Assert.AreSame(pawn, mock.LastUpdatedPawn);

            MechanoidBlinkHediffManager.ApplyHediff(pawn);
            Assert.IsTrue(mock.ApplyHediffCalled);

            MechanoidBlinkHediffManager.RemoveHediff(pawn);
            Assert.IsTrue(mock.RemoveHediffCalled);

            MechanoidBlinkHediffManager.UpdateAllMechanoids();
            Assert.IsTrue(mock.UpdateAllCalled);
        }
        finally
        {
            MechanoidBlinkHediffManager.Instance = original;
        }
    }

    [TestMethod]
    public void Test_Instance_NullAssignment_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(() =>
        {
            MechanoidBlinkHediffManager.Instance = null!;
        });
    }

    [TestMethod]
    public void Test_UpdateMechanoid_HandlesNullPawnSafely()
    {
        // Should not throw
        var manager = new RimWorldMechanoidBlinkHediffManager();
        manager.UpdateMechanoid(null);
    }

    [TestMethod]
    public void Test_UpdateMechanoid_HandlesNullPawnDefSafely()
    {
        var manager = new RimWorldMechanoidBlinkHediffManager();
        var pawn = new Pawn { def = null };
        manager.UpdateMechanoid(pawn);
    }
}
