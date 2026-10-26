using HarmonyLib;
using MechanoidBlink.Core;
using Verse;
using Verse.AI;

namespace MechanoidBlink.Patches;

/// <summary>
/// Harmony patches on <see cref="MentalStateHandler"/> to re-evaluate and assign or remove the
/// Mechanoid Blink Hediff whenever a mechanoid enters or recovers from a mental state (such as going berserk).
/// </summary>
/// <remarks>
/// <para>
/// When a friendly mechanoid goes berserk, RimWorld treats it as hostile to the player faction.
/// Re-evaluating permissions immediately upon mental state changes ensures the Hediff is granted or
/// revoked in real time according to the active hostile/friendly settings.
/// </para>
/// <para>
/// Executes in O(H) time and O(1) space, where H is the count of hediffs on the pawn.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Applied automatically by Harmony via MechanoidBlinkInitializer.
/// </code>
/// </example>
[HarmonyPatch]
public static class Patch_MentalState
{
    /// <summary>
    /// Postfix handler after attempting to start a mental state.
    /// </summary>
    /// <param name="__result"><see langword="true"/> if the mental state was successfully initiated.</param>
    /// <param name="___pawn">The private <see cref="Pawn"/> field injected by Harmony.</param>
    [HarmonyPatch(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState))]
    [HarmonyPostfix]
    public static void Postfix_TryStartMentalState(bool __result, Pawn? ___pawn)
    {
        if (__result && ___pawn?.RaceProps != null && ___pawn.RaceProps.IsMechanoid)
        {
            MechanoidBlinkHediffManager.UpdateMechanoid(___pawn);
        }
    }

    /// <summary>
    /// Postfix handler after directly clearing an active mental state.
    /// </summary>
    /// <param name="___pawn">The private <see cref="Pawn"/> field injected by Harmony.</param>
    [HarmonyPatch(typeof(MentalStateHandler), "ClearMentalStateDirect")]
    [HarmonyPostfix]
    public static void Postfix_ClearMentalStateDirect(Pawn? ___pawn)
    {
        if (___pawn?.RaceProps != null && ___pawn.RaceProps.IsMechanoid)
        {
            MechanoidBlinkHediffManager.UpdateMechanoid(___pawn);
        }
    }

    /// <summary>
    /// Postfix handler after resetting the mental state handler.
    /// </summary>
    /// <param name="___pawn">The private <see cref="Pawn"/> field injected by Harmony.</param>
    [HarmonyPatch(typeof(MentalStateHandler), nameof(MentalStateHandler.Reset))]
    [HarmonyPostfix]
    public static void Postfix_Reset(Pawn? ___pawn)
    {
        if (___pawn?.RaceProps != null && ___pawn.RaceProps.IsMechanoid)
        {
            MechanoidBlinkHediffManager.UpdateMechanoid(___pawn);
        }
    }
}
