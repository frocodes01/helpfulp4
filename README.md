# UMAD P4 Helper

An in-game Dalamud helper for **FFXIV UMAD Phase 4**.

This plugin recreates and streamlines the decision-making flow of the WTFDIG P4 helper inside Final Fantasy XIV, with a focus on quick manual inputs and clear text playback.

## Features

- `/p4helper` command to open the helper window
- Manual **Real / Fake** inputs for Neo Exdeath, Chaos, Mana Charge, and Mana Release
- Clear selected-state highlighting
  - **Real:** dark navy with white text
  - **Fake:** red with yellow text
- Responsive layout
  - Wide window: two-column layout
  - Narrow window: single-column layout with scrolling
- One-click **Undo** for the last input/action
- **Reset** button
- Automatic paired inputs where the mechanic guarantees the opposite value
- Text-only playback designed for use during progression

## Current Logic

### Neo Exdeath

Each Neo Exdeath cast can independently be Real or Fake.

| Cast | Spread Callout | Gaze |
| --- | --- | --- |
| Real | LIGHTNING | LOOK AWAY |
| Fake | WATER | LOOK IN |

The spread callout identifies **which element is spreading**.  
For example, if the helper says `LIGHTNING`, players with Lightning spread while the opposite element stacks.

### Water / Lightning Timing

Water/Lightning timers are paired between the two Neo Exdeath sets.

- Neo #1 Short → Neo #2 Long
- Neo #1 Long → Neo #2 Short
- Neo #2 Short → Neo #1 Long
- Neo #2 Long → Neo #1 Short

Selecting one automatically sets the other.

### Acceleration

Acceleration is entered **manually** and is independent of Water/Lightning timing.

Available inputs:

- Short
- Long
- None

Only one Neo set should contain your acceleration. Selecting Short or Long on one Neo automatically sets the other Neo's acceleration to `None`.

Acceleration playback:

| Accel Timing | Neo Cast | Playback |
| --- | --- | --- |
| Short | Real | 1st STILLNESS |
| Short | Fake | 1st MOTION |
| Long | Real | 2nd STILLNESS |
| Long | Fake | 2nd MOTION |
| None | — | No accel this set |

### Chaos

There is always one Fire/Inferno and one Water/Tsunami.

Selecting Fire or Water on one Chaos automatically sets the opposite element on the other Chaos.

#### Inferno

| Cast | Result |
| --- | --- |
| Real | SPREAD |
| Fake | STAY |

#### Tsunami

| Cast | Result |
| --- | --- |
| Real | STAY |
| Fake | SPREAD |

The playback displays **Inferno first, then Tsunami**, regardless of whether they were Chaos #1 or Chaos #2.

### Mana Charge / Mana Release

Mana Charge order is fixed:

1. Lightning
2. Blizzard

The final truth is determined by comparing the Charge and Release values.

| Charge | Release | Final |
| --- | --- | --- |
| Real | Real | Real |
| Real | Fake | Fake |
| Fake | Real | Fake |
| Fake | Fake | Real |

In other words:

- Same truth → Real
- Different truth → Fake

### Final Mana Callout

The plugin converts the resolved Lightning + Blizzard states into a movement callout:

| Lightning | Blizzard | Final Mana |
| --- | --- | --- |
| Real | Real | OUT OF BOTH |
| Fake | Fake | IN BOTH |
| Real | Fake | IN BLIZZARD |
| Fake | Real | IN LIGHTNING |

Because Mana Release resolves alongside Tsunami, the final call also includes the Tsunami movement:

- Tsunami `STAY` → `DONUT`
- Tsunami `SPREAD` → `CHARIOT`

Examples:

```text
Final Mana: OUT OF BOTH + DONUT
Final Mana: IN BOTH + CHARIOT
Final Mana: IN BLIZZARD + DONUT
Final Mana: IN LIGHTNING + CHARIOT
```

## Installation for Development

This plugin is currently intended to be loaded as a Dalamud development plugin.

### Requirements

- Final Fantasy XIV
- XIVLauncher / Dalamud
- .NET SDK
- VS Code or Visual Studio
- Dalamud SamplePlugin development environment

### Build

From the plugin project directory:

```powershell
dotnet build
```

The compiled DLL will be created under the project's `bin` directory.

Example:

```text
UmadP4Helper\bin\Debug\UmadP4Helper.dll
```

### Load in Dalamud

1. Launch FFXIV with Dalamud.
2. Open:
   ```text
   /xlsettings
   ```
3. Go to **Experimental**.
4. Add the compiled DLL under **Dev Plugin Locations**.
5. Open:
   ```text
   /xlplugins
   ```
6. Enable the development plugin.

## Usage

Open the helper with:

```text
/p4helper
```

Enter mechanic information as it appears during Phase 4.

The right side of the window displays the resolved playback in mechanic order.

Use **Undo** if the previous input was entered incorrectly.

Use **Reset** before starting a new pull.

## Project Status

Current version is a manual, text-only helper.

Possible future improvements:

- Better playback color-coding
- Configuration options
- Saved UI preferences
- Compact raid mode
- Additional input validation
- Automatic mechanic detection if desired later

## Credits

This project was inspired by the P4 Helper available at:

`https://wtfdig.info/tools/p4-helper`

The goal of this plugin is to provide a streamlined in-game workflow with custom playback wording and reduced input friction.

## Disclaimer

This is an unofficial community tool and is not affiliated with Square Enix, XIVLauncher, Dalamud, or WTFDIG. AI was used in this project.
