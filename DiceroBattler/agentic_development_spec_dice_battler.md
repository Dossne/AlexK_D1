# Agentic Development Spec — Unity Mobile Dice Battler

## 1. Purpose

This document defines the production-facing specification for a portrait-only Unity combat prototype built for Android, with prefab-swappable presentation, Google Sheets–driven balancing/configuration, and a structure intended to support future agent-assisted content and asset replacement.

The design follows a few core principles:

- keep gameplay logic, content configuration, and presentation separate;
- treat prefab contracts as stable interfaces;
- keep runtime behavior deterministic where possible and configurable where useful;
- support clean handoff between design, engineering, balancing, QA, and future AI-agent workflows;
- publish one underlying gameplay model into several lawful views: runtime systems, config schemas, prefab contracts, validation rules, and acceptance criteria.

## 2. Product Summary

The player controls one fixed hero in encounter-to-encounter turn-based combat.

Between encounters, the hero automatically runs upward as a visual transition only. There is no route choice and no player control during run segments.

Each encounter consists of:

1. encounter spawn;
2. player phase:
   - hero stands idle;
   - currently unlocked dice auto-roll;
   - player may reroll up to 3 times per turn by tapping a die, rerolling the tapped die and all dice to its left;
   - damage is recalculated from the new results;
   - player may attack at any time after the initial roll or any reroll;
3. hero attack phase:
   - hero attacks first;
   - total calculated damage is applied to exactly one target;
4. enemy phase:
   - all surviving enemies attack one by one;
   - after all enemy attacks finish, next-turn enemy intents are rerolled;
5. next player phase begins.

The run ends in either:

- **Defeat**: hero HP reaches 0, hero death animation finishes, defeat screen appears with **Retry** CTA;
- **Victory**: final wave is cleared, win screen appears with **Continue** CTA.

Both CTAs restart the run from the beginning.

## 3. Platform and Technical Constraints

### 3.1 Target platform

- Unity project
- Android build target
- Portrait only
- Touch-first input
- Mouse support in editor for testing

### 3.2 Presentation constraints

- UI should not be generated in code as a primary authoring path.
- UI and combat presentation should be prefab-based.
- Sprite, mesh, animator, and FX assets should be replaceable by swapping prefabs or referenced assets inside prefab contracts.
- The system must support manual asset replacement and future agent-safe replacement workflows.

### 3.3 Performance constraints

Prototype should be authored with mobile-safe assumptions:

- low runtime allocation during combat;
- pooling for swarm VFX, floating damage numbers, and repeated transient UI markers;
- no unnecessary full-screen re-layouts during combat;
- avoid expensive dynamic hierarchy creation during turns;
- allow either real 3D dice or 2D presentation of 3D-rendered dice, as long as the die prefab contract remains consistent.

## 4. Scope Boundaries

### 4.1 In scope

- one fixed hero;
- 1 run only;
- ~30 configured waves;
- 1 to 3 enemies per wave;
- 5 total dice slots;
- progressive dice unlocks only via level-up upgrades;
- combinations and multipliers;
- Google Sheets import to local config assets via manual editor button;
- prefab-based combat scene and UI;
- win/defeat flows;
- validation and failover rules for invalid imported data.

### 4.2 Out of scope for this version

- pause system;
- route choice or map progression;
- multiple heroes/classes;
- meta progression between runs;
- runtime Google Sheets fetch on device;
- chained multi-level-ups from one reward grant;
- status effects;
- skill trees;
- advanced target selection;
- batch enemy spawns within one wave.

## 5. Core Gameplay Rules

## 5.1 Encounter flow

Each encounter starts after an automatic run-up transition.

Enemy wave spawns immediately.

All enemies for the wave appear at once.

The hero and all enemies idle until actions occur.

## 5.2 Turn order

One turn consists of:

1. unlocked dice auto-roll;
2. player optional rerolls, up to remaining reroll count;
3. player taps **Attack**;
4. hero performs one attack animation;
5. one target enemy receives the full final damage in one hit;
6. if the target survives, it returns to idle after hit reaction;
7. if the target dies, it plays death animation, then death cleanup, then EXP reward is granted;
8. after hero attack resolution, every surviving enemy attacks one by one;
9. when all current enemy attacks finish, each surviving enemy rerolls its next-turn intent value;
10. next turn begins.

Hero always attacks first.

## 5.3 Targeting rules

Hero attacks exactly one enemy.

Target priority:

1. frontmost enemy line only;
2. if two front-line enemies exist, target the right one first;
3. if right-front is dead, target left-front;
4. back-line enemies cannot be targeted while any front-line enemy is alive.

No splash, overflow, or spillover damage.

## 5.4 Enemy formation rules

- 1 enemy: front line, centered
- 2 enemies: both in front line, left and right
- 3 enemies: 1 front line centered, 2 back line left and right

## 5.5 Hero and enemy animation states

Hero required animation states:

- Run
- Idle
- Attack
- Hit
- Death

Enemy required animation states:

- Idle
- Attack
- Hit
- Death

Animation behavior:

- idle is default state during decision windows;
- attack plays only during attack execution;
- hit plays when damage is received and unit survives;
- death plays once on lethal damage;
- for hero death, defeat screen appears only after hero death animation completes;
- for enemy death, EXP swarm begins after death event timing defined by prefab contract.

