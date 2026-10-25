using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace MechanoidBlink.Core;

/// <summary>
/// Registry responsible for discovering and caching all mechanoid types
/// registered under RimWorld's Mechanoid framework (vanilla and mods).
/// </summary>
public static class MechanoidRegistry
{
  private static List<ThingDef>? cachedMechanoidDefs;

  /// <summary>
  /// Gets all mechanoid ThingDefs registered in the game, ordered by label / defName.
  /// </summary>
  public static IReadOnlyList<ThingDef> AllMechanoids
  {
    get
    {
      if (cachedMechanoidDefs == null)
      {
        cachedMechanoidDefs = DefDatabase<ThingDef>.AllDefs
            .Where(IsMechanoidDef)
            .OrderBy(d => d.label ?? d.defName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
      }
      return cachedMechanoidDefs;
    }
  }

  /// <summary>
  /// Gets all mechanoid defNames.
  /// </summary>
  public static IEnumerable<string> AllMechanoidDefNames =>
      AllMechanoids.Select(d => d.defName);

  /// <summary>
  /// Determines whether the given ThingDef is a valid mechanoid pawn according to RimWorld's framework.
  /// Explicitly excludes mechanoid corpses (which inherit RaceProperties from the living pawn).
  /// </summary>
  public static bool IsMechanoidDef(ThingDef? def)
  {
    if (def == null || def.race == null || !def.race.IsMechanoid)
    {
      return false;
    }

    // RimWorld automatically generates corpse ThingDefs (e.g., Corpse_Mech_Centipede)
    // that share the living pawn's race properties (def.race.IsMechanoid == true).
    // To exclude corpses:
    // 1. A living mechanoid pawn belongs to ThingCategory.Pawn (corpses belong to ThingCategory.Item).
    // 2. def.IsCorpse must be false.
    // 3. def.thingClass must not be assignable to Corpse.
    if (def.category != ThingCategory.Pawn ||
        def.IsCorpse ||
        (def.thingClass != null && typeof(Corpse).IsAssignableFrom(def.thingClass)))
    {
      return false;
    }

    return true;
  }

  /// <summary>
  /// Clears the cached list of mechanoids, forcing a reload on the next query.
  /// </summary>
  public static void ResetCache()
  {
    cachedMechanoidDefs = null;
  }
}
