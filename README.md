> [!CAUTION]
> If you have PTSD of AI, you shouldn't use/read this mod (and code).
> This mod uses AI for coding.

# Mechanoid Blink

A RimWorld 1.7 mod that integrates [AutoBlink](https://steamcommunity.com/sharedfiles/filedetails/?id=3493889625) into mechanoids, letting them automatically teleport along their walking path. Friendly and hostile mechanoids can be enabled independently, and you can choose exactly which mechanoid types may blink.

## Features

- Adds an AutoBlink profile (`MechanoidBlink_Profile`) for mechanoids. It is not limited to the player faction.
- Separate toggles for **friendly** and **hostile** mechanoids.
- Per-mechanoid-type selection, with search, *Select All* and *Deselect All*.
- Permissions are enforced through an AutoBlink policy that denies blinking for mechanoids that are not allowed.
- Localization: English and Japanese.

## Requirements

- RimWorld 1.7
- [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077) (`brrainz.harmony`)
- [AutoBlink](https://steamcommunity.com/sharedfiles/filedetails/?id=3493889625) (`rabiosus.autoblink`)

Load order: Harmony, then AutoBlink, then Mechanoid Blink.

## Installation

1. Put the `MechanoidBlink` folder in `RimWorld/Mods/`.
2. Enable Harmony, AutoBlink and Mechanoid Blink in the mod list, in that order.

## Settings

Open **Options → Mod Settings → Mechanoid Blink**:

| Setting | Description |
|---|---|
| Integrate AutoBlink into friendly mechanoids | Friendly mechanoids can use AutoBlink abilities. |
| Integrate AutoBlink into hostile mechanoids | Hostile mechanoids can use AutoBlink abilities. |
| Choose which mechanoid types can use AutoBlink | Pick allowed types; use Search, Select All, or Deselect All. |

## Project layout

```
About/            Mod metadata
Common/Defs/      AutoBlink profile definition
Common/Languages/ English and Japanese strings
LoadFolders.xml   Version-specific load folders
Source/           C# source (.NET)
  Core/           Registry, permission checker, policy, injector, AutoBlink bridge
  Patches/        Harmony patches (blink controller, gizmos)
  Settings/       Mod settings
  UI/             Settings window
  Tests/          Unit and integration tests
```

## Building

The C# solution is in `Source/` (`Source.slnx`):

```
dotnet build Source/Source.csproj -c Release
```

Run the tests:

```
dotnet run --project Source/Tests/MechanoidBlink.Tests.csproj
```

## Issue and Pull Request

Any Issues are not welcome for mentainance / mental reasons. However, Pull
Requests are generally welcome. If you send a Pull Request, please make sure
your change has been tested.

## Credits

Created by Hiroaki Yamamoto and Gemini. Built on the AutoBlink mod by rabiosus.

## License

See [LICENSE.md](LICENSE.md).
