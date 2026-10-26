---
name: features
description: This skill explans about the goal and the features about this mod.
---

# The goal

This project is a patch that integrates the features of the RimWorld mod
[AutoBlink] into mechanoids from the Mechanoid framework.

# UI Design

The UI design follows the native vanilla style of RimWorld.

# Coding Note

- Because the source code of this mod is intended to be read by humans,
  the code must be clean.
- Especially, the code should follow the SOLID principle.
- The code should be well-tested.

# Feature List

1. Integrates [AutoBlink] functionality into friendly mechanoids.
2. Integrates [AutoBlink] functionality into hostile mechanoids.

# Settings

Configured via Options > Mod Options. For approach to enable AutoBlink,
refer approach skill.

1. Checkbox: Integrate [AutoBlink] into friendly mechanoids
  - If true (✅), friendly mechanoids can use [AutoBlink] abilities.
  - Otherwise, friendly mechanoids cannot use [AutoBlink].
  - Default: true
2. Checkbox: Integrate [AutoBlink] into hostile mechanoids
  - If true (✅), hostile mechanoids can use [AutoBlink] abilities.
  - Otherwise, hostile mechanoids cannot use [AutoBlink].
  - Default: false
3. Selection box: Choose which mechanoid types can use [AutoBlink]
  - Includes all mechanoids registered under RimWorld's Mechanoid framework.
    - Note that the mechanoid corpse must be excluded from the list and these
      mechanoids corpses cannot use [AutoBlink].
  - This covers both vanilla mechanoids and custom mechanoids added by other
    mods via the framework.
  - If a mechanoid type is selected here AND setting 1 or 2 is enabled,
    matching mechanoids can use [AutoBlink].
  - Otherwise, the mechanoid cannot use [AutoBlink].
  - Includes "Select All" and "Deselect All" buttons and their
    functionalities.

[AutoBlink]: https://steamcommunity.com/sharedfiles/filedetails/?id=3493889625
