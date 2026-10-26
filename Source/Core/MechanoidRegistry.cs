using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace MechanoidBlink.Core;

/// <summary>
/// Default service implementation for discovering and caching mechanoid definitions.
/// Builds O(1) hash sets for instantaneous membership tests and zero-allocation queries.
/// </summary>
public class MechanoidRegistryService : IMechanoidRegistry
{
    private List<ThingDef>? cachedMechanoids;
    private List<string>? cachedMechanoidDefNames;
    private HashSet<ThingDef>? cachedMechanoidDefSet;
    private HashSet<string>? cachedMechanoidDefNameSet;
    private readonly object lockObj = new();

    /// <summary>
    /// Gets all registered mechanoid ThingDefs, sorted by display label or defName.
    /// </summary>
    public IReadOnlyList<ThingDef> AllMechanoids
    {
        get
        {
            EnsureCacheLoaded();
            return cachedMechanoids!;
        }
    }

    /// <summary>
    /// Gets cached defNames of all registered mechanoids in O(1) time without allocations.
    /// </summary>
    public IReadOnlyList<string> AllMechanoidDefNames
    {
        get
        {
            EnsureCacheLoaded();
            return cachedMechanoidDefNames!;
        }
    }

    /// <summary>
    /// Determines whether the given ThingDef is a valid living mechanoid pawn.
    /// Performs an O(1) hash set lookup when the cache is loaded.
    /// </summary>
    public bool IsMechanoidDef(ThingDef? def)
    {
        if (def == null)
        {
            return false;
        }

        if (cachedMechanoidDefSet != null)
        {
            return cachedMechanoidDefSet.Contains(def);
        }

        // Cache not yet built or def was dynamically added: evaluate directly
        return EvaluateIsMechanoidDef(def);
    }

    /// <summary>
    /// Determines whether the given defName corresponds to a valid mechanoid pawn in O(1) time.
    /// </summary>
    public bool IsMechanoidDefName(string? defName)
    {
        if (string.IsNullOrEmpty(defName))
        {
            return false;
        }

        EnsureCacheLoaded();
        return cachedMechanoidDefNameSet!.Contains(defName);
    }

    /// <summary>
    /// Clears the cached list and lookup sets of mechanoids, forcing a reload on next query.
    /// </summary>
    public void ResetCache()
    {
        lock (lockObj)
        {
            cachedMechanoids = null;
            cachedMechanoidDefNames = null;
            cachedMechanoidDefSet = null;
            cachedMechanoidDefNameSet = null;
        }
    }

    /// <summary>
    /// Evaluates raw definition properties to check whether the definition represents a living mechanoid pawn.
    /// Explicitly excludes corpses and buildings.
    /// </summary>
    public static bool EvaluateIsMechanoidDef(ThingDef? def)
    {
        if (def == null || def.race == null || !def.race.IsMechanoid)
        {
            return false;
        }

        // Living mechanoids belong to ThingCategory.Pawn; corpses belong to ThingCategory.Item
        if (def.category != ThingCategory.Pawn ||
            def.IsCorpse ||
            (def.thingClass != null && typeof(Corpse).IsAssignableFrom(def.thingClass)))
        {
            return false;
        }

        return true;
    }

    private void EnsureCacheLoaded()
    {
        if (cachedMechanoids != null && cachedMechanoidDefSet != null)
        {
            return;
        }

        lock (lockObj)
        {
            if (cachedMechanoids != null && cachedMechanoidDefSet != null)
            {
                return;
            }

            var allDefs = DefDatabase<ThingDef>.AllDefs;
            var list = new List<ThingDef>();
            var defSet = new HashSet<ThingDef>();
            var nameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var def in allDefs)
            {
                if (EvaluateIsMechanoidDef(def))
                {
                    list.Add(def);
                    defSet.Add(def);
                    if (!string.IsNullOrEmpty(def.defName))
                    {
                        nameSet.Add(def.defName);
                    }
                }
            }

            list.Sort(static (a, b) =>
            {
                string labelA = a.label ?? a.defName ?? string.Empty;
                string labelB = b.label ?? b.defName ?? string.Empty;
                return StringComparer.CurrentCultureIgnoreCase.Compare(labelA, labelB);
            });

            var namesList = new List<string>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                namesList.Add(list[i].defName);
            }

            cachedMechanoids = list;
            cachedMechanoidDefNames = namesList;
            cachedMechanoidDefSet = defSet;
            cachedMechanoidDefNameSet = nameSet;
        }
    }
}

/// <summary>
/// Static registry facade responsible for discovering and caching all mechanoid types.
/// Delegates queries to an injectable <see cref="IMechanoidRegistry"/> instance for testability.
/// </summary>
public static class MechanoidRegistry
{
    private static IMechanoidRegistry instance = new MechanoidRegistryService();

    /// <summary>
    /// Gets or sets the active <see cref="IMechanoidRegistry"/> implementation.
    /// Can be swapped during unit tests for mock implementations.
    /// </summary>
    public static IMechanoidRegistry Instance
    {
        get => instance;
        set => instance = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets all mechanoid ThingDefs registered in the game, ordered by label / defName.
    /// </summary>
    public static IReadOnlyList<ThingDef> AllMechanoids => Instance.AllMechanoids;

    /// <summary>
    /// Gets all mechanoid defNames without creating new allocations.
    /// </summary>
    public static IReadOnlyList<string> AllMechanoidDefNames => Instance.AllMechanoidDefNames;

    /// <summary>
    /// Determines whether the given ThingDef is a valid mechanoid pawn according to RimWorld's framework.
    /// Executes in O(1) time.
    /// </summary>
    public static bool IsMechanoidDef(ThingDef? def) => Instance.IsMechanoidDef(def);

    /// <summary>
    /// Determines whether the given defName corresponds to a valid mechanoid pawn in O(1) time.
    /// </summary>
    public static bool IsMechanoidDefName(string? defName) => Instance.IsMechanoidDefName(defName);

    /// <summary>
    /// Clears the cached list of mechanoids, forcing a reload on the next query.
    /// </summary>
    public static void ResetCache() => Instance.ResetCache();
}