## 6. Dice System

## 6.1 Dice slots

There are always 5 visible dice slots on screen.

At run start:

- exactly 1 die is unlocked;
- 4 slots are visible but locked.

Locked dice remain visible to communicate future progression.

## 6.2 Die states

Each die uses the following design-facing states:

- **Locked** — not unlocked yet; visible as unavailable;
- **Rolling** — currently animating to a value; input blocked;
- **ShowingResult** — die has landed on final value and is in temporary highlight window;
- **Ready** — die has a final value, highlight ended, and can be tapped for reroll if rerolls remain;
- **TurnClosed** — player can no longer interact with dice during hero attack, enemy phase, transitions, result overlays, or any non-player decision state.

Implementation note:

- `ResolvedValue` should exist as runtime data for all non-locked dice after a roll finishes.
- Highlight is both visual and logical because the die is in a short-lived post-roll presentation phase.

## 6.3 Initial roll

At the start of every player phase, all currently unlocked dice roll automatically.

Each die shows a random final value from 1 to 6.

Dice should visually communicate 3D rolling behavior. This may be implemented using:

- actual 3D dice meshes and animation, or
- 2D presentation derived from 3D-rendered animation,

provided the die prefab contract remains stable.

## 6.4 Highlight behavior

When a die completes its roll:

1. it shows its final face value;
2. it becomes highlighted for a short configurable duration;
3. the highlight then disappears;
4. die transitions to Ready.

## 6.5 Reroll rules

Player starts each turn with **3 rerolls**.

Rerolls are optional.

The player may attack without using any rerolls.

To reroll, player taps one unlocked die currently in Ready state.

When tapped:

- the tapped die and all unlocked dice to its left reroll immediately;
- only those selected dice animate and update;
- dice to the right remain unchanged;
- reroll count decreases by 1;
- damage is recalculated from the new full set of current unlocked dice.

If no rerolls remain, tapping dice has no gameplay effect.

## 6.6 Dice input availability

Dice accept tap input only when:

- combat is in player decision phase;
- at least one unlocked die is in Ready;
- no roll animation is currently resolving for the tapped prefix;
- no modal overlay is active;
- rerolls remaining > 0.

After Attack is committed, all unlocked dice enter TurnClosed.

## 7. Damage Calculation

## 7.1 Formula

Damage is calculated in this order:

1. sum all currently unlocked dice values;
2. detect all valid configured combinations using only currently unlocked dice;
3. choose the single valid combination that yields the **best multiplier** for the current unlocked dice count;
4. multiply dice sum by that combination multiplier;
5. add flat final-damage bonus from upgrades;
6. output final damage.

## 7.2 Supported combinations

Supported combination families:

1. One Pair — two dice with same value
2. Two Pair — two separate pairs
3. Three of a Kind — three same values
4. Four of a Kind — four same values
5. Straight — all currently unlocked dice are sequential in any order

Combination detection uses only the currently unlocked dice.

## 7.3 Combination resolution rule

If multiple combinations are valid, use the one that produces the highest multiplier for the current unlocked dice count.

No combination-priority override exists in this version.

The system compares effective configured multiplier values, not a hardcoded hand ranking.

## 7.4 Display format

Total damage text should use this format:

`Damage value: <FinalDamage> <CombinationName> x<CombinationMultiplier> + Bonus <FlatBonus>`

If no combination applies:

`Damage value: <FinalDamage> No Combination x1 + Bonus <FlatBonus>`

If flat bonus is zero, it may still be shown for consistency, or hidden according to UI config. Preferred default is to show it explicitly.

## 7.5 Damage feedback

On damage updates:

- swarm or fly-to feedback should move from dice results toward total damage display;
- if combination is set, combination label appears with highlight;
- multiplier display should shake and animate when changing;
- when attack lands, actual damage number appears both above the target enemy and in the right-side damage panel.

## 8. Combat UI

## 8.1 Layout intent

Combat screen should align with the reference composition:

- top: EXP bar and wave indicator;
- mid: enemy formation and hero lane presentation;
- lower mid: total damage/combo readout and dice strip;
- bottom: reroll counter and attack CTA;
- right side: actual damage dealt panel;
- win/defeat overlays as modal screens.

## 8.2 Top HUD

Must include:

- EXP bar
- current level display if configured in UI skin
- wave text in format `Waves CurrentWave/TotalWave`

## 8.3 Unit HUD

Hero:

- HP bar under hero sprite/model

Enemy:

- HP bar under enemy sprite/model
- intent damage value under HP bar
- damage icon next to intent value

## 8.4 Dice HUD

Must include:

- 5 visible dice slots
- locked state visuals for unavailable dice
- reroll counter under dice in format `current/max` or equivalent
- total damage readout above dice
- attack button under dice

## 8.5 Attack button behavior

Attack button must:

- be available after initial roll;
- remain available after each reroll unless combat is otherwise blocked;
- shake and lighten on tap;
- become non-interactable after commit until next player phase.

## 8.6 Right-side damage panel

Panel text: `Damage dealt: N`

In this version it shows actual damage dealt on hero hit resolution.

