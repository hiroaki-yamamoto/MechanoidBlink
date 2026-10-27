using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using AutoBlink;
using HarmonyLib;
using MechanoidBlink.Core;
using MechanoidBlink.Patches;
using MechanoidBlink.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MechanoidBlink.Tests;

/// <summary>
/// End-to-end integration and regression tests verifying MechanoidBlink's Hediff-based
/// AutoBlink integration, whole-body HediffWithComps safety, batch settings synchronization,
/// and lifecycle patches across spawning, dormancy, faction, and mental state changes.
/// </summary>
[TestClass]
public class AutoBlinkIntegrationTests
{
    private const string TestMechDefName = "Mech_IntegrationTest_Lifter";
    private const string TestCorpseDefName = "Corpse_Mech_IntegrationTest_Lifter";
    private const string TestHumanDefName = "Human_IntegrationTest";

    private sealed class ConfigurablePermissionChecker : IMechanoidBlinkPermissionChecker
    {
        public bool AllowFriendly { get; set; } = true;
        public bool AllowHostile { get; set; } = false;
        public HashSet<string> AllowedDefs { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool IsBlinkAllowed(Pawn pawn)
        {
            if (pawn == null || pawn.def == null || pawn.Destroyed || (pawn.health != null && pawn.Dead))
            {
                return false;
            }

            if (pawn.RaceProps == null || !pawn.RaceProps.IsMechanoid)
            {
                return true;
            }

            if (!MechanoidRegistry.IsMechanoidDef(pawn.def))
            {
                return false;
            }

            if (AllowedDefs.Count > 0 && !AllowedDefs.Contains(pawn.def.defName))
            {
                return false;
            }

            bool isHostile = pawn.Faction != null && !pawn.Faction.IsPlayer;
            return isHostile ? AllowHostile : AllowFriendly;
        }
    }

    [TestInitialize]
    public void SetUp()
    {
        if (FleshTypeDefOf.Mechanoid == null)
        {
            FleshTypeDefOf.Mechanoid = new FleshTypeDef { defName = "Mechanoid" };
        }

        if (FleshTypeDefOf.Normal == null)
        {
            FleshTypeDefOf.Normal = new FleshTypeDef { defName = "Normal" };
        }

        if (DefDatabase<PawnCapacityDef>.DefCount == 0)
        {
            DefDatabase<PawnCapacityDef>.Add(new PawnCapacityDef { defName = "Moving" });
        }

        try
        {
            MethodInfo getter = AccessTools.PropertyGetter(typeof(ModsConfig), "BiotechActive");
            if (getter != null)
            {
                Harmony harmony = new("test.modsconfig.patch");
                MethodInfo prefix = AccessTools.Method(typeof(AutoBlinkIntegrationTests), nameof(Mock_BiotechActive_Prefix));
                harmony.Patch(getter, prefix: new HarmonyMethod(prefix));
            }

            MethodInfo healthScaleGetter = AccessTools.PropertyGetter(typeof(Pawn), "HealthScale");
            if (healthScaleGetter != null)
            {
                Harmony harmony = new("test.healthscale.patch");
                MethodInfo prefix = AccessTools.Method(typeof(AutoBlinkIntegrationTests), nameof(Mock_HealthScale_Prefix));
                harmony.Patch(healthScaleGetter, prefix: new HarmonyMethod(prefix));
            }

            MethodInfo checkState = AccessTools.Method(typeof(Pawn_HealthTracker), "CheckForStateChange");
            if (checkState != null)
            {
                Harmony harmony = new("test.checkstate.patch");
                MethodInfo prefix = AccessTools.Method(typeof(AutoBlinkIntegrationTests), nameof(Mock_CheckState_Prefix));
                harmony.Patch(checkState, prefix: new HarmonyMethod(prefix));
            }
        }
        catch { }

        EnsureSoundDefRegistered("AutoBlink_Skip_Small");
        EnsureSoundDefRegistered("AutoBlink_Skip_Exit_Small");
        EnsureEffecterDefRegistered("Skip_Entry");
        EnsureEffecterDefRegistered("Skip_ExitNoDelay");

        EnsureProfileDefRegistered();
        EnsureHediffDefRegistered();
        MechanoidRegistry.ResetCache();
        MechanoidBlinkHediffManager.ResetCache();

        RunInitializerForTests();
    }

