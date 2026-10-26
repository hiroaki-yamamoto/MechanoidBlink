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
using UnityEngine;
using Verse;

namespace MechanoidBlink.Tests;

/// <summary>
/// End-to-end integration and regression tests verifying MechanoidBlink's startup cache
/// synchronization, controller retention across AutoBlink rebuilds, Harmony gizmo behavior,
/// and permission enforcement against the live <c>AutoBlink.dll</c> assembly.
/// </summary>
[TestClass]
public class AutoBlinkIntegrationTests
{
    private const string TestMechDefName = "Mech_IntegrationTest_Lifter";
    private const string TestCorpseDefName = "Corpse_Mech_IntegrationTest_Lifter";
    private const string TestHumanDefName = "Human_IntegrationTest";

    private sealed class ConfigurablePermissionChecker : IMechanoidBlinkPermissionChecker
    {
        public bool AllowMechanoids { get; set; } = true;

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

            return AllowMechanoids;
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

        EnsureSoundDefRegistered(MechanoidBlinkInjector.DefaultBeforeSoundDefName);
        EnsureSoundDefRegistered(MechanoidBlinkInjector.DefaultAfterSoundDefName);
        EnsureEffecterDefRegistered(MechanoidBlinkInjector.DefaultBeforeEffectDefName);
        EnsureEffecterDefRegistered(MechanoidBlinkInjector.DefaultAfterEffectDefName);

        EnsureProfileDefRegistered();
        MechanoidRegistry.ResetCache();
    }

    [TestMethod]
    public void Test_StartupOrder_AutoBlinkBeforeMechanoidBlink_UpdatesSourceDefCache()
    {
        ThingDef mechDef = RegisterOrGetThingDef(
            TestMechDefName,
            ThingCategory.Pawn,
            isMechanoid: true,
            typeof(Pawn));
        mechDef.comps?.RemoveAll(c => c is CompProperties_AutoBlinkSource);

        ThingDef corpseDef = RegisterOrGetThingDef(
            TestCorpseDefName,
            ThingCategory.Item,
            isMechanoid: true,
            typeof(Corpse));
        corpseDef.comps?.RemoveAll(c => c is CompProperties_AutoBlinkSource);

        ThingDef humanDef = RegisterOrGetThingDef(
            TestHumanDefName,
            ThingCategory.Pawn,
            isMechanoid: false,
            typeof(Pawn));
        humanDef.comps?.RemoveAll(c => c is CompProperties_AutoBlinkSource);

        MechanoidRegistry.ResetCache();

        // 1. Simulate AutoBlink.StartupValidation..cctor() running BEFORE MechanoidBlinkInitializer
        //    (due to <loadAfter><li>rabiosus.autoblink</li></loadAfter> in About.xml).
        Assert.IsTrue(AutoBlinkBridge.RebuildSourceDefCache(), "BlinkSourceDefCache.Rebuild() should succeed");
        Assert.IsFalse(
            AutoBlinkBridge.HasCachedThingSource(mechDef),
            "Before MechanoidBlinkInjector runs, BlinkSourceDefCache caches false for mechanoid ThingDefs");

        // 2. Now run MechanoidBlinkInjector.InjectAll(), simulating MechanoidBlinkInitializer.Initialize().
        int injected = MechanoidBlinkInjector.InjectAll();
        Assert.IsTrue(injected >= 1, "At least the test mechanoid ThingDef should be newly injected");

        // 3. Verify CompProperties_AutoBlinkSource was injected and BlinkSourceDefCache now returns true!
        CompProperties_AutoBlinkSource? sourceComp = mechDef.comps?
            .OfType<CompProperties_AutoBlinkSource>()
            .FirstOrDefault();
        Assert.IsNotNull(sourceComp, "Mechanoid ThingDef must contain CompProperties_AutoBlinkSource");
        Assert.IsNotNull(sourceComp.profile, "Injected source must reference a valid AutoBlinkProfileDef");
        Assert.IsFalse(sourceComp.profile.playerFactionOnly, "Mechanoid profile must allow hostile blinking when enabled");
        Assert.IsNotNull(sourceComp.profile.beforeSound, "beforeSound must be configured");
        Assert.AreEqual(MechanoidBlinkInjector.DefaultBeforeSoundDefName, sourceComp.profile.beforeSound.defName);
        Assert.IsNotNull(sourceComp.profile.afterSound, "afterSound must be configured");
        Assert.AreEqual(MechanoidBlinkInjector.DefaultAfterSoundDefName, sourceComp.profile.afterSound.defName);
        Assert.IsNotNull(sourceComp.profile.beforeEffect, "beforeEffect must be configured");
        Assert.AreEqual(MechanoidBlinkInjector.DefaultBeforeEffectDefName, sourceComp.profile.beforeEffect.defName);
        Assert.IsNotNull(sourceComp.profile.afterEffect, "afterEffect must be configured");
        Assert.AreEqual(MechanoidBlinkInjector.DefaultAfterEffectDefName, sourceComp.profile.afterEffect.defName);

        Assert.IsTrue(
            AutoBlinkBridge.HasCachedThingSource(mechDef),
            "BlinkSourceDefCache.HasSource(mechDef) MUST be true after MechanoidBlinkInjector.InjectAll()");
        Assert.IsFalse(
            AutoBlinkBridge.HasCachedThingSource(corpseDef),
            "Mechanoid corpse ThingDef must NOT be marked as having an AutoBlink source");
        Assert.IsFalse(
            AutoBlinkBridge.HasCachedThingSource(humanDef),
            "Non-mechanoid ThingDef must NOT be marked as having an AutoBlink source");
    }

