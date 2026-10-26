using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace MechanoidBlink.Core;

/// <summary>
/// Default implementation of <see cref="IMechanoidBlinkHediffManager"/> using RimWorld engine primitives.
/// </summary>
public class RimWorldMechanoidBlinkHediffManager : IMechanoidBlinkHediffManager
{
    private HediffDef? cachedHediffDef;

    /// <summary>
    /// Resolves the <see cref="HediffDef"/> registered for Mechanoid Blink in O(1) time.
    /// </summary>
    public virtual HediffDef? GetHediffDef()
    {
        if (cachedHediffDef != null)
        {
            return cachedHediffDef;
        }

        cachedHediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(MechanoidBlinkHediffManager.HediffDefName)
            ?? DefDatabase<HediffDef>.GetNamedSilentFail(MechanoidBlinkHediffManager.FallbackHediffDefName);

        return cachedHediffDef;
    }

    /// <summary>
    /// Resets the cached HediffDef reference.
    /// </summary>
    public virtual void ResetCache()
    {
        cachedHediffDef = null;
    }

    /// <summary>
    /// Determines whether the specified <paramref name="pawn"/> currently possesses the Mechanoid Blink Hediff.
    /// Executes in O(H) time where H is the count of hediffs on the pawn.
    /// </summary>
    public virtual bool HasHediff(Pawn? pawn)
    {
        if (pawn?.health?.hediffSet == null)
        {
            return false;
        }

        HediffDef? def = GetHediffDef();
        if (def == null)
        {
            return false;
        }

        return pawn.health.hediffSet.GetFirstHediffOfDef(def) != null;
    }

    /// <summary>
    /// Applies the Mechanoid Blink Hediff to the specified <paramref name="pawn"/> if not already present.
    /// </summary>
    public virtual bool ApplyHediff(Pawn? pawn)
    {
        if (pawn?.health == null || pawn.Dead || pawn.Destroyed)
        {
            return false;
        }

        if (HasHediff(pawn))
        {
            return false;
        }

        HediffDef? def = GetHediffDef();
        if (def == null)
        {
            Log.Error("[MechanoidBlink] Cannot apply Mechanoid Blink Hediff: HediffDef not found.");
            return false;
        }

        Hediff hediff = HediffMaker.MakeHediff(def, pawn, null);
        hediff.Severity = 1.0f;
        pawn.health.AddHediff(hediff);
        return true;
    }

