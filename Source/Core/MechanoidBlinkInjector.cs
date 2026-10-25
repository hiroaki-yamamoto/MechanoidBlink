using System;
using System.Collections.Generic;
using AutoBlink;
using RimWorld;
using Verse;

namespace MechanoidBlink.Core;

/// <summary>
/// Responsible for injecting AutoBlink components into all mechanoid ThingDefs
/// and keeping <c>AutoBlink.BlinkSourceDefCache</c> synchronized.
/// </summary>
/// <remarks>
/// Because <c>About.xml</c> loads this mod after <c>rabiosus.autoblink</c>, <c>AutoBlink</c>
/// populates <c>BlinkSourceDefCache</c> before <see cref="InjectAll"/> executes. Updating the
/// cache in O(1) per injected <see cref="ThingDef"/> ensures <c>AutoBlinkGameComponent.Rebuild</c>
/// retains each mechanoid's <c>BlinkController</c>.
/// </remarks>
/// <example>
/// <code>
/// int newlyInjected = MechanoidBlinkInjector.InjectAll();
/// </code>
/// </example>
public static class MechanoidBlinkInjector
{
    public const string ProfileDefName = "MechanoidBlink_Profile";
    public const string DefaultBeforeSoundDefName = "AutoBlink_Skip_Small";
    public const string DefaultAfterSoundDefName = "AutoBlink_Skip_Exit_Small";
    public const string DefaultBeforeEffectDefName = "Skip_Entry";
    public const string DefaultAfterEffectDefName = "Skip_ExitNoDelay";

    /// <summary>
    /// Injects <see cref="CompProperties_AutoBlinkSource"/> into all mechanoids registered in the Mechanoid framework
    /// and synchronizes <c>AutoBlink.BlinkSourceDefCache</c> so active controllers are retained on map rebuilds.
    /// </summary>
    /// <returns>Number of mechanoid defs newly patched.</returns>
    public static int InjectAll()
    {
        AutoBlinkProfileDef? profile = ResolveProfileDef();
        if (profile == null)
        {
            Log.Error("[MechanoidBlink] Could not resolve an AutoBlinkProfileDef. AutoBlink cannot be injected.");
            return 0;
        }

        int count = 0;
        foreach (ThingDef def in MechanoidRegistry.AllMechanoids)
        {
            if (InjectComp(def, profile))
            {
                count++;
            }
        }

        // Fallback to a full cache rebuild only if direct O(1) dictionary updates were unavailable.
        if (!AutoBlinkBridge.CanUpdateThingSourceCacheDirectly)
        {
            AutoBlinkBridge.RebuildSourceDefCache();
        }

        AutoBlinkBridge.RebuildActiveMapsIfPresent();

        Log.Message($"[MechanoidBlink] Injected AutoBlink into {count} mechanoid definitions (Profile: {profile.defName}).");
        return count;
    }

    /// <summary>
    /// Ensures that the specified <see cref="AutoBlinkProfileDef"/> has valid departure/arrival sounds
    /// and visual effect definitions assigned.
    /// </summary>
    /// <remarks>
    /// Executes in O(1) time and O(1) space via DefDatabase O(1) dictionary lookups.
    /// Existing non-null configurations on <paramref name="profile"/> are preserved.
    /// </remarks>
    /// <example>
    /// <code>
    /// MechanoidBlinkInjector.EnsureProfileFx(profile);
    /// Assert.IsNotNull(profile.beforeSound);
    /// Assert.IsNotNull(profile.afterSound);
    /// Assert.IsNotNull(profile.beforeEffect);
    /// Assert.IsNotNull(profile.afterEffect);
    /// </code>
    /// </example>
    /// <param name="profile">The target <see cref="AutoBlinkProfileDef"/> to verify and populate.</param>
    public static void EnsureProfileFx(AutoBlinkProfileDef? profile)
    {
        if (profile == null) return;

        profile.beforeSound ??= DefDatabase<SoundDef>.GetNamedSilentFail(DefaultBeforeSoundDefName);
        profile.afterSound ??= DefDatabase<SoundDef>.GetNamedSilentFail(DefaultAfterSoundDefName);
        profile.beforeEffect ??= DefDatabase<EffecterDef>.GetNamedSilentFail(DefaultBeforeEffectDefName)
            ?? EffecterDefOf.Skip_Entry;
        profile.afterEffect ??= DefDatabase<EffecterDef>.GetNamedSilentFail(DefaultAfterEffectDefName)
            ?? EffecterDefOf.Skip_ExitNoDelay;
    }

    /// <summary>
    /// Injects <see cref="CompProperties_AutoBlinkSource"/> into a specific <see cref="ThingDef"/> if not already present,
    /// and marks the definition as an active AutoBlink source in <c>AutoBlink.BlinkSourceDefCache</c>.
    /// Excludes non-mechanoids and corpses.
    /// </summary>
    /// <param name="def">The target <see cref="ThingDef"/> to inspect and patch.</param>
    /// <param name="profile">The <see cref="AutoBlinkProfileDef"/> to attach to the injected component.</param>
    /// <returns><see langword="true"/> if a new <see cref="CompProperties_AutoBlinkSource"/> was added; otherwise, <see langword="false"/>.</returns>
    public static bool InjectComp(ThingDef def, AutoBlinkProfileDef profile)
    {
        if (def == null || profile == null || !MechanoidRegistry.IsMechanoidDef(def)) return false;

        if (def.comps == null)
        {
            def.comps = new List<CompProperties>();
        }

        EnsureProfileFx(profile);

        // Check if CompProperties_AutoBlinkSource already exists
        CompProperties_AutoBlinkSource? existingComp = def.comps.Find(c => c is CompProperties_AutoBlinkSource) as CompProperties_AutoBlinkSource;
        if (existingComp != null)
        {
            // If already present, ensure its profile allows hostile blinking and has FX configured
            if (existingComp.profile != null)
            {
                if (existingComp.profile.playerFactionOnly)
                {
                    existingComp.profile.playerFactionOnly = false;
                }
                EnsureProfileFx(existingComp.profile);
            }

            AutoBlinkBridge.MarkThingDefHasSource(def, hasSource: true);
            return false;
        }

        CompProperties_AutoBlinkSource newComp = new()
        {
            compClass = typeof(CompAutoBlinkSource),
            profile = profile
        };

        def.comps.Add(newComp);
        AutoBlinkBridge.MarkThingDefHasSource(def, hasSource: true);
        return true;
    }

    private static AutoBlinkProfileDef? ResolveProfileDef()
    {
        var customProfile = DefDatabase<AutoBlinkProfileDef>.GetNamedSilentFail(ProfileDefName);
        if (customProfile != null)
        {
            customProfile.playerFactionOnly = false;
            EnsureProfileFx(customProfile);
            return customProfile;
        }

        // Fallback to AutoBlink_Standard or first available profile
        var fallback = DefDatabase<AutoBlinkProfileDef>.GetNamedSilentFail("AutoBlink_Standard")
            ?? DefDatabase<AutoBlinkProfileDef>.AllDefsListForReading.Find(p => p != null);

        if (fallback != null)
        {
            fallback.playerFactionOnly = false;
            EnsureProfileFx(fallback);
        }

        return fallback;
    }
}