    /// <summary>
    /// Verifies that <see cref="MechanoidBlinkInjector.EnsureProfileFx"/> configures the required
    /// departure sound (<c>AutoBlink_Skip_Small</c>), arrival sound (<c>AutoBlink_Skip_Exit_Small</c>),
    /// departure effect (<c>Skip_Entry</c>), and arrival effect (<c>Skip_ExitNoDelay</c>) on a profile
    /// where those fields were previously unassigned.
    /// </summary>
    [TestMethod]
    public void Test_EnsureProfileFx_PopulatesMissingSoundsAndEffects()
    {
        AutoBlinkProfileDef profile = new()
        {
            defName = "Test_Profile_Without_Fx",
            beforeSound = null,
            afterSound = null,
            beforeEffect = null,
            afterEffect = null
        };

        MechanoidBlinkInjector.EnsureProfileFx(profile);

        Assert.IsNotNull(profile.beforeSound, "beforeSound must be assigned by EnsureProfileFx");
        Assert.AreEqual(MechanoidBlinkInjector.DefaultBeforeSoundDefName, profile.beforeSound.defName);
        Assert.IsNotNull(profile.afterSound, "afterSound must be assigned by EnsureProfileFx");
        Assert.AreEqual(MechanoidBlinkInjector.DefaultAfterSoundDefName, profile.afterSound.defName);
        Assert.IsNotNull(profile.beforeEffect, "beforeEffect must be assigned by EnsureProfileFx");
        Assert.AreEqual(MechanoidBlinkInjector.DefaultBeforeEffectDefName, profile.beforeEffect.defName);
        Assert.IsNotNull(profile.afterEffect, "afterEffect must be assigned by EnsureProfileFx");
        Assert.AreEqual(MechanoidBlinkInjector.DefaultAfterEffectDefName, profile.afterEffect.defName);
    }

    /// <summary>
    /// Verifies that <see cref="MechanoidBlinkInjector.EnsureProfileFx"/> preserves custom sound
    /// and visual effect definitions when they are already populated on the profile.
    /// </summary>
    [TestMethod]
    public void Test_EnsureProfileFx_PreservesExistingCustomFx()
    {
        SoundDef customBeforeSound = new() { defName = "Custom_Before_Sound" };
        SoundDef customAfterSound = new() { defName = "Custom_After_Sound" };
        EffecterDef customBeforeEffect = new() { defName = "Custom_Before_Effect" };
        EffecterDef customAfterEffect = new() { defName = "Custom_After_Effect" };

        AutoBlinkProfileDef profile = new()
        {
            defName = "Test_Profile_With_Custom_Fx",
            beforeSound = customBeforeSound,
            afterSound = customAfterSound,
            beforeEffect = customBeforeEffect,
            afterEffect = customAfterEffect
        };

        MechanoidBlinkInjector.EnsureProfileFx(profile);

        Assert.AreSame(customBeforeSound, profile.beforeSound, "Existing beforeSound must be preserved");
        Assert.AreSame(customAfterSound, profile.afterSound, "Existing afterSound must be preserved");
        Assert.AreSame(customBeforeEffect, profile.beforeEffect, "Existing beforeEffect must be preserved");
        Assert.AreSame(customAfterEffect, profile.afterEffect, "Existing afterEffect must be preserved");
    }