A presentation option may preview pending damage separately in future versions, but that is not part of current scope.

## 9. Enemy Intent System

## 9.1 Intent rule

Every living enemy displays the damage it will deal on its next attack.

Intent value is random within configured `damageMin` and `damageMax` for that mob type.

## 9.2 Intent timing

Enemy intent values are rolled:

- on enemy spawn, for their first attack preview;
- immediately after all surviving enemies finish their current turn attacks, for the next player-visible turn preview.

## 9.3 Intent execution

During enemy phase, every surviving enemy attacks once, one after another, using its currently shown intent value.

## 10. HP, Death, EXP, and Level-Up

## 10.1 HP rules

Hero and mobs have configurable HP values.

Healing cannot exceed hero max HP.

## 10.2 Enemy death flow

When an enemy dies:

1. enemy death animation plays;
2. enemy is marked dead and removed from future turn order;
3. kill EXP is granted immediately;
4. EXP swarm flies to the top EXP bar;
5. if this was the last enemy in the wave, then wave-clear EXP bonus is granted;
6. if the resulting EXP fills the bar and the cleared wave is not the final wave, open level-up overlay;
7. after reward or overlay resolution, continue run flow.

## 10.3 Hero death flow

When hero HP reaches 0:

1. hero death animation plays;
2. defeat screen appears after animation completes;
3. Retry CTA restarts run from the beginning.

## 10.4 EXP rules

EXP is granted from:

- each killed mob immediately on death;
- additional wave-clear reward when final enemy in wave dies.

EXP should be tuned so that one reward grant can fill at most one level-up threshold.

No EXP overflow is carried.

No chained multi-level-ups are allowed from one grant sequence.

If final wave is completed and EXP would otherwise trigger a level-up, skip upgrade and show win screen only.

## 10.5 Level-up flow

When EXP bar fills:

- combat stops completely;
- a blocking overlay with 3 upgrade choices is shown;
- player must choose one upgrade;
- after selection, overlay closes and combat/run continues.

## 11. Upgrade System

## 11.1 Current upgrade families

Prototype must support at minimum:

1. Add New Die
2. Restore Hero Health
3. Increase Final Damage Bonus

## 11.2 Future-safe requirement

Upgrade system must be implemented as extensible data-driven families rather than hardcoded one-off buttons.

The architecture must allow additional future upgrades without reworking the selection flow.

## 11.3 Choice generation

Level-up offers are generated as:

- 3 options;
- chosen uniformly at random among currently eligible upgrades;
- duplicates are allowed across the run;
- upgrades may stack;
- die unlock upgrades become ineligible once all dice are unlocked or once that specific unlock target is already consumed.

## 11.4 Add New Die

- unlocks next locked die immediately;
- new die becomes available in the same encounter after upgrade selection;
- future player phases include that die automatically.

## 11.5 Restore Hero Health

- restores configurable percent of max HP;
- value is data-driven from upgrade config;
- healing is capped at max HP.

## 11.6 Increase Final Damage Bonus

- adds configurable flat bonus to final damage formula;
- stacks across repeated selections.

## 12. Waves and Run Structure

## 12.1 Wave count

Run uses fixed configured total wave count, approximately 30.

UI reads total from config, not from auto-counting current table at runtime.

## 12.2 Wave progression

After a wave is resolved and no upgrade overlay blocks progress:

1. automatic run-up transition plays;
2. next wave spawns immediately after transition;
3. next encounter begins.

No route choice, map node, or checkpoint selection exists in this version.

## 12.3 Win flow

On final wave clear:

1. final enemy death resolves;
2. kill EXP and wave bonus EXP may still animate for feel if desired, but must not open upgrade flow;
3. win screen appears;
4. Continue CTA restarts run immediately from the beginning.

## 13. Bounded Runtime Contexts

To keep implementation stable and agent-friendly, the project should be separated into the following bounded contexts.

## 13.1 Combat Rules Context

Owns:

- turn order
- targeting
- damage formula
- win/lose conditions
- encounter progression

Must not own:

- direct prefab layout decisions
- Google Sheets parsing details
- asset replacement logic

## 13.2 Dice Context

Owns:

- die states
- roll/reroll sequencing
- prefix reroll selection
- resolved values
- highlight timing
- reroll budget

Must not own:

- enemy HP
- wave progression
- Google Sheets auth/sync logic

## 13.3 Unit Runtime Context

Owns:

- runtime HP
- death state
- intent value
- attack sequencing hooks
- animation event responses for hero/enemies

## 13.4 Progression Context

Owns:

- EXP accumulation
- level-up threshold evaluation
- upgrade offer generation
- unlock state of dice
- flat damage bonus state

## 13.5 Config Import Context

Owns:

- Google Sheets tab schema mapping
- validation
- import button execution
- creation/updating of local ScriptableObject or JSON cache assets
- fallback defaults

## 13.6 Presentation Context

Owns:

- prefab spawning
- binding runtime state to UI
- VFX and swarm playback
- damage number display
- screen overlays

Must not own gameplay truth.

## 13.7 Prefab Contract Context

Owns:

- stable requirements for swappable prefabs
- required child anchors
- required animator states/events
- validation utilities to check prefab conformity

## 14. Unity Architecture

