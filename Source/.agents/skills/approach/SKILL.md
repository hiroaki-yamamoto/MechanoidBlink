---
name: approach
description: This skill is a memo how to approach the goal of this mod.
---

# Approach to Feature Implementation

The following approach will be adopted to implement this feature:

1. Define the Hediff
2. Integrate the Hediff with AutoBlink
    - Mechanoids granted this Hediff will be able to use the AutoBlink feature.
3. Apply these Hediffs to mechanoids matching the mod settings criteria under
  the following circumstances:
    - Upon spawning:
      - The moment a mechanoid appears on the map (e.g., raids, mechanoid
        clusters, waking from dormancy, gestation/manufacturing, drop pod
        arrivals).
    - Upon mod settings change:
      - The moment a player checks or unchecks options such as
        "Disable Hostile Mechanoids" or specific races in the settings menu.
      - Requires iterating through all existing mechanoids to batch add or
        remove the Hediff.
    - Upon faction change:
      - When an enemy mechanoid is hacked or tamed to fall under colony control,
        or when one goes berserk / turns hostile.
      - Requires reassigning/updating the Hediff based on the active
        configuration (e.g., "Allies only", "Enemies only").

## Hediff Definition

- Name: Mechanoid Blink
- Description: This mechanoid can teleport automatically.
- HediffClass: HediffWithComps
- Label Color:
  - r: 0.52
  - g: 1.0
  - b: 0.95
- Target: Mechanoid
- Body Part: Whole body
- isBad: false
- initialSeverity: 1.0
- maxSeverity: 1.0
