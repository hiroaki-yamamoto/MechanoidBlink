using HarmonyLib;
using MechanoidBlink.Core;
using RimWorld;
using Verse;

namespace MechanoidBlink.Patches;

/// <summary>
/// Harmony postfix patch on <see cref="CompCanBeDormant.WakeUp"/> to evaluate and assign the Mechanoid Blink Hediff
/// the moment a dormant mechanoid (e.g. from a mechanoid cluster) wakes up.
/// </summary>
/// <remarks>
/// Executes in O(H) time and O(1) space, where H is the count of hediffs on the pawn.
/// </remarks>
/// <example>
/// <code>
/// // Applied automatically by Harmony via MechanoidBlinkInitializer.
/// </code>
/// </example>
[HarmonyPatch(typeof(CompCanBeDormant), nameof(CompCanBeDormant.WakeUp))]
public static class Patch_CompCanBeDormant_WakeUp
{
    /// <summary>
    /// Postfix handler invoked after a dormant entity wakes up.
    /// </summary>
    /// <param name="__instance">The <see cref="CompCanBeDormant"/> instance waking up.</param>
    public static void Postfix(CompCanBeDormant __instance)
    {
        if (__instance?.parent is Pawn pawn && pawn.RaceProps != null && pawn.RaceProps.IsMechanoid)
        {
            MechanoidBlinkHediffManager.UpdateMechanoid(pawn);
        }
    }
}
