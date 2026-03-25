# Dice Battler Setup

## Create the missing assets

In Unity, use:

- `Tools > Dice Battler > Setup > Create Default Assets`

This creates:

- `Assets/Configs/Defaults/SheetsImportSettings.asset`
- `Assets/Configs/Defaults/ImportStatusReport.asset`
- `Assets/Configs/Defaults/PrototypeContentSet.asset`
- default Hero, Mobs, Waves, Combinations, Upgrades, Progression, and RunConfig assets
- empty prefab registries

## Configure Google Sheets import

Open `Assets/Configs/Defaults/SheetsImportSettings.asset` and fill the `gid` for each tab:

- `Hero`
- `Mobs`
- `Waves`
- `Combinations`
- `Upgrades`
- `Progression`
- `RunConfig`

The spreadsheet ID is already set to the provided Google Sheet.

Then run:

- `Tools > Dice Battler > CSV Importer`
- assign `SheetsImportSettings.asset`
- assign `ImportStatusReport.asset`
- click `Download From Google Sheets`

## Why nothing loads on Play

The code layer is present, but the scene still needs Unity object references:

- a `GameBootstrap` in the scene
- a `CombatSceneInstaller`
- a HUD presenter
- a Hero presenter
- 3 Enemy presenters
- 5 Die presenters
- an Overlay controller
- a `PrototypeContentSet` assigned to the bootstrap

Without those scene references, gameplay logic cannot present anything on screen.

## Minimum next step

Create the default assets first. After that, wire the scene objects and assign the generated `PrototypeContentSet.asset`.
