using System.Reflection;
using HarmonyLib;
using MechanoidBlink.Core;
using Verse;

namespace MechanoidBlink.Patches;

/// <summary>
/// Harmony patch on internal type <c>AutoBlink.BlinkController.CanUse</c> to short-circuit
/// AutoBlink evaluations for mechanoids that are disabled in settings.
/// </summary>
/// <remarks>
/// Uses <see cref="AutoBlinkBridge.GetPawn"/> to resolve the owning <see cref="Pawn"/> in O(1)
/// time and O(1) space without per-call reflection allocations.
/// </remarks>
/// <example>
/// <code>
/// bool result = true;
/// bool runOriginal = Patch_BlinkController_CanUse.Prefix(controller, ref result);
/// </code>
/// </example>
[HarmonyPatch]
public static class Patch_BlinkController_CanUse
{
    /// <summary>
    /// Resolves <c>AutoBlink.BlinkController.CanUse</c> dynamically at patch time.
    /// </summary>
    /// <returns>The target <see cref="MethodBase"/>, or <see langword="null"/> if not found.</returns>
    public static MethodBase? TargetMethod()
    {
        System.Type? type = AccessTools.TypeByName("AutoBlink.BlinkController");
        return type != null ? AccessTools.Method(type, "CanUse") : null;
    }

    /// <summary>
    /// Short-circuits <c>BlinkController.CanUse</c> in O(1) time when a mechanoid is disallowed by mod settings.
    /// </summary>
    /// <param name="__instance">The <c>AutoBlink.BlinkController</c> instance.</param>
    /// <param name="__result">Receives <see langword="false"/> when blinking is denied.</param>
    /// <returns><see langword="false"/> to skip the original method when denied; otherwise, <see langword="true"/>.</returns>
    public static bool Prefix(object __instance, ref bool __result)
    {
        if (__instance == null) return true;

        Pawn? pawn = AutoBlinkBridge.GetPawn(__instance);

        if (pawn != null && pawn.RaceProps != null && pawn.RaceProps.IsMechanoid)
        {
            if (MechanoidBlinkMod.PermissionChecker != null &&
                !MechanoidBlinkMod.PermissionChecker.IsBlinkAllowed(pawn))
            {
                __result = false;
                return false; // Skip original CanUse calculation
            }
        }

        return true;
    }
}

