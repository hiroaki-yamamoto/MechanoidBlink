using Verse;

namespace MechanoidBlink.Core;

/// <summary>
/// Core permission checker for mechanoid AutoBlink capability.
/// Follows SOLID principles: open to extension, highly testable via injected dependencies.
/// </summary>
public class MechanoidBlinkPermissionChecker : IMechanoidBlinkPermissionChecker
{
    private readonly ISettingsProvider settingsProvider;
    private readonly IFactionHostilityChecker hostilityChecker;

    public MechanoidBlinkPermissionChecker(
        ISettingsProvider settingsProvider,
        IFactionHostilityChecker? hostilityChecker = null)
    {
        this.settingsProvider = settingsProvider ?? throw new System.ArgumentNullException(nameof(settingsProvider));
        this.hostilityChecker = hostilityChecker ?? RimWorldFactionHostilityChecker.Instance;
    }

    /// <summary>
    /// Determines whether the specified pawn is permitted to blink based on settings and faction state.
    /// Mechanoid corpses or dead mechanoids are strictly disallowed from blinking.
    /// </summary>
    public bool IsBlinkAllowed(Pawn pawn)
    {
        if (pawn == null || pawn.def == null)
        {
            return false;
        }

        // Dead pawns / mechanoid corpses cannot blink
        if (pawn.Destroyed || (pawn.health != null && pawn.Dead))
        {
            return false;
        }

        // Only manage mechanoids; if it's not a mechanoid, do not restrict AutoBlink
        if (pawn.RaceProps == null || !pawn.RaceProps.IsMechanoid)
        {
            return true;
        }

        // If the def itself is a corpse or invalid mechanoid def, deny
        if (!MechanoidRegistry.IsMechanoidDef(pawn.def))
        {
            return false;
        }

        // Setting 3: Mechanoid type selection check
        if (!settingsProvider.IsMechTypeAllowed(pawn.def.defName))
        {
            return false;
        }

        // Setting 1 & 2: Friendly / Hostile check
        bool isHostile = hostilityChecker.IsHostileToPlayer(pawn);
        if (isHostile)
        {
            return settingsProvider.EnableHostile;
        }
        else
        {
            return settingsProvider.EnableFriendly;
        }
    }
}

