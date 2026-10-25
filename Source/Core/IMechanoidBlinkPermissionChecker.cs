using Verse;

namespace MechanoidBlink.Core;

/// <summary>
/// Interface for checking if a mechanoid is permitted to blink.
/// </summary>
public interface IMechanoidBlinkPermissionChecker
{
    /// <summary>
    /// Checks whether the specified pawn is allowed to use AutoBlink.
    /// </summary>
    /// <param name="pawn">The pawn to check.</param>
    /// <returns>True if allowed to blink; otherwise false.</returns>
    bool IsBlinkAllowed(Pawn pawn);
}

/// <summary>
/// Abstraction for accessing mod settings to support unit testing without RimWorld engine state.
/// </summary>
public interface ISettingsProvider
{
    bool EnableFriendly { get; }
    bool EnableHostile { get; }
    bool IsMechTypeAllowed(string defName);
}

/// <summary>
/// Abstraction for checking pawn hostility to support unit testing.
/// </summary>
public interface IFactionHostilityChecker
{
    bool IsHostileToPlayer(Pawn pawn);
}