## 14.1 Recommended Unity project structure

Use a simple, explicit Unity project layout rooted in standard asset folders so both humans and agents can navigate it safely.

```text
Assets/
  Art/
    Sprites/
    UI/
    Backgrounds/
    VFX/
    Materials/
    Fonts/
  Audio/
    Music/
    SFX/
  Animations/
    Hero/
    Enemies/
    Dice/
    UI/
  Prefabs/
    Characters/
      Hero/
      Enemies/
    Dice/
    UI/
      HUD/
      Overlays/
      Widgets/
    VFX/
    Environment/
  Scenes/
    Boot/
    Meta/
    Combat/
    Debug/
  Scripts/
    Runtime/
      Boot/
      Combat/
      Dice/
      Units/
      Progression/
      UI/
      Presentation/
      Config/
      Utilities/
    Editor/
      SheetsImport/
      Validation/
      Tools/
  Configs/
    Imported/
    Defaults/
    Registries/
    Debug/
  Data/
    Generated/
    Reports/
  Resources/
    only_if_strictly_required/
  Gizmos/
  Tests/
    EditMode/
    PlayMode/
```

### 14.1.1 Folder intent

- `Assets/Art/` contains raw visual assets, not gameplay logic.
- `Assets/Prefabs/` contains swappable runtime presentation units.
- `Assets/Scripts/Runtime/` contains gameplay/runtime code only.
- `Assets/Scripts/Editor/` contains importers, validators, and editor-only tooling.
- `Assets/Configs/Imported/` contains imported ScriptableObjects produced from Google Sheets.
- `Assets/Configs/Defaults/` contains safe local fallback assets.
- `Assets/Configs/Registries/` contains prefab registries and lookup assets.
- `Assets/Data/Generated/` contains generated intermediate data when useful.
- `Assets/Data/Reports/` contains validation/import reports.
- `Assets/Scenes/` contains scene assets only.

### 14.1.2 Simplified version

If you want the simplest possible starting structure, this is acceptable too:

```text
Assets/
  Prefabs/
  Scripts/
  Configs/
  Sprites/
  Scenes/
  Animations/
  Audio/
```

But for this project, the recommended production-safe version is the expanded layout above, because it scales better once you add import tools, prefab validators, multiple enemy prefabs, and agent-assisted replacement.

## 14.2 Suggested ScriptableObject / config asset list

Use ScriptableObjects as imported runtime config and registry surfaces.

### 14.2.1 Core imported config assets

- `HeroConfig`
  - hero ID
  - display name
  - max HP
  - starting unlocked dice
  - starting rerolls
  - starting flat damage bonus

- `MobConfig`
  - mob ID
  - display name
  - HP
  - damage min/max
  - EXP reward
  - prefab key

- `MobDatabase`
  - list of `MobConfig`
  - dictionary build/cache by ID at runtime

- `WaveConfig`
  - wave number
  - mob ID list
  - wave clear EXP reward

- `WaveDatabase`
  - ordered list of `WaveConfig`
  - total wave count consistency metadata

- `CombinationMultiplierConfig`
  - combination family enum/string
  - multipliers for unlocked dice count 1..5

- `CombinationDatabase`
  - list of all combination configs

- `UpgradeConfig`
  - ID
  - type
  - title
  - description
  - value
  - eligibleFromLevel
  - enabled
  - targetDiceCount if relevant
  - icon key if relevant

- `UpgradeDatabase`
  - list of all upgrade configs

- `ProgressionLevelConfig`
  - level
  - expToNext

- `ProgressionDatabase`
  - ordered level thresholds

- `RunConfig`
  - run ID
  - total waves
  - total dice slots
  - default rerolls per turn
  - highlight duration
  - transition durations
  - panel durations
  - misc feature flags

### 14.2.2 Registries

- `MobPrefabRegistry`
  - maps `prefabKey -> Enemy prefab`

- `HeroPrefabRegistry`
  - maps hero ID -> Hero prefab

- `UISkinRegistry`
  - HUD prefab
  - upgrade overlay prefab
  - defeat overlay prefab
  - win overlay prefab

- `VfxRegistry`
  - dice-to-damage swarm
  - EXP swarm
  - hit VFX
  - button tap FX
  - multiplier shake/highlight config if split out

### 14.2.3 Tooling assets

- `SheetsImportSettings`
  - sheet source URL / document ID
  - tab mapping
  - output asset paths
  - auth/config if needed later

- `ImportStatusReport`
  - last import timestamp
  - source sheet identifier
  - fatal/warning/info entries
  - imported asset versions/hashes if desired

## 14.3 Suggested runtime class list

Keep classes small and aligned to one responsibility.

### 14.3.1 Boot and composition

- `GameBootstrap`
  - loads required config assets and registries
  - enters initial run flow

- `CombatSceneInstaller`
  - wires scene references, services, presenters, and registries

- `RunSession`
  - stores current run-level mutable state
  - hero runtime state
  - current wave index
  - unlocked dice count
  - flat damage bonus
  - EXP progress

### 14.3.2 Combat domain

- `CombatFlowController`
  - owns top-level combat state machine
  - transitions between spawn, roll, decision, attack, enemy phase, post-wave, overlays

