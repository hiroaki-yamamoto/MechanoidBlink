using AutoBlink;
using Verse;

namespace MechanoidBlink.Core;

/// <summary>
/// AutoBlink policy implementation that enforces Mechanoid Blink permissions.
/// Registered into AutoBlinkApi to guard against unauthorized blinking.
/// </summary>
public class MechanoidBlinkPolicy : IAutoBlinkPolicy
{
    private readonly IMechanoidBlinkPermissionChecker permissionChecker;

    public MechanoidBlinkPolicy(IMechanoidBlinkPermissionChecker permissionChecker)
    {
        this.permissionChecker = permissionChecker ?? throw new System.ArgumentNullException(nameof(permissionChecker));
    }

    public BlinkPlanConstraint BeforePlan(ref BlinkContext context)
    {
        Pawn pawn = context.Pawn;
        if (pawn != null && pawn.RaceProps != null && pawn.RaceProps.IsMechanoid)
        {
            if (!permissionChecker.IsBlinkAllowed(pawn))
            {
                return BlinkPlanConstraint.Deny;
            }
        }

        return BlinkPlanConstraint.Allow;
    }

    public BlinkTargetDecision AdjustTarget(ref BlinkContext context, IntVec3 proposedTarget)
    {
        return BlinkTargetDecision.Allow;
    }
}