    /// <summary>
    /// Removes the Mechanoid Blink Hediff from the specified <paramref name="pawn"/> if present in a single pass.
    /// Executes in O(H) time without redundant presence checks.
    /// </summary>
    public virtual bool RemoveHediff(Pawn? pawn)
    {
        if (pawn?.health?.hediffSet == null)
        {
            return false;
        }

        HediffDef? def = GetHediffDef();
        if (def == null)
        {
            return false;
        }

        Hediff? hediff = pawn.health.hediffSet.GetFirstHediffOfDef(def);
        if (hediff != null)
        {
            pawn.health.RemoveHediff(hediff);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Evaluates whether the given <paramref name="pawn"/> should have the Mechanoid Blink Hediff,
    /// applying or removing it accordingly.
    /// Eliminates redundant collection scanning by invoking <see cref="RemoveHediff"/> directly in 1 pass.
    /// </summary>
    public virtual void UpdateMechanoid(Pawn? pawn)
    {
        if (pawn == null || pawn.def == null)
        {
            return;
        }

        // If the pawn is dead, destroyed, or not a recognized mechanoid def, strip the hediff in 1 pass.
        if (pawn.Dead || pawn.Destroyed || (pawn.RaceProps != null && !pawn.RaceProps.IsMechanoid) || !MechanoidRegistry.IsMechanoidDef(pawn.def))
        {
            RemoveHediff(pawn);
            return;
        }

        IMechanoidBlinkPermissionChecker? checker = MechanoidBlinkMod.PermissionChecker;
        bool isAllowed = checker != null && checker.IsBlinkAllowed(pawn);

        if (isAllowed)
        {
            ApplyHediff(pawn);
        }
        else
        {
            RemoveHediff(pawn);
        }
    }

    /// <summary>
    /// Batch-updates all mechanoids across all loaded maps and living world pawns in O(N) time and O(1) space.
    /// </summary>
    public virtual void UpdateAllMechanoids()
    {
        if (Current.Game == null)
        {
            return;
        }

        List<Map>? maps = Current.Game.Maps;
        if (maps != null)
        {
            for (int i = 0; i < maps.Count; i++)
            {
                Map map = maps[i];
                if (map?.mapPawns?.AllPawnsSpawned == null)
                {
                    continue;
                }

                IReadOnlyList<Pawn>? pawns = map.mapPawns.AllPawnsSpawned;
                if (pawns != null)
                {
                    for (int j = 0; j < pawns.Count; j++)
                    {
                        Pawn pawn = pawns[j];
                        if (pawn?.RaceProps != null && pawn.RaceProps.IsMechanoid)
                        {
                            UpdateMechanoid(pawn);
                        }
                    }
                }
            }
        }

        WorldPawns? worldPawns = Current.Game.World?.worldPawns;
        if (worldPawns != null)
        {
            try
            {
                IEnumerable<Pawn>? allWorldPawns = worldPawns.AllPawnsAlive;
                if (allWorldPawns != null)
                {
                    foreach (Pawn pawn in allWorldPawns)
                    {
                        if (pawn?.RaceProps != null && pawn.RaceProps.IsMechanoid)
                        {
                            UpdateMechanoid(pawn);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[MechanoidBlink] Error synchronizing world mechanoids: {ex.Message}");
            }
        }
    }
}

/// <summary>
/// Manages the application, removal, and synchronization of the Mechanoid Blink Hediff
/// on mechanoid pawns according to mod settings and game events.
/// Static facade delegating to an injectable <see cref="IMechanoidBlinkHediffManager"/> instance.
/// </summary>
public static class MechanoidBlinkHediffManager
{
    /// <summary>
    /// The primary defName of the Mechanoid Blink Hediff.
    /// </summary>
    public const string HediffDefName = "MechanoidBlink_Hediff";

    /// <summary>
    /// Fallback defName for the Mechanoid Blink Hediff.
    /// </summary>
    public const string FallbackHediffDefName = "MechanoidBlink";

    private static IMechanoidBlinkHediffManager instance = new RimWorldMechanoidBlinkHediffManager();

    /// <summary>
    /// Gets or sets the active manager implementation.
    /// Allows mocking and dependency injection during unit testing.
    /// </summary>
    public static IMechanoidBlinkHediffManager Instance
    {
        get => instance;
        set => instance = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Resolves the <see cref="HediffDef"/> registered for Mechanoid Blink in O(1) time.
    /// </summary>
    public static HediffDef? GetHediffDef() => Instance.GetHediffDef();

    /// <summary>
    /// Resets the cached HediffDef reference.
    /// </summary>
    public static void ResetCache() => Instance.ResetCache();

    /// <summary>
    /// Determines whether the specified <paramref name="pawn"/> currently possesses the Mechanoid Blink Hediff.
    /// </summary>
    public static bool HasHediff(Pawn? pawn) => Instance.HasHediff(pawn);

    /// <summary>
    /// Applies the Mechanoid Blink Hediff to the specified <paramref name="pawn"/> if not already present.
    /// </summary>
    public static bool ApplyHediff(Pawn? pawn) => Instance.ApplyHediff(pawn);

    /// <summary>
    /// Removes the Mechanoid Blink Hediff from the specified <paramref name="pawn"/> if present in a single pass.
    /// </summary>
    public static bool RemoveHediff(Pawn? pawn) => Instance.RemoveHediff(pawn);

    /// <summary>
    /// Evaluates whether the given <paramref name="pawn"/> should have the Mechanoid Blink Hediff,
    /// applying or removing it accordingly.
    /// </summary>
    public static void UpdateMechanoid(Pawn? pawn) => Instance.UpdateMechanoid(pawn);

    /// <summary>
    /// Batch-updates all mechanoids across all loaded maps and living world pawns.
    /// </summary>
    public static void UpdateAllMechanoids() => Instance.UpdateAllMechanoids();
}
