using HarmonyLib;
using Verse;

namespace MechanoidBlink.Patches;

/// <summary>
/// Harmony patch on <see cref="Hediff_AddedPart.PostAdd"/> to prevent <see cref="System.NullReferenceException"/>
/// when an added-part Hediff is applied to the whole body (<c>Part == null</c>).
/// </summary>
/// <remarks>
/// <para>
/// Vanilla RimWorld's <see cref="Hediff_AddedPart.PostAdd"/> assumes <see cref="Hediff.Part"/> is non-null
/// and iterates over <c>Part.parts</c> to generate <c>MissingBodyPart</c> records for amputated sub-parts.
/// When Mechanoid Blink is configured as a whole-body implant/added-part (<c>Part == null</c>), this prefix
/// bypasses sub-part replacement while properly initializing attached comps (including
/// <see cref="AutoBlink.HediffComp_AutoBlinkSource"/>).
/// </para>
/// <para>
/// Executes in O(C) time and O(1) space, where C is the number of comps on the hediff (typically 1).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Applied automatically by Harmony via MechanoidBlinkInitializer.
/// </code>
/// </example>
[HarmonyPatch(typeof(Hediff_AddedPart), nameof(Hediff_AddedPart.PostAdd))]
public static class Patch_Hediff_AddedPart_PostAdd
{
    /// <summary>
    /// Intercepts <see cref="Hediff_AddedPart.PostAdd"/> when <paramref name="__instance"/> has no specific body part assigned.
    /// </summary>
    /// <param name="__instance">The <see cref="Hediff_AddedPart"/> instance being added.</param>
    /// <param name="dinfo">Optional damage info that caused the addition.</param>
    /// <returns><see langword="false"/> to skip vanilla child-part replacement when <c>Part == null</c>; otherwise, <see langword="true"/>.</returns>
    public static bool Prefix(Hediff_AddedPart __instance, DamageInfo? dinfo)
    {
        if (__instance == null)
        {
            return true;
        }

        if (__instance.Part == null)
        {
            if (__instance.comps != null)
            {
                for (int i = 0; i < __instance.comps.Count; i++)
                {
                    __instance.comps[i].CompPostPostAdd(dinfo);
                }
            }
            return false;
        }

        return true;
    }
}