- `TurnController`
  - manages per-turn sequencing

- `TargetingService`
  - resolves legal hero target using front/back and right-first rules

- `DamageCalculationService`
  - sums dice
  - detects valid combinations
  - selects best multiplier
  - applies flat bonus

- `CombinationEvaluationService`
  - pure rule evaluator for combination detection

- `WaveController`
  - spawns current wave
  - detects wave cleared
  - advances wave index

### 14.3.3 Dice domain

- `DiceController`
  - owns all die runtime models for the current turn

- `DieRuntimeModel`
  - unlocked state
  - current state enum
  - resolved value
  - index

- `DiceRollService`
  - random rolling logic
  - prefix reroll selection

- `DiceStatePresenter`
  - pushes state to die prefab presenter

- `RerollBudgetController`
  - tracks remaining rerolls per turn

### 14.3.4 Units domain

- `HeroRuntimeUnit`
  - current HP
  - max HP
  - alive/dead

- `EnemyRuntimeUnit`
  - config reference
  - current HP
  - current intent damage
  - formation slot
  - alive/dead

- `EnemyIntentService`
  - rolls intent damage using min/max

- `UnitAttackResolver`
  - executes one-hit application and timing callbacks

### 14.3.5 Progression domain

- `ExpController`
  - grants kill EXP and wave EXP
  - checks level-up threshold
  - blocks overflow

- `UpgradeSelectionService`
  - computes eligible upgrades
  - picks 3 uniformly at random

- `UpgradeApplyService`
  - unlock die
  - heal percent
  - add flat damage bonus

### 14.3.6 Presentation/UI domain

- `CombatHudPresenter`
  - updates wave text, EXP bar, reroll counter, damage text, button state

- `HeroPresenter`
  - drives hero prefab animation hooks and feedback

- `EnemyPresenter`
  - drives enemy prefab animation hooks and intent display

- `DiePresenter`
  - visual state of one die prefab

- `DamageNumberPresenter`
  - floating damage numbers above targets

- `DamagePanelPresenter`
  - right-side actual damage panel

- `SwarmFxPresenter`
  - dice-to-damage and EXP swarm playback

- `OverlayController`
  - level-up, defeat, win overlays

### 14.3.7 Utility/support

- `RandomService`
  - wrapper for deterministic testing if needed later

- `PrefabContractValidator`
  - editor/runtime-dev validation entry point

- `SafeLogger`
  - structured error and warning utility in dev builds

## 14.4 Suggested combat state machine classes

### 14.4.1 High-level states

Create explicit state classes or a strongly typed enum-driven state machine with handlers for:

- `BootState`
- `RunStartState`
- `EncounterTransitionState`
- `EncounterSpawnState`
- `PlayerRollState`
- `PlayerDecisionState`
- `HeroAttackState`
- `EnemyAttackState`
- `PostWaveResolveState`
- `LevelUpOverlayState`
- `VictoryOverlayState`
- `DefeatOverlayState`
- `RunRestartState`

### 14.4.2 State responsibilities

- `PlayerRollState`
  - rolls all unlocked dice
  - waits until roll/highlight complete
  - recalculates damage
  - enters player decision

- `PlayerDecisionState`
  - enables dice taps and attack button
  - waits for reroll or attack

- `HeroAttackState`
  - closes die input
  - resolves target
  - plays hero attack
  - applies damage on attack event
  - resolves death/EXP consequences

- `EnemyAttackState`
  - iterates surviving enemies in order
  - plays each attack
  - applies shown intent damage
  - if hero dies, branches to defeat
  - after sequence ends, rerolls next-turn intents

- `PostWaveResolveState`
  - grants wave reward if needed
  - checks final wave vs level-up vs next encounter transition

## 14.5 Source of truth strategy

Authoring truth is Google Sheets.

Runtime truth is imported local Unity data assets generated via manual editor import.

The shipped Android build must not depend on live Sheets access.

## 14.6 Import trigger

Import is initiated manually via button on a dedicated ScriptableObject importer asset or equivalent editor tool surface.

Import result should:

- fetch configured sheet tabs;
- validate rows and cross-references;
- generate/update local data assets;
- write validation summary;
- refuse to mark import successful if fatal errors remain.

## 14.7 Prefab validator checklist

The prefab validator should run in editor and check these contracts.

### 14.7.1 Hero prefab validation

Validate:

- root presenter component exists
- Animator exists
- required states exist: `Idle`, `Run`, `Attack`, `Hit`, `Death`
- required hooks/events are configured: `AttackHitFrame`, `HitReactionFinished`, `DeathFinished`
- HP bar anchor exists
- damage anchor exists
- hit FX anchor exists

### 14.7.2 Enemy prefab validation

Validate:

- root presenter component exists
- Animator exists
- required states exist: `Idle`, `Attack`, `Hit`, `Death`
- required hooks/events exist: `AttackHitFrame`, `HitReactionFinished`, `DeathFinished`
- HP bar anchor exists
- intent anchor exists
- damage anchor exists

### 14.7.3 Die prefab validation

Validate:

- root `DiePresenter` exists
- tap target exists
- visual root exists
- result display binding exists
- highlight binding exists
- lock binding exists or a declared no-lock-visual mode exists
- roll lifecycle hooks exist: `RollStarted`, `RollFinished`, `HighlightFinished`
- all required visual states are supported

