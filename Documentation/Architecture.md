# DEAD LETTER — Foundation Architecture

## Current project baseline

- Unity Editor: **6000.0.58f2** (Unity 6)
- Render pipeline: Universal Render Pipeline 17.0.4
- Input: Unity Input System 1.14.2
- Existing playable scene: `Assets/Scenes/SampleScene.unity`
- Current build list: only `SampleScene`

This repository is an untouched Unity URP starter project. The folders below keep game-owned content separate from Unity's template assets while leaving gameplay implementation for a later milestone.

## Asset layout

```text
Assets/
  _Project/                         # All DEAD LETTER-owned runtime assets
    Art/                             # Source art, textures, models and animation assets
    Audio/                           # Music, ambience, SFX and mixers
    Materials/                       # Shared materials and shaders
    Prefabs/
      Characters/                    # Player, complainants and future NPCs
      Environment/                   # Office and mail-room set pieces
      Interactables/                 # Computer, stamp station, bins and letters
      UI/                            # Reusable UI prefabs
    Scenes/                          # Bootstrap, Lobby, Office and PostRoom scenes
    ScriptableObjects/               # Configurable data assets; no hard-coded content tables
    Scripts/
      Core/                          # Game lifetime, scene loading and high-level game state
      Player/                        # Locomotion, camera and player-owned state
      Interaction/                   # Focus detection, prompts and interaction contracts
      LetterWork/                    # Letter-work domain models and workflow coordination
      UI/                            # UI presenters bound to runtime state
      World/                         # NPC and environment-specific behaviour
    UI/                              # UI Documents, themes and other source UI assets
    VFX/                             # Particles and post-process support assets
  Scenes/                            # Legacy starter scene only; do not add game scenes here
  Settings/                          # Existing URP settings
```

## Proposed scene map

| Scene | Responsibility | Loaded mode |
| --- | --- | --- |
| `Bootstrap` | Creates persistent composition root, save/session state, audio and scene loader. Contains no gameplay geometry. | Initial / single |
| `Lobby` | Title, settings and game-start entry point. | Single |
| `Office` | Narrative reception area: arrivals, complainant hand-off, tension and transition into work. | Single |
| `PostRoom` | Main mail-processing environment: terminal, stamping desk and country mail bins. | Single |

`Bootstrap` owns services for the duration of the application. Gameplay scenes own only their scene-local objects. If a later loading screen is needed, it becomes a separate additive presentation scene; gameplay scenes should not depend on one another by object reference.

## Player and interaction design

```text
Input actions
  -> PlayerController (move / look)
  -> PlayerInteractionController (query current focus)
  -> IInteractable.TryInteract(context)
  -> feature-specific controller
  -> UI presenter / world feedback
```

- **Player**: one `PlayerController` coordinates input and character movement; a separate camera/look component owns view rotation. Neither knows about letters, countries or UI panels.
- **Focus**: `PlayerInteractionController` raycasts or spherecasts from the player camera, selects the nearest valid `IInteractable`, and exposes its prompt data to the UI layer.
- **Contract**: `IInteractable` represents the common operation boundary: availability check, display prompt, and an interaction request that receives a small `InteractionContext` (interactor, camera, optional carried item). Concrete objects implement it: letter, terminal, stamp station and mail bin.
- **Feature controllers**: interactions notify a workflow service or feature controller rather than changing other scene objects directly. This makes a keyboard/computer UI, physical interaction, and future alternate controls all feed the same workflow.
- **UI**: a prompt presenter observes focused interaction state. Screens such as the terminal are presenters that request actions from their controller; they do not decide whether the action is valid.

The first implementation should create these contracts and a harmless focus/prompt prototype before implementing individual task objects.

## Letter-work domain design

The work system is modelled as data plus a coordinator, instead of a long chain of scene-object calls.

```text
LetterDefinition (authored immutable data)
        +
LetterWorkSession (runtime progress for one assigned letter)
        |
        v
LetterWorkCoordinator -- validates --> LetterWorkStep
        |                                  |
        +--> terminal / stamp / bin adapters +--> UI and world feedback
```

### Script responsibilities for the future implementation

| Area | Planned types | Responsibility |
| --- | --- | --- |
| Data | `LetterDefinition`, `CountryDefinition`, `MailBinDefinition` | ScriptableObject content: identifiers, visuals and rules; no scene references. |
| Runtime | `LetterWorkSession`, `LetterWorkStep`, `LetterWorkResult` | A serializable session and explicit progress states for the current letter. |
| Rules | `LetterWorkCoordinator`, `ILetterWorkRules` | Receives requests, validates prerequisites, changes the session and emits results. |
| Scene adapters | `LetterPickupInteractable`, `PostalTerminalInteractable`, `StampStationInteractable`, `MailBinInteractable` | Translate a scene interaction into one request to the coordinator. |
| Presentation | `LetterWorkPresenter`, `TerminalPresenter`, `TaskPromptPresenter` | Display state and feedback only. |

### Workflow states

1. `AwaitingComplainant`
2. `AwaitingLetterPickup`
3. `CarryingLetter`
4. `AwaitingCountryLookup`
5. `AwaitingCountrySelection`
6. `AwaitingStamp`
7. `AwaitingMailBin`
8. `Completed` (or `Failed` for later consequences)

The coordinator permits only valid transitions. For example, a mail bin interaction before a country selection returns a rejection result rather than silently changing state. Country-specific content is configured using IDs in data assets, so adding a country or bin later does not require changing player or interaction code.

## First implementation boundary

This foundation intentionally does **not** include player movement, NPC spawning, task progression, terminal UI, stamping, or bin placement. The next gameplay milestone can implement the contracts and one vertical slice using `PostRoom`, while retaining this separation of data, rules, scene adapters and presentation.
