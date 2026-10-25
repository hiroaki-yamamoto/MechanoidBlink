using System.Collections.Generic;
using System.Reflection;
using AutoBlink;
using HarmonyLib;
using MechanoidBlink.Core;
using RimWorld;
using Verse;

namespace MechanoidBlink.Patches;

/// <summary>
/// Harmony patch on internal type <c>AutoBlink.BlinkGizmos.Append</c> to control AutoBlink
/// command gizmo visibility on mechanoid pawns.
/// </summary>
/// <remarks>
/// <para>
/// Upstream <c>BlinkGizmos.Append</c> suppresses gizmos whenever <c>pawn.RaceProps.IsMechanoid</c>
/// and <c>!pawn.IsColonyMechPlayerControlled</c>, which hides the AutoBlink button on non-Biotech
/// player mechanoids, un-overseen/Dev-Mode player mechanoids, and custom mechanoid frameworks
/// that do not attach <see cref="CompOverseerSubject"/>.
/// </para>
/// <para>
/// Handling mechanoid pawns directly in <see cref="Prefix"/> runs in O(1) time and O(1) space
/// when suppressed, and uses a single iterator state machine in O(G) time and O(1) space when
/// appending the prepared <c>BlinkCommand</c> gizmo.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// bool showGizmo = Patch_BlinkGizmos_Append.ShouldShowMechanoidGizmo(mechPawn);
/// </code>
/// </example>
[HarmonyPatch]
public static class Patch_BlinkGizmos_Append
{
    /// <summary>
    /// Resolves <c>AutoBlink.BlinkGizmos.Append</c> dynamically at patch time.
    /// </summary>
    /// <returns>The target <see cref="MethodBase"/>, or <see langword="null"/> if not found.</returns>
    public static MethodBase? TargetMethod()
    {
        System.Type? type = AccessTools.TypeByName("AutoBlink.BlinkGizmos");
        return type != null ? AccessTools.Method(type, "Append") : null;
    }

    /// <summary>
    /// Evaluates mechanoid gizmo eligibility directly: suppresses AutoBlink gizmos for disallowed
    /// or non-player-controllable mechanoids, and appends the prepared <c>BlinkCommand</c> gizmo
    /// once for allowed player-faction mechanoids. Non-mechanoid pawns fall through to the
    /// original <c>BlinkGizmos.Append</c> implementation.
    /// </summary>
    /// <param name="original">The upstream gizmo sequence before AutoBlink appends its command.</param>
    /// <param name="controller">The <c>AutoBlink.BlinkController</c> instance.</param>
    /// <param name="__result">Receives the resulting gizmo sequence when handled by this prefix.</param>
    /// <returns><see langword="false"/> when <paramref name="controller"/> belongs to a mechanoid pawn; otherwise, <see langword="true"/>.</returns>
    public static bool Prefix(IEnumerable<Gizmo> original, object controller, ref IEnumerable<Gizmo> __result)
    {
        if (controller == null)
        {
            return true;
        }

        Pawn? pawn = AutoBlinkBridge.GetPawn(controller);
        if (pawn == null || pawn.RaceProps == null || !pawn.RaceProps.IsMechanoid)
        {
            return true;
        }

        if (!ShouldShowMechanoidGizmo(pawn))
        {
            __result = original;
            return false;
        }

        __result = AppendCommandIfMissing(original, controller);
        return false;
    }

    /// <summary>
    /// Determines whether <paramref name="pawn"/> is a living, spawned, player-faction mechanoid
    /// permitted by Mechanoid Blink settings to display the AutoBlink command gizmo.
    /// </summary>
    /// <param name="pawn">The candidate mechanoid <see cref="Pawn"/>.</param>
    /// <returns><see langword="true"/> if the AutoBlink command gizmo should be shown; otherwise, <see langword="false"/>.</returns>
    public static bool ShouldShowMechanoidGizmo(Pawn? pawn)
    {
        if (pawn == null || pawn.def == null || pawn.RaceProps == null || !pawn.RaceProps.IsMechanoid)
        {
            return false;
        }

        if (!MechanoidRegistry.IsMechanoidDef(pawn.def))
        {
            return false;
        }

        if (MechanoidBlinkMod.PermissionChecker != null &&
            !MechanoidBlinkMod.PermissionChecker.IsBlinkAllowed(pawn))
        {
            return false;
        }

        if (!pawn.Spawned || pawn.Destroyed || (pawn.health != null && pawn.Dead))
        {
            return false;
        }

        Faction? playerFaction = Faction.OfPlayerSilentFail;
        bool isPlayerFaction = pawn.Faction != null &&
            (playerFaction != null ? pawn.Faction == playerFaction : pawn.Faction.IsPlayer);
        if (!isPlayerFaction)
        {
            return false;
        }

        if (pawn.mindState?.mentalStateHandler?.CurStateDef != null || pawn.guest?.HostFaction != null)
        {
            return false;
        }

        if (pawn.RaceProps.Humanlike && (pawn.health == null || !pawn.IsColonistPlayerControlled))
        {
            return false;
        }

        return true;
    }

    private static IEnumerable<Gizmo> AppendCommandIfMissing(IEnumerable<Gizmo> gizmos, object controller)
    {
        Gizmo? command = AutoBlinkBridge.GetCommand(controller);
        bool alreadyYielded = false;

        if (gizmos != null)
        {
            foreach (Gizmo gizmo in gizmos)
            {
                if (command != null && ReferenceEquals(gizmo, command))
                {
                    alreadyYielded = true;
                }

                yield return gizmo;
            }
        }

        if (alreadyYielded)
        {
            yield break;
        }

        AutoBlinkBridge.RefreshProfile(controller);
        AutoBlinkProfileDef? profile = AutoBlinkBridge.GetProfile(controller);
        if (profile == null || !profile.showGizmo)
        {
            yield break;
        }

        command ??= AutoBlinkBridge.GetCommand(controller);
        if (command != null && AutoBlinkBridge.PrepareCommand(command, profile))
        {
            yield return command;
        }
    }
}