### 14.7.4 HUD validation

Validate references for:

- EXP bar
- wave label
- reroll label
- total damage label
- attack button
- right damage panel

### 14.7.5 Overlay validation

Validate:

- level-up overlay has 3 choice slots
- defeat overlay has Retry CTA
- win overlay has Continue CTA

### 14.7.6 Registry validation

Validate:

- every `prefabKey` in `Mobs` resolves in `MobPrefabRegistry`
- hero config resolves in `HeroPrefabRegistry`
- required overlays exist in `UISkinRegistry`

## 15. Google Sheets Data Model

The project should define exact tabs as follows.

## 15.1 `Hero`

Purpose: fixed hero base data.

Required columns:

- `id` — string, unique, recommended `hero_main`
- `displayName` — string
- `maxHp` — int, > 0
- `startingUnlockedDice` — int, required, default 1, valid range 1..5
- `startingRerollsPerTurn` — int, required, default 3, valid range 0..10
- `startingFlatDamageBonus` — int, required, default 0, valid range >= 0
- `baseRunId` — string, optional, default `run_01`

Rules:

- exactly one hero row for this prototype;
- `startingUnlockedDice` must be 1 in current content unless deliberately overridden;
- `startingRerollsPerTurn` should be 3 in current content;
- `maxHp` required.

Fail on:

- missing hero row;
- duplicate `id`;
- non-positive `maxHp`.

## 15.2 `Mobs`

Purpose: enemy definitions and unit balancing.

Required columns:

- `id` — string, unique
- `displayName` — string
- `hp` — int, > 0
- `damageMin` — int, >= 0
- `damageMax` — int, >= `damageMin`
- `expReward` — int, >= 0
- `prefabKey` — string, required for prefab lookup

Optional columns:

- `notes` — string

Rules:

- `damageMin` and `damageMax` define intent roll range;
- `prefabKey` must resolve to a registered prefab or asset mapping.

Fail on:

- duplicate `id`;
- invalid damage range;
- missing referenced mob in waves.

## 15.3 `Waves`

Purpose: fixed encounter sequence.

Required columns:

- `waveNumber` — int, unique, starts at 1
- `mobList` — string, comma-separated mob IDs, example `slime,slime,archer`
- `expReward` — int, >= 0, wave-clear bonus EXP

Optional columns:

- `notes` — string

Rules:

- number of rows must equal configured total wave count or importer must reject mismatch;
- `mobList` must resolve to 1..3 enemies;
- all mobs in `mobList` must exist in `Mobs`;
- spawn is immediate; no spawn timing syntax allowed in this version.

Fail on:

- missing wave numbers;
- duplicate wave numbers;
- wave with 0 enemies;
- wave with > 3 enemies;
- unresolved mob ID.

## 15.4 `Combinations`

Purpose: combination multipliers by unlocked dice count.

Required columns:

- `combination` — enum-like string, one of:
  - `OnePair`
  - `TwoPair`
  - `ThreeOfAKind`
  - `FourOfAKind`
  - `Straight`
- `1` — float or decimal multiplier for 1 unlocked die
- `2` — multiplier for 2 unlocked dice
- `3` — multiplier for 3 unlocked dice
- `4` — multiplier for 4 unlocked dice
- `5` — multiplier for 5 unlocked dice

Rules:

- all 5 combination rows must exist;
- columns `1..5` represent multiplier by current unlocked dice count;
- values should be >= 1 unless explicitly allowing weaker combos;
- importer should permit decimal values.

Recommended default:

- invalid or impossible combo at low dice counts may be set to `1` rather than left blank.

Fail on:

- missing combination family;
- duplicate combination family;
- non-numeric multiplier.

## 15.5 `Upgrades`

Purpose: upgrade definitions and random offer pool.

Required columns:

- `id` — string, unique
- `type` — enum-like string:
  - `UnlockDie`
  - `HealPercent`
  - `FlatDamageBonus`
- `title` — string
- `description` — string
- `value` — number, interpretation depends on type
- `eligibleFromLevel` — int, default 1
- `enabled` — bool

Optional columns:

- `targetDiceCount` — int, required for `UnlockDie`, indicates resulting unlocked die count after applying upgrade
- `uiIconKey` — string
- `notes` — string

Rules:

- selection is uniform random among eligible rows;
- no weight field is used in this version;
- `UnlockDie` rows become ineligible once their target is reached or exceeded;
- `HealPercent.value` is percent of max HP;
- `FlatDamageBonus.value` is additive final damage bonus.

Fail on:

- duplicate `id`;
- invalid type;
- missing `targetDiceCount` for `UnlockDie`;
- `targetDiceCount` outside 2..5;
- non-positive or invalid `value` for enabled rows.

## 15.6 `Progression`

Purpose: level-up thresholds and run-global progression settings.

Required columns:

- `level` — int, unique, starts at 1
- `expToNext` — int, > 0

Optional columns:

- `notes` — string

Rules:

- values should be tuned so no single reward event sequence causes chained level-ups;
- importer should warn if any `expToNext` is lower than the maximum possible reward event for a single resolved grant window.

Fail on:

