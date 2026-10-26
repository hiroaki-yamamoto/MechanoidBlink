using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace MechanoidBlink.Core;

/// <summary>
/// Manages the application, removal, and synchronization of the Mechanoid Blink Hediff
/// on mechanoid pawns according to mod settings and game events.
/// </summary>
/// <remarks>
/// <para>
/// Rather than modifying <see cref="ThingDef.comps"/> or using reflection on internal
/// <c>AutoBlink</c> caches, this manager leverages AutoBlink's native <see cref="AutoBlink.HediffComp_AutoBlinkSource"/>
/// by adding or removing the <c>MechanoidBlink_Hediff</c> on mechanoids.
/// </para>
/// <para>
/// Operations on individual pawns execute in O(H) time and O(1) space, where H is the number of
/// hediffs currently attached to the pawn (typically H &lt; 15). Batch operations across all maps
/// and alive world pawns execute in O(N) time and O(1) space, where N is the total count of pawns.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Update a single mechanoid pawn after a faction or mental state change:
/// MechanoidBlinkHediffManager.UpdateMechanoid(mechPawn);
///
/// // Synchronize all active mechanoids after mod settings are updated:
/// MechanoidBlinkHediffManager.UpdateAllMechanoids();
/// </code>
/// </example>
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

    private static HediffDef? cachedHediffDef;

    /// <summary>
    /// Resolves the <see cref="HediffDef"/> registered for Mechanoid Blink in O(1) time.
    /// </summary>
    /// <returns>The resolved <see cref="HediffDef"/>, or <see langword="null"/> if not yet loaded.</returns>
    public static HediffDef? GetHediffDef()
    {
        if (cachedHediffDef != null)
        {
            return cachedHediffDef;
        }

        cachedHediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(HediffDefName)
            ?? DefDatabase<HediffDef>.GetNamedSilentFail(FallbackHediffDefName);

        return cachedHediffDef;
    }

    /// <summary>
    /// Resets the cached HediffDef reference (used primarily during unit tests or Def reload).
    /// </summary>
    public static void ResetCache()
    {
        cachedHediffDef = null;
    }

    /// <summary>
    /// Determines whether the specified <paramref name="pawn"/> currently possesses the Mechanoid Blink Hediff.
    /// Executes in O(H) time where H is the count of hediffs on the pawn.
    /// </summary>
    /// <param name="pawn">The target <see cref="Pawn"/> to inspect.</param>
    /// <returns><see langword="true"/> if the pawn has the hediff; otherwise, <see langword="false"/>.</returns>
    public static bool HasHediff(Pawn? pawn)
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
    /// <param name="pawn">The target living mechanoid <see cref="Pawn"/>.</param>
    /// <returns><see langword="true"/> if the hediff was newly created and applied; otherwise, <see langword="false"/>.</returns>
    public static bool ApplyHediff(Pawn? pawn)
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
    /// Removes the Mechanoid Blink Hediff from the specified <paramref name="pawn"/> if present.
    /// </summary>
    /// <param name="pawn">The target <see cref="Pawn"/> to modify.</param>
    /// <returns><see langword="true"/> if the hediff was found and removed; otherwise, <see langword="false"/>.</returns>
    public static bool RemoveHediff(Pawn? pawn)
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
    /// Evaluates whether the given <paramref name="pawn"/> should have the Mechanoid Blink Hediff
    /// according to <see cref="MechanoidBlinkMod.PermissionChecker"/>, applying or removing it accordingly.
    /// </summary>
    /// <param name="pawn">The <see cref="Pawn"/> to evaluate.</param>
    public static void UpdateMechanoid(Pawn? pawn)
    {
        if (pawn == null || pawn.def == null)
        {
            return;
        }

        // If the pawn is dead, destroyed, or not a recognized mechanoid def, strip the hediff.
        if (pawn.Dead || pawn.Destroyed || (pawn.RaceProps != null && !pawn.RaceProps.IsMechanoid) || !MechanoidRegistry.IsMechanoidDef(pawn.def))
        {
            if (HasHediff(pawn))
            {
                RemoveHediff(pawn);
            }
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
    public static void UpdateAllMechanoids()
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
