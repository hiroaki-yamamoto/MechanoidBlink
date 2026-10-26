using Verse;

namespace MechanoidBlink.Core;

/// <summary>
/// Abstraction for managing the application, removal, and synchronization of the Mechanoid Blink Hediff.
/// Enables mock injection and isolated unit testing of hediff lifecycle logic.
/// </summary>
public interface IMechanoidBlinkHediffManager
{
    /// <summary>
    /// Resolves the <see cref="HediffDef"/> registered for Mechanoid Blink in O(1) time.
    /// </summary>
    HediffDef? GetHediffDef();

    /// <summary>
    /// Resets the cached HediffDef reference.
    /// </summary>
    void ResetCache();

    /// <summary>
    /// Determines whether the specified <paramref name="pawn"/> currently possesses the Mechanoid Blink Hediff.
    /// </summary>
    bool HasHediff(Pawn? pawn);

    /// <summary>
    /// Applies the Mechanoid Blink Hediff to the specified <paramref name="pawn"/> if not already present.
    /// </summary>
    bool ApplyHediff(Pawn? pawn);

    /// <summary>
    /// Removes the Mechanoid Blink Hediff from the specified <paramref name="pawn"/> if present in a single pass.
    /// </summary>
    bool RemoveHediff(Pawn? pawn);

    /// <summary>
    /// Evaluates whether the given <paramref name="pawn"/> should have the Mechanoid Blink Hediff,
    /// applying or removing it accordingly.
    /// </summary>
    void UpdateMechanoid(Pawn? pawn);

    /// <summary>
    /// Batch-updates all mechanoids across all loaded maps and living world pawns.
    /// </summary>
    void UpdateAllMechanoids();
}
