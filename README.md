![Njord's Hand Icon](icon.png)
# Njord's Hond

**Njord's Hond** is a Valheim mod that enhances sea navigation by introducing intelligent ship autopilot capabilities. Built on top of the ShipNavigator mod, it allows players to set waypoint-based courses, switch between sailing and paddling automatically based on wind conditions, and control their vessel using simple chat commands. Njord, the Norse god of the sea and wind, lends his steady hand to steer your course—even when you're distracted by battle or beer.

## Features

- 📍 **Waypoint routing**: Define custom routes using in-game map pins.
- 🌬️ **Wind-aware navigation**: Automatically switches between sailing and paddling to avoid being stuck in irons.
- 💬 **Chat command control**: Manage navigation with in-game chat commands.
- 🔁 **Hot-reload friendly**: Designed for live code reloading without restarting the game.

## Requirements

- Valheim (latest stable version)
- [BepInEx](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/)
- [ShipNavigator](https://www.nexusmods.com/valheim/mods/885) mod

> This mod builds on [ShipNavigator](https://www.nexusmods.com/valheim/mods/885) by JustCrazy. Full credit to JustCrazy for the original automatic ship navigation system.

## Installation

1. Install [BepInEx](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/).
2. Download and install dependencies listed above.
3. Place the `NjordsHand.dll` in your `BepInEx/plugins` directory.

## Usage

1. Add waypoint pins to your map

Open the in-game chat (`Enter`) and use the following commands:

- `/nh sc [pin name 1] [pin name 2] [etc]` — Set desired course

When you send the chat message, Njords hand will immediately start to guide your vessel from pin to pin. When the final pin is reached, the vessel speed will be set to stop.

- `/nh cc` — Clear the current course.

## Building

This project includes a `VERSION.txt` file and is structured for command-line builds. You may use the included `build.sh` to automate building and deployment.

## Development Notes

- Source is compatible with macOS.
- Hot-reload support is prioritized; avoid static state where possible.

## License

MIT License. See [LICENSE](LICENSE) for details.