    public static bool Mock_HealthScale_Prefix(ref float __result)
    {
        __result = 1f;
        return false;
    }

    public static bool Mock_BiotechActive_Prefix(ref bool __result)
    {
        __result = false;
        return false;
    }

    public static bool Mock_CheckState_Prefix() => false;

    /// <summary>
    /// Verifies that the HediffDef conforms exactly to the specification in .agent/skills:
    /// HediffClass = HediffWithComps, Label Color = (0.52, 1.0, 0.95), Body Part = Whole body, isBad = false,
    /// initialSeverity = 1.0, maxSeverity = 1.0, and contains HediffCompProperties_AutoBlinkSource.
    /// </summary>
    [TestMethod]
    public void Test_HediffDef_ConformsToSpecification()
    {
        HediffDef? def = MechanoidBlinkHediffManager.GetHediffDef();
        Assert.IsNotNull(def, "Mechanoid Blink HediffDef must be registered in DefDatabase");
        Assert.AreEqual("Mechanoid Blink", def.label, "Label must be Mechanoid Blink");
        Assert.AreEqual("This mechanoid can teleport automatically.", def.description, "Description must match specification");
        Assert.AreEqual(typeof(HediffWithComps), def.hediffClass, "HediffClass must be HediffWithComps");
        Assert.AreEqual(new Color(0.52f, 1.0f, 0.95f), def.defaultLabelColor, "Label color must match specification");
        Assert.IsFalse(def.isBad, "isBad must be false");
        Assert.AreEqual(1.0f, def.initialSeverity, 0.001f, "initialSeverity must be 1.0");
        Assert.AreEqual(1.0f, def.maxSeverity, 0.001f, "maxSeverity must be 1.0");

        var compProps = def.comps?.OfType<HediffCompProperties_AutoBlinkSource>().FirstOrDefault();
        Assert.IsNotNull(compProps, "HediffDef must include HediffCompProperties_AutoBlinkSource");
        Assert.IsNotNull(compProps.profile, "HediffCompProperties_AutoBlinkSource must reference an AutoBlinkProfileDef");
        Assert.IsFalse(compProps.profile.playerFactionOnly, "Profile must allow hostile blinking when enabled in settings");
    }

    /// <summary>
    /// Verifies that <see cref="HediffWithComps"/> safely handles whole-body additions
    /// (<c>Part == null</c>) without throwing <see cref="NullReferenceException"/>
    /// and correctly triggers comp post-add logic to attach the AutoBlink controller.
    /// </summary>
    [TestMethod]
    public void Test_HediffWithComps_AppliedToWholeBody_InitializesAutoBlink()
    {
        HediffDef? def = MechanoidBlinkHediffManager.GetHediffDef();
        Assert.IsNotNull(def);

        (Game game, object gameComp, Faction playerFaction, _) = CreateTestGameEnvironment();
        try
        {
            ThingDef mechDef = RegisterOrGetThingDef(TestMechDefName, ThingCategory.Pawn, isMechanoid: true, typeof(Pawn));
            Pawn mechPawn = CreateSpawnedPawn(mechDef, playerFaction);
            mechPawn.health = new Pawn_HealthTracker(mechPawn);

            HediffWithComps hediff = (HediffWithComps)HediffMaker.MakeHediff(def, mechPawn, null);
            Assert.IsNull(hediff.Part, "Precondition: Part is null for whole-body hediff");

            // Add to health tracker
            mechPawn.health.AddHediff(hediff);
            Assert.IsTrue(MechanoidBlinkHediffManager.HasHediff(mechPawn), "Pawn must now possess the hediff");

            // Verify AutoBlinkGameComponent attached controller
            Type gameCompType = gameComp.GetType();
            MethodInfo tryGetMethod = AccessTools.Method(gameCompType, "TryGet");
            object?[] args = { mechPawn, null };
            bool hasController = (bool)tryGetMethod.Invoke(gameComp, args)!;
            Assert.IsTrue(hasController, "AutoBlink controller must be attached upon Hediff addition");
        }
        finally
        {
            Current.Game = null;
        }
    }