    [TestMethod]
    public void Test_GameComponentRebuild_RetainsMechanoidBlinkController()
    {
        ThingDef mechDef = RegisterOrGetThingDef(
            TestMechDefName,
            ThingCategory.Pawn,
            isMechanoid: true,
            typeof(Pawn));
        MechanoidRegistry.ResetCache();

        // Simulate AutoBlink caching before MechanoidBlink injection
        AutoBlinkBridge.RebuildSourceDefCache();
        MechanoidBlinkInjector.InjectAll();

        (Game game, object gameComp, Faction playerFaction, _) = CreateTestGameEnvironment();
        try
        {
            Pawn mechPawn = CreateSpawnedPawn(mechDef, playerFaction);
            mechPawn.InitializeComps();

            Type gameCompType = gameComp.GetType();
            MethodInfo rebuildMethod = AccessTools.Method(gameCompType, "Rebuild", new[] { typeof(Pawn) });
            MethodInfo tryGetMethod = AccessTools.Method(gameCompType, "TryGet");

            // Rebuild #1: initial controller attachment (tick 0)
            rebuildMethod.Invoke(gameComp, new object[] { mechPawn });

            object?[] args1 = { mechPawn, null };
            bool foundAfterFirstRebuild = (bool)tryGetMethod.Invoke(gameComp, args1)!;
            Assert.IsTrue(foundAfterFirstRebuild, "BlinkController should be attached on initial Rebuild");
            Assert.IsNotNull(args1[1]);

            // Rebuild #2: simulates AutoBlinkMapComponent.MapComponentTick() running Rebuild(pawn) on tick 1
            rebuildMethod.Invoke(gameComp, new object[] { mechPawn });

            object?[] args2 = { mechPawn, null };
            bool foundAfterSecondRebuild = (bool)tryGetMethod.Invoke(gameComp, args2)!;
            Assert.IsTrue(
                foundAfterSecondRebuild,
                "BlinkController MUST survive subsequent AutoBlinkGameComponent.Rebuild(pawn) ticks");

            object controller = args2[1]!;
            Assert.AreSame(mechPawn, AutoBlinkBridge.GetPawn(controller));
            Assert.IsNotNull(AutoBlinkBridge.GetProfile(controller), "Active profile should be resolved on controller");
        }
        finally
        {
            Current.Game = null;
        }
    }

    [TestMethod]
    public void Test_BlinkGizmosAppend_AllowedUnoverseenPlayerMechanoid_YieldsBlinkCommandOnce()
    {
        ThingDef mechDef = RegisterOrGetThingDef(
            TestMechDefName,
            ThingCategory.Pawn,
            isMechanoid: true,
            typeof(Pawn));
        MechanoidRegistry.ResetCache();

        ConfigurablePermissionChecker checker = new() { AllowMechanoids = true };
        MechanoidBlinkMod.PermissionChecker = checker;
        RunInitializerForTests();

        (_, object gameComp, Faction playerFaction, _) = CreateTestGameEnvironment();
        try
        {
            // Create a player-faction mechanoid WITHOUT CompOverseerSubject / Mechanitor binding.
            Pawn mechPawn = CreateSpawnedPawn(mechDef, playerFaction);
            mechPawn.InitializeComps();
            Assert.IsNull(mechPawn.GetComp<CompOverseerSubject>(), "Precondition: mechanoid has no CompOverseerSubject / Mechanitor binding");

            object controller = AttachAndGetController(gameComp, mechPawn);
            Command_Action upstreamGizmo = new() { defaultLabel = "ExistingCommand" };

            List<Gizmo> result = InvokeBlinkGizmosAppend(new Gizmo[] { upstreamGizmo }, controller);

            Assert.AreEqual(2, result.Count, "Expected upstream gizmo + 1 BlinkCommand gizmo");
            Assert.AreSame(upstreamGizmo, result[0]);

            Gizmo? expectedCommand = AutoBlinkBridge.GetCommand(controller);
            Assert.IsNotNull(expectedCommand);
            Assert.AreSame(expectedCommand, result[1], "Appended gizmo must be the controller's BlinkCommand");
        }
        finally
        {
            Current.Game = null;
        }
    }

