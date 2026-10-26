using HarmonyLib;
using MechanoidBlink.Core;
using RimWorld;
using Verse;

namespace MechanoidBlink.Patches;

/// <summary>
/// Harmony postfix patch on <see cref="Pawn.SetFaction"/> to re-evaluate and assign or remove the
/// Mechanoid Blink Hediff whenever a mechanoid changes factions (e.g. hacked or tamed under colony control).
/// </summary>
/// <remarks>
/// Executes in O(H) time and O(1) space, where H is the count of hediffs on the pawn.
/// </remarks>
/// <example>
/// <code>
/// // Applied automatically by Harmony via MechanoidBlinkInitializer.
/// </code>
/// </example>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.SetFaction))]
public static class Patch_Pawn_SetFaction
{
    /// <summary>
    /// Postfix handler invoked after a pawn's faction has changed.
    /// </summary>
    /// <param name="__instance">The <see cref="Pawn"/> whose faction changed.</param>
    /// <param name="newFaction">The new <see cref="Faction"/> assigned to the pawn.</param>
    public static void Postfix(Pawn __instance, Faction newFaction)
    {
        if (__instance?.RaceProps != null && __instance.RaceProps.IsMechanoid)
        {
            MechanoidBlinkHediffManager.UpdateMechanoid(__instance);
        }
    }
}