    /// <summary>
    /// Verifies that <see cref="MechanoidBlinkHediffManager.ApplyHediff"/> and
    /// <see cref="MechanoidBlinkHediffManager.RemoveHediff"/> correctly attach and detach
    /// the AutoBlink controller on the mechanoid pawn.
    /// </summary>
    [TestMethod]
    public void Test_HediffManager_ApplyAndRemoveHediff()
    {
        (Game game, object gameComp, Faction playerFaction, _) = CreateTestGameEnvironment();
        try
        {
            ThingDef mechDef = RegisterOrGetThingDef(TestMechDefName, ThingCategory.Pawn, isMechanoid: true, typeof(Pawn));
            Pawn mechPawn = CreateSpawnedPawn(mechDef, playerFaction);
            mechPawn.health = new Pawn_HealthTracker(mechPawn);

            // 1. Initial state: no hediff, no controller
            Assert.IsFalse(MechanoidBlinkHediffManager.HasHediff(mechPawn));

            // 2. Apply hediff
            bool applied = MechanoidBlinkHediffManager.ApplyHediff(mechPawn);
            Assert.IsTrue(applied, "ApplyHediff should return true when newly applied");
            Assert.IsTrue(MechanoidBlinkHediffManager.HasHediff(mechPawn));

            // Repeated apply is idempotent
            Assert.IsFalse(MechanoidBlinkHediffManager.ApplyHediff(mechPawn), "ApplyHediff should return false if already present");

            Type gameCompType = gameComp.GetType();
            MethodInfo tryGetMethod = AccessTools.Method(gameCompType, "TryGet");
            object?[] args = { mechPawn, null };
            bool hasController = (bool)tryGetMethod.Invoke(gameComp, args)!;
            Assert.IsTrue(hasController, "AutoBlink controller must be active");

            // 3. Remove hediff
            bool removed = MechanoidBlinkHediffManager.RemoveHediff(mechPawn);
            Assert.IsTrue(removed, "RemoveHediff should return true when removed");
            Assert.IsFalse(MechanoidBlinkHediffManager.HasHediff(mechPawn));

            object?[] argsAfter = { mechPawn, null };
            bool hasControllerAfter = (bool)tryGetMethod.Invoke(gameComp, argsAfter)!;
            Assert.IsFalse(hasControllerAfter, "AutoBlink controller must be detached after hediff removal");
        }
        finally
        {
            Current.Game = null;
        }
    }

    /// <summary>
    /// Verifies that <see cref="MechanoidBlinkHediffManager.UpdateMechanoid"/> respects
    /// permission checker decisions across friendly, hostile, and race filters.
    /// </summary>
    [TestMethod]
    public void Test_HediffManager_UpdateMechanoid_RespectsPermissions()
    {
        ConfigurablePermissionChecker checker = new()
        {
            AllowFriendly = true,
            AllowHostile = false
        };
        MechanoidBlinkMod.PermissionChecker = checker;

        (Game game, _, Faction playerFaction, Faction hostileFaction) = CreateTestGameEnvironment();
        try
        {
            ThingDef mechDef = RegisterOrGetThingDef(TestMechDefName, ThingCategory.Pawn, isMechanoid: true, typeof(Pawn));

            Pawn friendlyMech = CreateSpawnedPawn(mechDef, playerFaction);
            friendlyMech.health = new Pawn_HealthTracker(friendlyMech);

            Pawn hostileMech = CreateSpawnedPawn(mechDef, hostileFaction);
            hostileMech.health = new Pawn_HealthTracker(hostileMech);

            // Friendly mech: allowed by checker -> receives hediff
            MechanoidBlinkHediffManager.UpdateMechanoid(friendlyMech);
            Assert.IsTrue(MechanoidBlinkHediffManager.HasHediff(friendlyMech), "Friendly mech must receive hediff");

            // Hostile mech: disallowed by checker -> does not receive hediff
            MechanoidBlinkHediffManager.UpdateMechanoid(hostileMech);
            Assert.IsFalse(MechanoidBlinkHediffManager.HasHediff(hostileMech), "Hostile mech must not receive hediff");

            // Enable hostile in checker
            checker.AllowHostile = true;
            MechanoidBlinkHediffManager.UpdateMechanoid(hostileMech);
            Assert.IsTrue(MechanoidBlinkHediffManager.HasHediff(hostileMech), "Hostile mech must now receive hediff");

            // Disallow race in checker
            checker.AllowedDefs.Add("Different_Mech_Def");
            MechanoidBlinkHediffManager.UpdateMechanoid(friendlyMech);
            MechanoidBlinkHediffManager.UpdateMechanoid(hostileMech);
            Assert.IsFalse(MechanoidBlinkHediffManager.HasHediff(friendlyMech), "Friendly mech must lose hediff when race disallowed");
            Assert.IsFalse(MechanoidBlinkHediffManager.HasHediff(hostileMech), "Hostile mech must lose hediff when race disallowed");
        }
        finally
        {
            Current.Game = null;
        }
    }

