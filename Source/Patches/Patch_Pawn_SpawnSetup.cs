using HarmonyLib;
using MechanoidBlink.Core;
using Verse;

namespace MechanoidBlink.Patches;

/// <summary>
/// Harmony postfix patch on <see cref="Pawn.SpawnSetup"/> to evaluate and assign the Mechanoid Blink Hediff
/// the moment a mechanoid appears on a map (raids, mech clusters, gestation, drop pod arrivals).
/// </summary>
/// <remarks>
/// Executes in O(H) time and O(1) space, where H is the count of hediffs on the pawn.
/// </remarks>
/// <example>
/// <code>
/// // Applied automatically by Harmony via MechanoidBlinkInitializer.
/// </code>
/// </example>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
public static class Patch_Pawn_SpawnSetup
{
    /// <summary>
    /// Postfix handler invoked after a pawn is spawned into a map.
    /// </summary>
    /// <param name="__instance">The <see cref="Pawn"/> that has just been spawned.</param>
    public static void Postfix(Pawn __instance)
    {
        if (__instance?.RaceProps != null && __instance.RaceProps.IsMechanoid)
        {
            MechanoidBlinkHediffManager.UpdateMechanoid(__instance);
        }
    }
}