- duplicate level;
- non-positive threshold.

## 15.7 `RunConfig`

Purpose: run-global constants.

Required columns:

- `key`
- `value`

Required keys:

- `runId`
- `totalWaves`
- `diceSlotsTotal` (must be 5)
- `rerollsPerTurnDefault` (should be 3)
- `diceHighlightDuration`
- `postWaveRunTransitionDuration`
- `damagePanelDisplayDuration`

Optional keys:

- `showBonusWhenZero`
- `lockedDiceVisible`

Rules:

- `totalWaves` must match wave row count;
- `diceSlotsTotal` must be 5 in current prototype;
- values must parse to declared runtime types.

## 16. Import Contract and Validation

## 16.1 Import workflow

Manual import button should execute this sequence:

1. connect to configured Google Sheet source;
2. read required tabs;
3. normalize raw rows;
4. validate schema-level rules;
5. validate cross-tab references;
6. generate local typed data assets;
7. write import report asset or log;
8. mark import successful only if no fatal errors occurred.

## 16.2 Validation classes

Validation output should classify issues as:

- **Fatal** — import fails, runtime assets not updated as successful build data
- **Warning** — import succeeds, but report includes issue
- **Info** — non-blocking guidance

## 16.3 Fatal validation examples

- missing required tab
- missing required column
- invalid enum value
- duplicate primary ID
- unresolved mob in wave
- total waves mismatch
- hero row missing
- missing prefabKey mapping for enabled mob

## 16.4 Warning examples

- combination multiplier of 1 for a complex combo
- progression thresholds that look poorly tuned
- unused mob definition
- unlocked die upgrade missing friendly title style consistency

## 16.5 Failover behavior

If import fails:

- previously valid local config remains active;
- importer must not partially overwrite active runtime data with incomplete new data;
- user receives readable validation report;
- build pipeline should detect stale or invalid config status if integrated later.

If runtime somehow encounters invalid data despite import validation:

- log error with data source reference;
- use safe fallback defaults where possible;
- never hard crash combat flow if a recoverable fallback exists.

Recoverable fallback examples:

- missing combination multiplier → use `1`
- missing damage panel display duration → use hardcoded safe default
- missing optional note/icon → continue silently

Non-recoverable runtime examples:

- wave has no enemies
- hero data missing
- no valid dice slot count

These should stop entry into combat scene with a visible development error surface in editor builds.

## 17. Prefab Contracts

Prefab contracts are stable interfaces. Asset style may change; contract shape may not.

## 17.1 Hero prefab contract

Required components/anchors:

- root runtime presenter component
- animator
- render root or sprite root
- HP bar anchor
- floating damage anchor
- hit FX anchor
- optional weapon trail anchor

Required animator states:

- `Idle`
- `Run`
- `Attack`
- `Hit`
- `Death`

Required animation events or equivalent hooks:

- `AttackHitFrame`
- `HitReactionFinished`
- `DeathFinished`

## 17.2 Enemy prefab contract

Required components/anchors:

- root runtime presenter component
- animator
- render root or sprite root
- HP bar anchor
- intent display anchor
- floating damage anchor
- hit FX anchor

Required animator states:

- `Idle`
- `Attack`
- `Hit`
- `Death`

Required hooks:

- `AttackHitFrame`
- `HitReactionFinished`
- `DeathFinished`

## 17.3 Die prefab contract

Required components/anchors:

- root die presenter component
- visual root
- value display binding or face-selection binding
- highlight visual binding
- tap target
- optional lock overlay binding

Required hooks:

- `RollStarted`
- `RollFinished`
- `HighlightFinished`

Required visual state support:

- Locked
- Rolling
- ShowingResult
- Ready
- TurnClosed

## 17.4 HUD prefab contract

Required bindings:

- EXP bar
- wave text
- reroll counter text
- total damage text
- bonus text or inline display binding
- attack button
- right-side damage panel

## 17.5 Overlay prefab contract

Required overlays:

- level-up selection overlay
- defeat screen with Retry CTA
- win screen with Continue CTA

## 17.6 Validation utility

Editor validation tooling should be able to inspect prefabs and assert required components, anchors, animator states, and hook names.

A prefab that fails contract validation should be reported before runtime.

## 18. Agent-Safe Asset Replacement Rules

The project must support prompt-driven asset replacement like: “in this folder use this asset for this prefab.”

To make that safe, the following rules apply.

## 18.1 Stable semantic ownership

Agents may replace:

- visual assets
- child renderers
- sprites/materials/meshes
- VFX references
- prefab references in registries

Agents may not silently change:

- gameplay scripts
- binding component names/types
- required anchors
- animator state names required by contract
- event hook names without updating validator and all consumers together

## 18.2 Replacement granularity

Preferred replacement levels:

1. swap referenced art asset inside existing prefab;
2. swap child visual prefab under stable presenter root;
3. swap full prefab only if the new prefab passes contract validation.

## 18.3 Registry requirement

All runtime-spawned prefabs should be resolved through registries or typed references, not by ad-hoc scene searches.

Recommended registries:

- hero prefab registry
- mob prefab registry keyed by `prefabKey`
- HUD skin registry
- overlay registry
- VFX registry

## 18.4 Agent workflow guardrails

