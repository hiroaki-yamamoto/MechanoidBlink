using RimWorld;
using Verse;

namespace MechanoidBlink.Core;

/// <summary>
/// Default implementation of hostility checker using native RimWorld faction logic.
/// </summary>
public sealed class RimWorldFactionHostilityChecker : IFactionHostilityChecker
{
    public static readonly RimWorldFactionHostilityChecker Instance = new();

    public bool IsHostileToPlayer(Pawn pawn)
    {
        if (pawn == null) return false;

        Faction playerFaction = Faction.OfPlayer;
        if (playerFaction == null)
        {
            // If player faction is not yet initialized, check if pawn itself belongs to player
            return pawn.Faction != null && !pawn.Faction.IsPlayer;
        }

        return pawn.HostileTo(playerFaction);
    }
}

