using System;
using HarmonyLib;
using MechanoidBlink.Core;
using Verse;

namespace MechanoidBlink;

/// <summary>
/// Startup initializer executed after all Defs and mods are loaded.
/// Applies Harmony patches and synchronizes mechanoid blink Hediffs.
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

    static MechanoidBlinkInitializer()
    {
        Initialize();
    }

    /// <summary>
    /// Applies Harmony patches, initializes settings defaults, and synchronizes the Mechanoid Blink
    /// Hediff across any already-spawned mechanoids.
    /// </summary>
    public static void Initialize()
    {
        ApplyHarmonyPatches();
        InitializeSettings();
        SyncActiveMechanoids();
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

    private static void InitializeSettings()
    {
        try
        {
            if (MechanoidBlinkMod.Settings != null)
            {
                MechanoidBlinkMod.Settings.EnsureInitialized(MechanoidRegistry.AllMechanoidDefNames);
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[MechanoidBlink] Exception while initializing mechanoid settings: {ex}");
        }
    }

    private static void SyncActiveMechanoids()
    {
        try
        {
            MechanoidBlinkHediffManager.UpdateAllMechanoids();
        }
        catch (Exception ex)
        {
            Log.Error($"[MechanoidBlink] Exception while synchronizing active mechanoids: {ex}");
        }
    }
}