    /// <summary>
    /// Circumstance 1: Verifies that <see cref="Patch_Pawn_SpawnSetup"/> grants or denies
    /// the Hediff the moment a mechanoid appears on a map.
    /// </summary>
    [TestMethod]
    public void Test_Circumstance1_SpawnSetup_GrantsHediff()
    {
        ConfigurablePermissionChecker checker = new() { AllowFriendly = true };
        MechanoidBlinkMod.PermissionChecker = checker;

        (Game game, _, Faction playerFaction, _) = CreateTestGameEnvironment();
        try
        {
            ThingDef mechDef = RegisterOrGetThingDef(TestMechDefName, ThingCategory.Pawn, isMechanoid: true, typeof(Pawn));
            Pawn mechPawn = CreateSpawnedPawn(mechDef, playerFaction);
            mechPawn.health = new Pawn_HealthTracker(mechPawn);

            Assert.IsFalse(MechanoidBlinkHediffManager.HasHediff(mechPawn));

            Patch_Pawn_SpawnSetup.Postfix(mechPawn);

            Assert.IsTrue(MechanoidBlinkHediffManager.HasHediff(mechPawn), "SpawnSetup postfix must grant Hediff to eligible mechanoid");
        }
        finally
        {
            Current.Game = null;
        }
    }

    /// <summary>
    /// Circumstance 1: Verifies that <see cref="Patch_CompCanBeDormant_WakeUp"/> updates the Hediff
    /// the moment a dormant mechanoid wakes up.
    /// </summary>
    [TestMethod]
    public void Test_Circumstance1_CompCanBeDormant_WakeUp_UpdatesHediff()
    {
        ConfigurablePermissionChecker checker = new() { AllowFriendly = true };
        MechanoidBlinkMod.PermissionChecker = checker;

        (Game game, _, Faction playerFaction, _) = CreateTestGameEnvironment();
        try
        {
            ThingDef mechDef = RegisterOrGetThingDef(TestMechDefName, ThingCategory.Pawn, isMechanoid: true, typeof(Pawn));
            Pawn mechPawn = CreateSpawnedPawn(mechDef, playerFaction);
            mechPawn.health = new Pawn_HealthTracker(mechPawn);

            CompCanBeDormant dormantComp = new() { parent = mechPawn };

            Patch_CompCanBeDormant_WakeUp.Postfix(dormantComp);

            Assert.IsTrue(MechanoidBlinkHediffManager.HasHediff(mechPawn), "WakeUp postfix must grant Hediff to eligible mechanoid");
        }
        finally
        {
            Current.Game = null;
        }
    }

    /// <summary>
    /// Circumstance 2: Verifies that <see cref="MechanoidBlinkHediffManager.UpdateAllMechanoids"/>
    /// batch-synchronizes all mechanoids on maps and in the world when mod settings change.
    /// </summary>
    [TestMethod]
    public void Test_Circumstance2_ModSettingsChange_BatchUpdatesAllMechanoids()
    {
        ConfigurablePermissionChecker checker = new()
        {
            AllowFriendly = true,
            AllowHostile = false
        };
        MechanoidBlinkMod.PermissionChecker = checker;

        (Game game, _, Faction playerFaction, Faction hostileFaction) = CreateTestGameEnvironment();
        try
        {
            ThingDef mechDef = RegisterOrGetThingDef(TestMechDefName, ThingCategory.Pawn, isMechanoid: true, typeof(Pawn));

            Pawn friendlyMech = CreateSpawnedPawn(mechDef, playerFaction);
            friendlyMech.health = new Pawn_HealthTracker(friendlyMech);

            Pawn hostileMech = CreateSpawnedPawn(mechDef, hostileFaction);
            hostileMech.health = new Pawn_HealthTracker(hostileMech);

            Map map = game.Maps[0];
            AccessTools.Field(typeof(MapPawns), "pawnsSpawned")?.SetValue(map.mapPawns, new List<Pawn> { friendlyMech, hostileMech });

            // Batch sync #1: friendly enabled, hostile disabled
            ExecuteOnMockMainThread(() => MechanoidBlinkHediffManager.UpdateAllMechanoids());
            Assert.IsTrue(MechanoidBlinkHediffManager.HasHediff(friendlyMech));
            Assert.IsFalse(MechanoidBlinkHediffManager.HasHediff(hostileMech));

            // Player toggles "Enable Hostile" in settings
            checker.AllowHostile = true;
            ExecuteOnMockMainThread(() => MechanoidBlinkHediffManager.UpdateAllMechanoids());
            Assert.IsTrue(MechanoidBlinkHediffManager.HasHediff(friendlyMech));
            Assert.IsTrue(MechanoidBlinkHediffManager.HasHediff(hostileMech), "Hostile mech must receive hediff after settings update");

            // Player toggles "Enable Friendly" off
            checker.AllowFriendly = false;
            ExecuteOnMockMainThread(() => MechanoidBlinkHediffManager.UpdateAllMechanoids());
            Assert.IsFalse(MechanoidBlinkHediffManager.HasHediff(friendlyMech), "Friendly mech must lose hediff after settings update");
            Assert.IsTrue(MechanoidBlinkHediffManager.HasHediff(hostileMech));
        }
        finally
        {
            Current.Game = null;
        }
    }