    [TestMethod]
    public void Test_BlinkGizmosAppend_DisallowedPlayerMechanoid_SuppressesBlinkCommand()
    {
        ThingDef mechDef = RegisterOrGetThingDef(
            TestMechDefName,
            ThingCategory.Pawn,
            isMechanoid: true,
            typeof(Pawn));
        MechanoidRegistry.ResetCache();

        ConfigurablePermissionChecker checker = new() { AllowMechanoids = false };
        MechanoidBlinkMod.PermissionChecker = checker;
        RunInitializerForTests();

        (_, object gameComp, Faction playerFaction, _) = CreateTestGameEnvironment();
        try
        {
            Pawn mechPawn = CreateSpawnedPawn(mechDef, playerFaction);
            mechPawn.InitializeComps();

            object controller = AttachAndGetController(gameComp, mechPawn);
            Command_Action upstreamGizmo = new() { defaultLabel = "ExistingCommand" };

            List<Gizmo> result = InvokeBlinkGizmosAppend(new Gizmo[] { upstreamGizmo }, controller);

            Assert.AreEqual(1, result.Count, "Disallowed mechanoid must NOT receive BlinkCommand gizmo");
            Assert.AreSame(upstreamGizmo, result[0]);
        }
        finally
        {
            Current.Game = null;
        }
    }

    [TestMethod]
    public void Test_BlinkGizmosAppend_HostileMechanoid_DoesNotYieldBlinkCommand()
    {
        ThingDef mechDef = RegisterOrGetThingDef(
            TestMechDefName,
            ThingCategory.Pawn,
            isMechanoid: true,
            typeof(Pawn));
        MechanoidRegistry.ResetCache();

        // Even when hostile mechanoids ARE permitted to auto-blink, selecting an enemy mechanoid
        // must never expose a player-controlled manual blink command gizmo.
        ConfigurablePermissionChecker checker = new() { AllowMechanoids = true };
        MechanoidBlinkMod.PermissionChecker = checker;
        RunInitializerForTests();

        (_, object gameComp, _, Faction hostileFaction) = CreateTestGameEnvironment();
        try
        {
            Pawn hostileMech = CreateSpawnedPawn(mechDef, hostileFaction);
            hostileMech.InitializeComps();

            object controller = AttachAndGetController(gameComp, hostileMech);
            Command_Action upstreamGizmo = new() { defaultLabel = "InspectOnly" };

            List<Gizmo> result = InvokeBlinkGizmosAppend(new Gizmo[] { upstreamGizmo }, controller);

            Assert.AreEqual(1, result.Count, "Hostile/non-player mechanoid must NOT show BlinkCommand gizmo");
            Assert.AreSame(upstreamGizmo, result[0]);
        }
        finally
        {
            Current.Game = null;
        }
    }

    [TestMethod]
    public void Test_BlinkGizmosAppend_WhenCommandAlreadyPresent_DoesNotDuplicate()
    {
        ThingDef mechDef = RegisterOrGetThingDef(
            TestMechDefName,
            ThingCategory.Pawn,
            isMechanoid: true,
            typeof(Pawn));
        MechanoidRegistry.ResetCache();

        ConfigurablePermissionChecker checker = new() { AllowMechanoids = true };
        MechanoidBlinkMod.PermissionChecker = checker;
        RunInitializerForTests();

        (_, object gameComp, Faction playerFaction, _) = CreateTestGameEnvironment();
        try
        {
            Pawn mechPawn = CreateSpawnedPawn(mechDef, playerFaction);
            mechPawn.InitializeComps();

            object controller = AttachAndGetController(gameComp, mechPawn);
            Gizmo command = AutoBlinkBridge.GetCommand(controller)!;
            Assert.IsNotNull(command);

            List<Gizmo> result = InvokeBlinkGizmosAppend(new[] { command }, controller);

            int commandOccurrences = result.Count(g => ReferenceEquals(g, command));
            Assert.AreEqual(1, commandOccurrences, "BlinkCommand must never be yielded more than once");
        }
        finally
        {
            Current.Game = null;
        }
    }

