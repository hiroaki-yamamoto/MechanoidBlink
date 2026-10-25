using System;
using AutoBlink;
using HarmonyLib;
using MechanoidBlink.Core;
using Verse;

namespace MechanoidBlink;

/// <summary>
/// Startup initializer executed after all Defs and mods are loaded.
/// Applies Harmony patches, injects AutoBlink into mechanoid ThingDefs,
/// and registers the AutoBlink policy.
/// </summary>
/// <remarks>
/// Initialization stages are isolated so a failure in one stage logs an error without blocking
/// the remaining stages, and repeated calls to <see cref="Initialize"/> are idempotent.
/// </remarks>
/// <example>
/// <code>
/// MechanoidBlinkInitializer.Initialize();
/// </code>
/// </example>
[StaticConstructorOnStartup]
public static class MechanoidBlinkInitializer
{
    public const string HarmonyId = "mechanoidblink.patch";
    private static IDisposable? policyRegistration;

    static MechanoidBlinkInitializer()
    {
        Initialize();
    }

    /// <summary>
    /// Applies Harmony patches, initializes settings defaults, injects AutoBlink components
    /// into mechanoid definitions (synchronizing <c>BlinkSourceDefCache</c>), and registers
    /// the Mechanoid Blink policy with <see cref="AutoBlinkApi"/>.
    /// </summary>
    public static void Initialize()
    {
        ApplyHarmonyPatches();
        InjectMechanoidDefinitions();
        RegisterAutoBlinkPolicy();
    }

    private static void ApplyHarmonyPatches()
    {
        try
        {
            if (Harmony.HasAnyPatches(HarmonyId))
            {
                return;
            }

            Harmony harmony = new(HarmonyId);
            harmony.PatchAll(typeof(MechanoidBlinkInitializer).Assembly);
            Log.Message("[MechanoidBlink] Harmony patches applied successfully.");
        }
        catch (Exception ex)
        {
            Log.Error($"[MechanoidBlink] Exception while applying Harmony patches: {ex}");
        }
    }

    private static void InjectMechanoidDefinitions()
    {
        try
        {
            if (MechanoidBlinkMod.Settings != null)
            {
                MechanoidBlinkMod.Settings.EnsureInitialized(MechanoidRegistry.AllMechanoidDefNames);
            }

            _ = MechanoidBlinkInjector.InjectAll();
        }
        catch (Exception ex)
        {
            Log.Error($"[MechanoidBlink] Exception while injecting mechanoid AutoBlink definitions: {ex}");
        }
    }

    private static void RegisterAutoBlinkPolicy()
    {
        try
        {
            if (MechanoidBlinkMod.PermissionChecker == null)
            {
                return;
            }

            policyRegistration?.Dispose();
            policyRegistration = AutoBlinkApi.RegisterPolicy(
                "MechanoidBlink",
                new MechanoidBlinkPolicy(MechanoidBlinkMod.PermissionChecker),
                priority: 1000);
            Log.Message("[MechanoidBlink] AutoBlink policy registered successfully.");
        }
        catch (Exception ex)
        {
            Log.Error($"[MechanoidBlink] Exception while registering AutoBlink policy: {ex}");
        }
    }
}