    /// <summary>
    /// Circumstance 3: Verifies that <see cref="Patch_Pawn_SetFaction"/> reassigns the Hediff
    /// when an enemy mechanoid is hacked or tamed to fall under colony control.
    /// </summary>
    [TestMethod]
    public void Test_Circumstance3_FactionChange_UpdatesHediff()
    {
        ConfigurablePermissionChecker checker = new()
        {
            AllowFriendly = true,
            AllowHostile = false
        };
        MechanoidBlinkMod.PermissionChecker = checker;

        (Game game, _, Faction playerFaction, Faction hostileFaction) = CreateTestGameEnvironment();
        try
        {
            ThingDef mechDef = RegisterOrGetThingDef(TestMechDefName, ThingCategory.Pawn, isMechanoid: true, typeof(Pawn));
            Pawn mechPawn = CreateSpawnedPawn(mechDef, hostileFaction);
            mechPawn.health = new Pawn_HealthTracker(mechPawn);

            // Initially hostile: no hediff
            MechanoidBlinkHediffManager.UpdateMechanoid(mechPawn);
            Assert.IsFalse(MechanoidBlinkHediffManager.HasHediff(mechPawn));

            // Mechanoid is hacked / captured: faction changes to PlayerColony
            AccessTools.Field(typeof(Thing), "factionInt")?.SetValue(mechPawn, playerFaction);
            Patch_Pawn_SetFaction.Postfix(mechPawn, playerFaction);

            Assert.IsTrue(MechanoidBlinkHediffManager.HasHediff(mechPawn), "Hacked mechanoid joining player faction must receive Hediff");
        }
        finally
        {
            Current.Game = null;
        }
    }

    /// <summary>
    /// Circumstance 3: Verifies that <see cref="Patch_MentalState"/> reassigns the Hediff
    /// when a friendly mechanoid goes berserk or recovers from a mental state.
    /// </summary>
    [TestMethod]
    public void Test_Circumstance3_MentalStateChange_UpdatesHediff()
    {
        ConfigurablePermissionChecker checker = new()
        {
            AllowFriendly = true,
            AllowHostile = false
        };
        MechanoidBlinkMod.PermissionChecker = checker;

        (Game game, _, Faction playerFaction, Faction hostileFaction) = CreateTestGameEnvironment();
        try
        {
            ThingDef mechDef = RegisterOrGetThingDef(TestMechDefName, ThingCategory.Pawn, isMechanoid: true, typeof(Pawn));
            Pawn mechPawn = CreateSpawnedPawn(mechDef, playerFaction);
            mechPawn.health = new Pawn_HealthTracker(mechPawn);

            MechanoidBlinkHediffManager.UpdateMechanoid(mechPawn);
            Assert.IsTrue(MechanoidBlinkHediffManager.HasHediff(mechPawn), "Precondition: friendly mech has hediff");

            // Mechanoid goes berserk -> turns hostile
            AccessTools.Field(typeof(Thing), "factionInt")?.SetValue(mechPawn, hostileFaction);
            Patch_MentalState.Postfix_TryStartMentalState(__result: true, ___pawn: mechPawn);

            Assert.IsFalse(MechanoidBlinkHediffManager.HasHediff(mechPawn), "Berserk mechanoid should lose Hediff when hostile is disabled");

            // Berserk ends -> returns to player faction
            AccessTools.Field(typeof(Thing), "factionInt")?.SetValue(mechPawn, playerFaction);
            Patch_MentalState.Postfix_ClearMentalStateDirect(___pawn: mechPawn);

            Assert.IsTrue(MechanoidBlinkHediffManager.HasHediff(mechPawn), "Recovered mechanoid should regain Hediff");
        }
        finally
        {
            Current.Game = null;
        }
    }