    [TestMethod]
    public void Test_CanUsePatch_AndPolicy_EnforcePermissionChecker()
    {
        ThingDef mechDef = RegisterOrGetThingDef(
            TestMechDefName,
            ThingCategory.Pawn,
            isMechanoid: true,
            typeof(Pawn));
        MechanoidRegistry.ResetCache();

        ConfigurablePermissionChecker checker = new() { AllowMechanoids = false };
        MechanoidBlinkMod.PermissionChecker = checker;
        RunInitializerForTests();

        (_, object gameComp, Faction playerFaction, _) = CreateTestGameEnvironment();
        try
        {
            Pawn mechPawn = CreateSpawnedPawn(mechDef, playerFaction);
            mechPawn.InitializeComps();

            object controller = AttachAndGetController(gameComp, mechPawn);
            AutoBlinkProfileDef profile = AutoBlinkBridge.GetProfile(controller)!;
            Assert.IsNotNull(profile);

            // 1. Disallowed mechanoid: CanUse is short-circuited to false by Patch_BlinkController_CanUse
            MethodInfo canUseMethod = AccessTools.Method(controller.GetType(), "CanUse");
            bool canUseWhenDisallowed = (bool)canUseMethod.Invoke(controller, new object[] { profile, false, false })!;
            Assert.IsFalse(canUseWhenDisallowed, "CanUse must return false when PermissionChecker denies blinking");

            MechanoidBlinkPolicy policy = new(checker);
            BlinkContext context = CreateBlinkContext(mechPawn, profile);
            Assert.IsTrue(policy.BeforePlan(ref context).Denied, "Policy must deny planning when disallowed");

            // 2. Allowed mechanoid: Prefix allows execution and Policy allows planning
            checker.AllowMechanoids = true;
            bool prefixResult = true;
            bool runOriginal = Patch_BlinkController_CanUse.Prefix(controller, ref prefixResult);
            Assert.IsTrue(runOriginal, "Patch_BlinkController_CanUse.Prefix must allow original CanUse when permitted");
            Assert.IsFalse(policy.BeforePlan(ref context).Denied, "Policy must allow planning when permitted");
        }
        finally
        {
            Current.Game = null;
        }
    }

    [TestMethod]
    public void Test_Initialize_CalledMultipleTimes_DoesNotDoublePatch()
    {
        ConfigurablePermissionChecker checker = new() { AllowMechanoids = true };
        MechanoidBlinkMod.PermissionChecker = checker;

        RunInitializerForTests();
        RunInitializerForTests();

        MethodBase? canUseTarget = Patch_BlinkController_CanUse.TargetMethod();
        MethodBase? gizmoTarget = Patch_BlinkGizmos_Append.TargetMethod();
        Assert.IsNotNull(canUseTarget);
        Assert.IsNotNull(gizmoTarget);

        HarmonyLib.Patches? canUsePatches = Harmony.GetPatchInfo(canUseTarget);
        HarmonyLib.Patches? gizmoPatches = Harmony.GetPatchInfo(gizmoTarget);
        Assert.IsNotNull(canUsePatches);
        Assert.IsNotNull(gizmoPatches);

        int canUsePrefixCount = canUsePatches.Prefixes.Count(p => p.owner == MechanoidBlinkInitializer.HarmonyId);
        int gizmoPrefixCount = gizmoPatches.Prefixes.Count(p => p.owner == MechanoidBlinkInitializer.HarmonyId);

        Assert.AreEqual(1, canUsePrefixCount, "CanUse should have exactly 1 MechanoidBlink prefix");
        Assert.AreEqual(1, gizmoPrefixCount, "BlinkGizmos.Append should have exactly 1 MechanoidBlink prefix");
    }