Any agent-driven prefab replacement operation should:

1. identify target contract type;
2. load replacement candidate;
3. validate against required components and animator states;
4. preserve stable presenter component or add required bridge adapter;
5. only commit replacement if validation passes;
6. report what changed and what was left unchanged.

## 18.5 Replacement failure behavior

If replacement fails validation:

- do not apply it automatically;
- emit a readable report naming missing anchors, states, or bindings.

## 19. Runtime State Machine

## 19.1 High-level run states

- Boot
- RunStart
- EncounterTransitionRun
- EncounterSpawn
- PlayerRoll
- PlayerDecision
- HeroAttackResolve
- EnemyAttackResolve
- PostWaveResolve
- LevelUpOverlay
- VictoryOverlay
- DefeatOverlay
- RunRestart

## 19.2 Transition rules

- `Boot -> RunStart`
- `RunStart -> EncounterTransitionRun`
- `EncounterTransitionRun -> EncounterSpawn`
- `EncounterSpawn -> PlayerRoll`
- `PlayerRoll -> PlayerDecision`
- `PlayerDecision -> HeroAttackResolve` on Attack tap
- `HeroAttackResolve -> EnemyAttackResolve` if enemies remain and hero alive
- `HeroAttackResolve -> PostWaveResolve` if wave cleared
- `EnemyAttackResolve -> DefeatOverlay` if hero died
- `EnemyAttackResolve -> PlayerRoll` if hero alive and enemies remain
- `PostWaveResolve -> LevelUpOverlay` if EXP filled and not final wave
- `PostWaveResolve -> VictoryOverlay` if final wave cleared
- `PostWaveResolve -> EncounterTransitionRun` otherwise
- `LevelUpOverlay -> EncounterTransitionRun`
- `VictoryOverlay -> RunRestart`
- `DefeatOverlay -> RunRestart`
- `RunRestart -> RunStart`

## 20. Observability and Debugging

Prototype should expose lightweight debug data in editor builds:

- current wave
- hero HP
n- unlocked dice count
- current dice values
- current best combination
- rerolls remaining
- flat damage bonus
- selected target ID
- each enemy intent
- config asset version/import timestamp

## 21. QA Acceptance Criteria

## 21.1 Combat acceptance

- Hero always attacks before enemies.
- All surviving enemies attack once per enemy phase.
- Hero damage always hits exactly one valid target.
- Back-line enemies are untargetable while front enemy exists.
- If two front enemies exist, right one is targeted first.

## 21.2 Dice acceptance

- Run starts with exactly 1 unlocked die and 4 visible locked dice.
- Initial player phase auto-rolls all unlocked dice.
- Tapping die `N` rerolls die `N` and all unlocked dice to its left only.
- Dice to the right remain unchanged on reroll.
- Player has 3 rerolls per turn.
- Player can attack immediately after initial roll without rerolling.

## 21.3 Combination acceptance

- Only currently unlocked dice are considered for combinations.
- Best multiplier only is applied.
- Multipliers come from imported combination table by unlocked dice count.
- Final damage includes flat bonus after multiplier.

## 21.4 Progression acceptance

- Enemy kill grants mob EXP immediately.
- Clearing a wave grants wave bonus EXP.
- Level-up overlay blocks combat.
- Upgrade choices are chosen uniformly among eligible upgrades.
- Unlock-die upgrade becomes unavailable once consumed/reached.
- No chained multi-level-ups occur from a single reward resolution.

## 21.5 Result flow acceptance

- Hero death animation finishes before defeat screen appears.
- Final wave clear shows win screen, not upgrade overlay.
- Retry restarts run from beginning.
- Continue on win restarts run from beginning.

## 21.6 Data acceptance

- Import fails on unresolved mob references or malformed required tabs.
- Old valid config remains active after failed import.
- Total wave count matches config.
- Prefab validation catches missing required animator states.

## 22. Implementation Notes for First Vertical Slice

The first vertical slice should prove the following end-to-end loop:

1. import sheets into local config;
2. load combat scene;
3. spawn hero and one simple enemy;
4. auto-roll one unlocked die;
5. allow reroll;
6. compute damage with imported combination table;
7. attack enemy;
8. enemy attacks back using intent;
9. kill enemy, grant EXP, open upgrade overlay;
10. select unlock-die upgrade;
11. next encounter begins with 2 unlocked dice.

This slice is the minimum viable example for validating the architecture before scaling to all waves and art swaps.

## 23. Open Design Decisions Explicitly Deferred

These are intentionally deferred and should not be silently improvised during implementation:

- pause system
- meta progression
- route/map layer
- weighted upgrade rolls
- status effects
- multi-hit hero attacks
- predictive right-side damage panel
- runtime online config fetch
- advanced accessibility settings

## 24. Delivery Expectation for Agents and Humans

Any future implementation pass should preserve the distinction between:

- design-time description: config schemas, prefab contracts, importer rules;
- runtime execution: combat turns, rolls, attacks, rewards;
- publication surfaces: design docs, ScriptableObjects, prefabs, validation reports.

No implementation should collapse these layers by hiding gameplay truth inside art prefabs, scene-only wiring, or ad-hoc spreadsheet parsing in combat scripts.