    /// <summary>
    /// Verifies that corpses, dead mechanoids, and non-mechanoid pawns are strictly excluded
    /// from receiving the Mechanoid Blink Hediff.
    /// </summary>
    [TestMethod]
    public void Test_CorpseAndNonMechanoid_StrictlyExcluded()
    {
        ConfigurablePermissionChecker checker = new()
        {
            AllowFriendly = true,
            AllowHostile = true
        };
        MechanoidBlinkMod.PermissionChecker = checker;

        (Game game, _, Faction playerFaction, _) = CreateTestGameEnvironment();
        try
        {
            ThingDef corpseDef = RegisterOrGetThingDef(TestCorpseDefName, ThingCategory.Item, isMechanoid: true, typeof(Corpse));
            ThingDef humanDef = RegisterOrGetThingDef(TestHumanDefName, ThingCategory.Pawn, isMechanoid: false, typeof(Pawn));
            ThingDef mechDef = RegisterOrGetThingDef(TestMechDefName, ThingCategory.Pawn, isMechanoid: true, typeof(Pawn));

            Pawn humanPawn = CreateSpawnedPawn(humanDef, playerFaction);
            humanPawn.health = new Pawn_HealthTracker(humanPawn);

            Pawn deadMech = CreateSpawnedPawn(mechDef, playerFaction);
            AccessTools.Field(typeof(Thing), "mapIndexOrState")?.SetValue(deadMech, (sbyte)-2);
            deadMech.health = new Pawn_HealthTracker(deadMech);
            AccessTools.Field(typeof(Pawn_HealthTracker), "healthState")?.SetValue(deadMech.health, PawnHealthState.Dead);
            Assert.IsTrue(deadMech.Dead, "Precondition: deadMech.Dead is true");
            Assert.IsTrue(deadMech.Destroyed, "Precondition: deadMech.Destroyed is true");

            MechanoidBlinkHediffManager.UpdateMechanoid(humanPawn);
            MechanoidBlinkHediffManager.UpdateMechanoid(deadMech);

            Assert.IsFalse(MechanoidBlinkHediffManager.HasHediff(humanPawn), "Humanlike pawns must not receive Mechanoid Blink Hediff");
            Assert.IsFalse(MechanoidBlinkHediffManager.HasHediff(deadMech), "Destroyed / dead mechanoids must not receive Mechanoid Blink Hediff");
        }
        finally
        {
            Current.Game = null;
        }
    }

    /// <summary>
    /// Verifies that <see cref="MechanoidBlinkInitializer.Initialize"/> is idempotent and does
    /// not duplicate Harmony patches when invoked multiple times.
    /// </summary>
    [TestMethod]
    public void Test_Initialize_CalledMultipleTimes_DoesNotDoublePatch()
    {
        RunInitializerForTests();
        RunInitializerForTests();

        MethodBase? spawnTarget = AccessTools.Method(typeof(Pawn), nameof(Pawn.SpawnSetup));
        Assert.IsNotNull(spawnTarget);

        HarmonyLib.Patches? spawnPatches = Harmony.GetPatchInfo(spawnTarget);
        Assert.IsNotNull(spawnPatches);

        int spawnPostfixCount = spawnPatches.Postfixes.Count(p => p.owner == MechanoidBlinkInitializer.HarmonyId);
        Assert.AreEqual(1, spawnPostfixCount, "Pawn.SpawnSetup should have exactly 1 MechanoidBlink postfix");
    }

    private static void ExecuteOnMockMainThread(Action action)
    {
        FieldInfo? mainThreadField = AccessTools.Field(typeof(UnityData), "mainThreadId");
        object? prev = mainThreadField?.GetValue(null);
        try
        {
            mainThreadField?.SetValue(null, Environment.CurrentManagedThreadId);
            action();
        }
        finally
        {
            mainThreadField?.SetValue(null, prev);
        }
    }