    private static void RunInitializerForTests()
    {
        // Ensure texture-initializing static constructors execute while IsInMainThread is false
        // so headless UnityStubs Texture2D native constructors are safely bypassed.
        RuntimeHelpers.RunClassConstructor(typeof(BaseContent).TypeHandle);
        Type? blinkCommandType = AccessTools.TypeByName("AutoBlink.BlinkCommand");
        if (blinkCommandType != null)
        {
            RuntimeHelpers.RunClassConstructor(blinkCommandType.TypeHandle);
        }

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
        AutoBlinkProfileDef? profile = DefDatabase<AutoBlinkProfileDef>.GetNamedSilentFail(MechanoidBlinkInjector.ProfileDefName);
        if (profile == null)
        {
            profile = new AutoBlinkProfileDef
            {
                defName = MechanoidBlinkInjector.ProfileDefName,
                label = "mechanoid auto blink",
                description = "Automatically teleports along the current walking path for mechanoids.",
                playerFactionOnly = false,
                showGizmo = true,
                iconPath = "UI/Commands/AutoBlink"
            };
            DefDatabase<AutoBlinkProfileDef>.Add(profile);
        }

        // Pre-populate private icon field with a stub Texture2D so headless tests do not query Unity ContentFinder.
        FieldInfo? iconField = AccessTools.Field(typeof(AutoBlinkProfileDef), "icon");
        if (iconField != null && iconField.GetValue(profile) == null)
        {
            Texture2D stubTex = (Texture2D)RuntimeHelpers.GetUninitializedObject(typeof(Texture2D));
            iconField.SetValue(profile, stubTex);
        }

        MechanoidBlinkInjector.EnsureProfileFx(profile);

        return profile;
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

        RaceProperties raceProps = new();
        FieldInfo? fleshField = typeof(RaceProperties).GetField("fleshType", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        fleshField?.SetValue(raceProps, isMechanoid ? FleshTypeDefOf.Mechanoid : FleshTypeDefOf.Normal);

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
        AccessTools.Field(typeof(Game), "maps")?.SetValue(game, new List<Map> { stubMap });

        TickManager tickManager = (TickManager)RuntimeHelpers.GetUninitializedObject(typeof(TickManager));
        AccessTools.Field(typeof(Game), "tickManager")?.SetValue(game, tickManager);

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
        AccessTools.Field(typeof(Game), "worldInt")?.SetValue(game, world);

        Current.Game = game;

        Type gameCompType = AccessTools.TypeByName("AutoBlink.AutoBlinkGameComponent")!;
        object gameComp = Activator.CreateInstance(gameCompType, game)!;
        game.components.Add((GameComponent)gameComp);

        return (game, gameComp, playerFaction, hostileFaction);
    }

    private static Pawn CreateSpawnedPawn(ThingDef def, Faction faction)
    {
        Pawn pawn = new()
        {
            def = def,
            pather = (Verse.AI.Pawn_PathFollower)RuntimeHelpers.GetUninitializedObject(typeof(Verse.AI.Pawn_PathFollower))
        };

        AccessTools.Field(typeof(Thing), "factionInt")?.SetValue(pawn, faction);
        // Set mapIndexOrState = 0 so pawn.Spawned evaluates to true without requiring a full Unity Map instance.
        AccessTools.Field(typeof(Thing), "mapIndexOrState")?.SetValue(pawn, (sbyte)0);

        return pawn;
    }

    private static object AttachAndGetController(object gameComp, Pawn pawn)
    {
        Type gameCompType = gameComp.GetType();
        MethodInfo rebuildMethod = AccessTools.Method(gameCompType, "Rebuild", new[] { typeof(Pawn) });
        MethodInfo tryGetMethod = AccessTools.Method(gameCompType, "TryGet");

        rebuildMethod.Invoke(gameComp, new object[] { pawn });

        object?[] args = { pawn, null };
        bool found = (bool)tryGetMethod.Invoke(gameComp, args)!;
        Assert.IsTrue(found, "Expected AutoBlinkGameComponent to attach a BlinkController to the mechanoid pawn");
        return args[1]!;
    }

    private static List<Gizmo> InvokeBlinkGizmosAppend(IEnumerable<Gizmo> original, object controller)
    {
        Type blinkGizmosType = AccessTools.TypeByName("AutoBlink.BlinkGizmos")!;
        MethodInfo appendMethod = AccessTools.Method(blinkGizmosType, "Append");
        IEnumerable<Gizmo> enumerable = (IEnumerable<Gizmo>)appendMethod.Invoke(null, new object[] { original, controller })!;
        return enumerable.ToList();
    }

    private static BlinkContext CreateBlinkContext(Pawn pawn, AutoBlinkProfileDef profile)
    {
        ConstructorInfo? ctor = typeof(BlinkContext)
            .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault();
        Assert.IsNotNull(ctor, "BlinkContext constructor must exist");

        ParameterInfo[] parameters = ctor.GetParameters();
        object?[] args = new object?[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            Type pType = parameters[i].ParameterType;
            if (pType == typeof(Pawn))
            {
                args[i] = pawn;
            }
            else if (pType == typeof(AutoBlinkProfileDef))
            {
                args[i] = profile;
            }
            else if (pType.IsValueType)
            {
                args[i] = Activator.CreateInstance(pType);
            }
            else
            {
                args[i] = null;
            }
        }

        return (BlinkContext)ctor.Invoke(args);
    }
}
