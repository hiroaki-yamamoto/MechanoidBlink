using System.Collections.Generic;
using Verse;

namespace MechanoidBlink.Core;

/// <summary>
/// Abstraction for discovering, caching, and querying mechanoid ThingDefs.
/// Decouples DefDatabase queries to enable high-speed lookups and isolated unit testing.
/// </summary>
public interface IMechanoidRegistry
{
    /// <summary>
    /// Gets all registered mechanoid ThingDefs, sorted by display label or defName.
    /// </summary>
    IReadOnlyList<ThingDef> AllMechanoids { get; }

    /// <summary>
    /// Gets cached defNames of all registered mechanoids in O(1) time without allocations.
    /// </summary>
    IReadOnlyList<string> AllMechanoidDefNames { get; }

    /// <summary>
    /// Determines whether the given ThingDef is a valid living mechanoid pawn.
    /// Executes in O(1) time via cached set lookup.
    /// </summary>
    bool IsMechanoidDef(ThingDef? def);

    /// <summary>
    /// Determines whether the given defName corresponds to a valid mechanoid pawn in O(1) time.
    /// </summary>
    bool IsMechanoidDefName(string? defName);

    /// <summary>
    /// Clears the cached mechanoid definitions and index sets, forcing a reload on the next query.
    /// </summary>
    void ResetCache();
}