    private static void RunInitializerForTests()
    {
        RuntimeHelpers.RunClassConstructor(typeof(BaseContent).TypeHandle);
        FieldInfo? mainThreadField = AccessTools.Field(typeof(UnityData), "mainThreadId");
        object? previousMainThreadId = mainThreadField?.GetValue(null);
        try
        {
            mainThreadField?.SetValue(null, Environment.CurrentManagedThreadId);
            MechanoidBlinkInitializer.Initialize();
        }
        finally
        {
            mainThreadField?.SetValue(null, previousMainThreadId);
        }
    }

    private static AutoBlinkProfileDef EnsureProfileDefRegistered()
    {
        AutoBlinkProfileDef? profile = DefDatabase<AutoBlinkProfileDef>.GetNamedSilentFail("MechanoidBlink_Profile");
        if (profile == null)
        {
            profile = new AutoBlinkProfileDef
            {
                defName = "MechanoidBlink_Profile",
                label = "mechanoid auto blink",
                description = "Automatically teleports along the current walking path for mechanoids.",
                playerFactionOnly = false,
                showGizmo = true,
                iconPath = "UI/Commands/AutoBlink",
                beforeSound = DefDatabase<SoundDef>.GetNamedSilentFail("AutoBlink_Skip_Small"),
                afterSound = DefDatabase<SoundDef>.GetNamedSilentFail("AutoBlink_Skip_Exit_Small"),
                beforeEffect = DefDatabase<EffecterDef>.GetNamedSilentFail("Skip_Entry") ?? EffecterDefOf.Skip_Entry,
                afterEffect = DefDatabase<EffecterDef>.GetNamedSilentFail("Skip_ExitNoDelay") ?? EffecterDefOf.Skip_ExitNoDelay
            };
            DefDatabase<AutoBlinkProfileDef>.Add(profile);
        }

        FieldInfo? iconField = AccessTools.Field(typeof(AutoBlinkProfileDef), "icon");
        if (iconField != null && iconField.GetValue(profile) == null)
        {
            Texture2D stubTex = (Texture2D)RuntimeHelpers.GetUninitializedObject(typeof(Texture2D));
            iconField.SetValue(profile, stubTex);
        }

        return profile;
    }

    private static HediffDef EnsureHediffDefRegistered()
    {
        HediffDef? def = DefDatabase<HediffDef>.GetNamedSilentFail(MechanoidBlinkHediffManager.HediffDefName);
        if (def == null)
        {
            def = new HediffDef
            {
                defName = MechanoidBlinkHediffManager.HediffDefName,
                label = "Mechanoid Blink",
                description = "This mechanoid can teleport automatically.",
                hediffClass = typeof(HediffWithComps),
                defaultLabelColor = new Color(0.52f, 1.0f, 0.95f),
                isBad = false,
                initialSeverity = 1.0f,
                maxSeverity = 1.0f,
                comps = new List<HediffCompProperties>
                {
                    new HediffCompProperties_AutoBlinkSource
                    {
                        compClass = typeof(HediffComp_AutoBlinkSource),
                        profile = EnsureProfileDefRegistered()
                    }
                }
            };
            DefDatabase<HediffDef>.Add(def);
        }

        return def;
    }

    private static void EnsureSoundDefRegistered(string defName)
    {
        if (DefDatabase<SoundDef>.GetNamedSilentFail(defName) == null)
        {
            DefDatabase<SoundDef>.Add(new SoundDef { defName = defName });
        }
    }

    private static void EnsureEffecterDefRegistered(string defName)
    {
        if (DefDatabase<EffecterDef>.GetNamedSilentFail(defName) == null)
        {
            DefDatabase<EffecterDef>.Add(new EffecterDef { defName = defName });
        }
    }

    private static ThingDef RegisterOrGetThingDef(string defName, ThingCategory category, bool isMechanoid, Type thingClass)
    {
        ThingDef? existing = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
        if (existing != null)
        {
            return existing;
        }

        RaceProperties raceProps = new()
        {
            baseHealthScale = 1.0f
        };
        FieldInfo? fleshField = typeof(RaceProperties).GetField("fleshType", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        fleshField?.SetValue(raceProps, isMechanoid ? FleshTypeDefOf.Mechanoid : FleshTypeDefOf.Normal);

        BodyDef bodyDef = new() { defName = "Test_Mech_Body_" + defName };
        BodyPartRecord corePart = new() { def = new BodyPartDef { defName = "MechanicalThorax" }, body = bodyDef };
        bodyDef.corePart = corePart;
        raceProps.body = bodyDef;

        ThingDef def = new()
        {
            defName = defName,
            label = defName,
            category = category,
            race = raceProps,
            thingClass = thingClass,
            comps = new List<CompProperties>()
        };

        DefDatabase<ThingDef>.Add(def);
        return def;
    }

    private static (Game Game, object AutoBlinkComp, Faction PlayerFaction, Faction HostileFaction) CreateTestGameEnvironment()
    {
        Game game = (Game)RuntimeHelpers.GetUninitializedObject(typeof(Game));
        AccessTools.Field(typeof(Game), "components")?.SetValue(game, new List<GameComponent>());

        Map stubMap = (Map)RuntimeHelpers.GetUninitializedObject(typeof(Map));
        AccessTools.Field(typeof(Map), "components")?.SetValue(stubMap, new List<MapComponent>());
        MapPawns mapPawns = new(stubMap);
        AccessTools.Field(typeof(Map), "mapPawns")?.SetValue(stubMap, mapPawns);
        AccessTools.Field(typeof(Game), "maps")?.SetValue(game, new List<Map> { stubMap });

        TickManager tickManager = (TickManager)RuntimeHelpers.GetUninitializedObject(typeof(TickManager));
        AccessTools.Field(typeof(Game), "tickManager")?.SetValue(game, tickManager);

        UniqueIDsManager uniqueIDsManager = new();
        AccessTools.Field(typeof(Game), "uniqueIDsManager")?.SetValue(game, uniqueIDsManager);

        FactionManager factionManager = new();
        Faction playerFaction = (Faction)RuntimeHelpers.GetUninitializedObject(typeof(Faction));
        playerFaction.def = new FactionDef { defName = "PlayerColony_Test", isPlayer = true };
        playerFaction.Name = "Player Colony";

        Faction hostileFaction = (Faction)RuntimeHelpers.GetUninitializedObject(typeof(Faction));
        hostileFaction.def = new FactionDef { defName = "Mechanoid_Test", isPlayer = false, permanentEnemy = true };
        hostileFaction.Name = "Mechanoid Hive";

        AccessTools.Field(typeof(FactionManager), "ofPlayer")?.SetValue(factionManager, playerFaction);
        List<Faction>? allFactions = AccessTools.Field(typeof(FactionManager), "allFactions")?.GetValue(factionManager) as List<Faction>;
        allFactions?.Add(playerFaction);
        allFactions?.Add(hostileFaction);

        RimWorld.Planet.World world = (RimWorld.Planet.World)RuntimeHelpers.GetUninitializedObject(typeof(RimWorld.Planet.World));
        world.factionManager = factionManager;
        WorldPawns worldPawns = new();
        world.worldPawns = worldPawns;
        AccessTools.Field(typeof(Game), "worldInt")?.SetValue(game, world);

        Current.Game = game;

        Type gameCompType = AccessTools.TypeByName("AutoBlink.AutoBlinkGameComponent")!;
        object gameComp = Activator.CreateInstance(gameCompType, game)!;
        game.components.Add((GameComponent)gameComp);

        return (game, gameComp, playerFaction, hostileFaction);
    }

    private static Pawn CreateSpawnedPawn(ThingDef def, Faction faction)
    {
        PawnKindDef kindDef = new()
        {
            defName = "Test_Mech_Kind_" + def.defName,
            race = def,
            lifeStages = new List<PawnKindLifeStage>
            {
                new PawnKindLifeStage { bodyGraphicData = null }
            }
        };

        Pawn pawn = new()
        {
            def = def,
            kindDef = kindDef,
            pather = (Verse.AI.Pawn_PathFollower)RuntimeHelpers.GetUninitializedObject(typeof(Verse.AI.Pawn_PathFollower))
        };

        AccessTools.Field(typeof(Thing), "factionInt")?.SetValue(pawn, faction);
        // Set mapIndexOrState = 0 so pawn.Spawned evaluates to true without requiring a full Unity Map instance.
        AccessTools.Field(typeof(Thing), "mapIndexOrState")?.SetValue(pawn, (sbyte)0);

        return pawn;
    }
}
