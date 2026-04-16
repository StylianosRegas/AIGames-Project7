# Capture the Treasure — Unity Implementation Guide

**CST-415 | Project 7 | Bayesian Network Guard AI**  
Alberto Felix Castro · Ysabelle Trinidad · Stylianos Regas  
Unity Engine 6000.2.10f1 · C# · uGUI / TextMeshPro

---

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [Script Reference](#2-script-reference)
3. [Unity Project Setup](#3-unity-project-setup)
4. [Scene Assembly — Step by Step](#4-scene-assembly--step-by-step)
5. [Configuring the Tile Map](#5-configuring-the-tile-map)
6. [Adding and Configuring Guards](#6-adding-and-configuring-guards)
7. [Building the HUD](#7-building-the-hud)
8. [Fog of War (Optional)](#8-fog-of-war-optional)
9. [BN Demo Scene (Plan B Fallback)](#9-bn-demo-scene-plan-b-fallback)
10. [Tuning the Bayesian Network CPTs](#10-tuning-the-bayesian-network-cpts)
11. [Testing Checklist](#11-testing-checklist)
12. [How the AI Works (Quick Reference)](#12-how-the-ai-works-quick-reference)
13. [Troubleshooting](#13-troubleshooting)

---

## 1. Project Overview

Capture the Treasure is a top-down 2D stealth game where the player must
retrieve a treasure from a guarded facility and escape without being caught.

Guards are driven by a **Bayesian Network (BN)** with **Variable Elimination (VE)**
inference. Each guard tick:

1. **Sensors** read NoiseLevel, LightExposure, and VisualContact from the scene.
2. **Variable Elimination** computes `P(GuardAlertState | evidence)`.
3. The guard transitions to the state with the highest posterior probability.
4. **A\*** pathfinding navigates the guard toward its target tile.

---

## 2. Script Reference

| File | Folder | Purpose |
|------|--------|---------|
| `BayesNet.cs` | `Scripts/AI/` | BN node definitions + full CPT tables |
| `VariableElimination.cs` | `Scripts/AI/` | VE inference engine |
| `GuardController.cs` | `Scripts/AI/` | Guard FSM, inference loop, A\* navigation |
| `Sensors.cs` | `Scripts/Sensors/` | NoiseSensor, LightSensor, VisionSensor |
| `AStar.cs` | `Scripts/Pathfinding/` | A\* on the tile grid |
| `TileMap.cs` | `Scripts/Map/` | Walkability grid, coordinate conversion |
| `FogOfWar.cs` | `Scripts/Map/` | Fog-of-war Tilemap overlay |
| `PlayerController.cs` | `Scripts/Core/` | WASD movement, sneak, treasure pickup |
| `GameManager.cs` | `Scripts/Core/` | Win/lose state, singleton |
| `BNDemoScene.cs` | `Scripts/Core/` | Standalone BN demo (Plan B) |
| `HUD.cs` | `Scripts/UI/` | SuspicionMeter + DebugPanel |

---

## 3. Unity Project Setup

### 3.1 Create the Project

1. Open **Unity Hub** → **New Project**.
2. Select the **2D (Built-in Render Pipeline)** template.
3. Name the project `CaptureTheTreasure`.
4. Click **Create Project**.

### 3.2 Install TextMeshPro

1. **Window → Package Manager**.
2. Search for **TextMeshPro** → Install.
3. When prompted, click **Import TMP Essential Resources**.

### 3.3 Import Scripts

1. In the **Project** panel, navigate to `Assets/`.
2. Create this folder structure:
   ```
   Assets/
   └── Scripts/
       ├── AI/
       ├── Core/
       ├── Map/
       ├── Pathfinding/
       ├── Sensors/
       └── UI/
   ```
3. Copy each `.cs` file into its matching folder.

> **Note:** Unity will compile all scripts automatically. Fix any compile
> errors before proceeding to scene setup.

### 3.4 Configure Layers and Tags

Go to **Edit → Project Settings → Tags and Layers**. Add these tags:

| Tag | Used By |
|-----|---------|
| `Wall` | All wall/obstacle colliders |
| `LitFull` | Full-brightness light zone triggers |
| `LitPartial` | Partial-brightness light zone triggers |
| `Treasure` | The treasure pickup object |
| `Exit` | The exit zone trigger |

---

## 4. Scene Assembly — Step by Step

### 4.1 Camera

1. Select the **Main Camera** in the Hierarchy.
2. Set **Projection** → **Orthographic**.
3. Set **Size** → `10` (adjust based on map size).
4. Set **Position** → `(0, 0, -10)`.

### 4.2 Tilemaps (Map Layer)

1. **Right-click Hierarchy → 2D Object → Tilemap → Rectangular**.
   Unity creates a `Grid` with a child `Tilemap`. Rename the child to **GroundLayer**.
2. Repeat to add a second Tilemap named **WallLayer**.
3. On WallLayer's `TilemapCollider2D` (add if missing), set the tag to `Wall`.
4. Paint your map using the **Tile Palette** (Window → 2D → Tile Palette).
   - Ground tiles on GroundLayer.
   - Wall tiles on WallLayer.

### 4.3 TileMap Singleton

1. **Right-click Hierarchy → Create Empty**. Name it `TileMap`.
2. Attach the `TileMap` script.
3. Set **Grid Width** and **Grid Height** to match your painted map.
4. Set **World Origin** to `(0, 0)` (or the bottom-left corner of your grid).
5. Check **Auto Build On Start** → the script will auto-detect wall tiles.

### 4.4 GameManager

1. **Create Empty** → name it `GameManager`.
2. Attach the `GameManager` script.
3. Leave panel references empty for now (assign after building the UI).

### 4.5 Player

1. **Right-click → 2D Object → Sprite** → name it `Player`.
2. Set the sprite to a small blue circle or character sprite.
3. Add a **Rigidbody2D** → set **Body Type: Kinematic**, **Gravity Scale: 0**.
4. Add a **CircleCollider2D** → set Radius to `0.4`.
5. Attach the `PlayerController` script.

### 4.6 Treasure

1. **Right-click → 2D Object → Sprite** → name it `Treasure`.
2. Use a gold/star sprite.
3. Add a **CircleCollider2D** → enable **Is Trigger**.
4. Set the tag to `Treasure`.
5. Place it somewhere in the map.

### 4.7 Exit Zone

1. **Right-click → 2D Object → Sprite** or **Create Empty** → name it `Exit`.
2. Add a **BoxCollider2D** → enable **Is Trigger**.
3. Set the tag to `Exit`.
4. Place it at one edge of the map.

### 4.8 Light Zones

For each lit area in your map:

1. **Create Empty** → name it `LightZone_Full` (or `Partial`).
2. Add a **BoxCollider2D** → enable **Is Trigger**.
3. Set tag to `LitFull` or `LitPartial`.
4. Resize the collider to cover the lit tile area.

---

## 5. Configuring the Tile Map

The `TileMap` script scans the scene for `Wall`-tagged colliders to build the
walkability grid. Make sure:

- All solid wall Tilemaps have the `Wall` tag set on the **Tilemap** GameObject
  (not individual tiles — the whole Tilemap component gets the tag).
- The `TilemapCollider2D` component is present on WallLayer.
- **Grid Width / Grid Height** in the `TileMap` Inspector matches your map dimensions exactly.

To verify: enter **Play Mode**, then select the `TileMap` GameObject and look at
the Scene view gizmos — green = walkable, red = blocked.

---

## 6. Adding and Configuring Guards

### 6.1 Create a Guard Prefab

1. **Create Empty** → name it `Guard`.
2. Add a child **Sprite** named `GuardSprite` (use a red triangle or arrow sprite).
3. Attach these components to the **Guard** root:
   - `GuardController`
   - `NoiseSensor`
   - `LightSensor`
   - `VisionSensor`
   - `Rigidbody2D` (Kinematic, Gravity Scale 0)
   - `CircleCollider2D` (radius 0.4, NOT trigger — guards are solid)
4. Add a child **TextMeshPro - Text** named `StateLabel`.
   - Set font size to 3, alignment Center, color white.
   - Position it above the sprite: local Y ≈ 0.8.

### 6.2 Assign Inspector References

Select the **Guard** root and fill in the Inspector:

| Field | Assign |
|-------|--------|
| `Player` | Drag the Player GameObject |
| `State Label` | Drag the child StateLabel TextMeshPro |
| `Patrol Waypoints` | Add 2–4 Transform references (see below) |
| `NoiseSensor → Player` | Drag the Player GameObject |
| `LightSensor → Player` | Drag the Player GameObject |
| `VisionSensor → Player` | Drag the Player GameObject |
| `VisionSensor → Wall Layer` | Select the `Wall` layer |

### 6.3 Create Patrol Waypoints

1. For each guard, create **2–4 empty GameObjects** named `Waypoint_1`, `Waypoint_2`, etc.
2. Position them along the desired patrol path.
3. Drag them into the guard's **Patrol Waypoints** list in the Inspector.

### 6.4 Vision Sensor Tuning

| Field | Recommended Value | Notes |
|-------|------------------|-------|
| Vision Range | 8 | World units |
| Vision Half Angle | 45° | Total cone = 90° |
| Hearing Radius | 10 | NoiseSensor range |
| Detection Radius | 15 | LightSensor range |

### 6.5 Duplicate for Multiple Guards

1. Right-click the Guard GameObject → **Prefab → Create Prefab**.
2. Drag the prefab into the scene multiple times.
3. For each guard, assign unique Patrol Waypoints.

---

## 7. Building the HUD

### 7.1 Create the Canvas

1. **Right-click Hierarchy → UI → Canvas**.
2. Set **Render Mode** → **Screen Space — Overlay**.
3. Add a **CanvasScaler** component → set to **Scale With Screen Size**,
   reference resolution `1920 × 1080`.

### 7.2 Suspicion Meter (Top-Right)

1. **Right-click Canvas → UI → Panel** → name it `SuspicionPanel`.
2. Anchor it to the **top-right** corner. Size: ~400 × 80 px.
3. Inside SuspicionPanel, add:
   - **TextMeshProUGUI** named `AlertLabel` (text: "Nearest Guard Alert Level")
   - **Slider** named `MeterSlider` (Min 0, Max 1, not interactable)
     - On the slider, set the **Fill** image color to green.
   - **TextMeshProUGUI** named `StateText` (text: "PATROLLING")
4. Attach `SuspicionMeter` to SuspicionPanel.
5. Assign in Inspector:
   - `Meter Slider` → the Slider
   - `Meter Fill` → the Fill image (child of Slider → Fill Area → Fill)
   - `State Text` → the StateText label
   - `Guards` → drag all Guard GameObjects into the list

### 7.3 Debug Panel (Tab Toggle)

1. **Right-click Canvas → UI → Panel** → name it `DebugPanel`.
2. Anchor center, size ~600 × 400 px. Set background alpha to ~200.
3. Add a **TextMeshProUGUI** inside it named `DebugText` (font size 14, monospace if available).
4. Attach `DebugPanel` to the DebugPanel GameObject.
5. Assign in Inspector:
   - `Suspicion Meter Ref` → the SuspicionPanel's SuspicionMeter component
   - `Debug Text` → the DebugText label

> The panel is hidden by default. Press **Tab** in Play Mode to toggle it.

### 7.4 Game Over & Win Panels

1. Create two UI Panels: `GameOverPanel` and `WinPanel`.
2. Each should contain a TextMeshProUGUI message and a Button.
3. On each Button's **OnClick**:
   - `GameManager.RestartGame()` for the Restart button.
   - `GameManager.QuitGame()` for the Quit button.
4. Assign both panels to the `GameManager` in the Inspector.

---

## 8. Fog of War (Optional)

If you want the fog-of-war effect:

1. Add a third Tilemap named `FogLayer` above WallLayer (**Order in Layer: 10**).
2. Create a solid dark tile asset (black/grey square) in your Tiles folder.
3. Flood-fill the entire FogLayer with this dark tile to start.
4. Attach `FogOfWar` to the FogLayer GameObject.
5. Assign in Inspector:
   - `Player Transform` → the Player's Transform
   - `Fog Tilemap` → the FogLayer Tilemap component
   - `Wall Layer` → the Wall layer mask
   - `Fog Tile` → your dark tile asset
   - Set `Grid Width` / `Grid Height` to match your TileMap settings.

> **Alternative:** If this is too complex, skip fog of war entirely.
> The game is fully playable without it.

---

## 9. BN Demo Scene (Plan B Fallback)

If guard integration breaks close to the deadline, use the standalone demo:

1. **File → New Scene** → name it `BNDemo`. Save it.
2. Add a **Canvas** with:
   - Three **TMP_Dropdown** elements:
     - `NoiseDropdown` — options: None, Low, High
     - `LightDropdown` — options: None, Partial, Full
     - `VisionDropdown` — options: False, True
   - A **Button** labeled "Run Inference"
   - A **TextMeshProUGUI** named `OutputText` (large, centered)
3. **Create Empty** → name it `BNDemo` → attach `BNDemoScene` script.
4. Assign all three dropdowns, the button, and the output text in the Inspector.
5. Add the BNDemo scene to **File → Build Settings** scenes list.

In Play Mode, change the dropdowns and click **Run Inference** to see the
Variable Elimination output live on screen and in the Console.

---

## 10. Tuning the Bayesian Network CPTs

All CPT values are defined in `BayesNet.cs` inside `GuardBayesNet.BuildNetwork()`.
Modify them to adjust guard behavior:

### Key CPT rows to tune

```
GuardAlertState CPT (in BayesNet.cs → AddAlertRow calls):

AddAlertRow(noise, light, vision, patrolProb, investigateProb, chaseProb)
```

| Scenario | Current Values | Effect |
|----------|---------------|--------|
| VisualContact=True | chase=0.95–0.99 | Guard almost always chases on sight |
| NoiseLevel=High, Vision=False | investigate=0.65–0.75 | High noise → usually investigate |
| NoiseLevel=None, Vision=False | patrol=0.70–0.90 | Quiet → stays patrolling |

**Rules when editing CPTs:**
- Each row (same noise + light + vision combination) **must sum to exactly 1.0**.
- Higher `chaseProb` = more aggressive guard.
- Higher `investigateProb` = more cautious/curious guard.

### NoiseSensor thresholds (in Sensors.cs)

```csharp
public float StillThreshold = 0.05f;  // below this speed → NoiseLevel = "None"
public float WalkThreshold  = 2.5f;   // below this speed → NoiseLevel = "Low"
                                       // above WalkThreshold → NoiseLevel = "High"
```

Set `PlayerController.SneakSpeed` below `WalkThreshold` so sneaking gives `NoiseLevel = "Low"`.

---

## 11. Testing Checklist

Run through this checklist before submitting:

### BN Inference
- [ ] Open the BNDemo scene → set VisualContact=True → P(Chasing) should be > 0.90
- [ ] Set all evidence to None/None/False → P(Patrolling) should be highest
- [ ] Set NoiseLevel=High, VisualContact=False → P(Investigating) should be highest
- [ ] All posterior probabilities sum to 1.0 (check debug panel)

### Guard Behavior
- [ ] Guard patrols waypoints when player is far away
- [ ] Guard switches to Investigating when player runs nearby (out of sight)
- [ ] Guard switches to Chasing when player walks into vision cone
- [ ] Guard state label updates correctly ([P] / [I] / [C])
- [ ] Suspicion meter bar fills up red when guard is chasing

### Player
- [ ] WASD moves the player
- [ ] Holding Shift reduces movement speed (check NoiseLevel in debug panel)
- [ ] Picking up treasure makes it disappear and shows banner
- [ ] Reaching exit after picking up treasure triggers Win panel
- [ ] Getting caught by a guard triggers Game Over panel
- [ ] Restart button reloads the scene correctly

### Pathfinding
- [ ] Guards navigate around walls (not through them)
- [ ] Guards reach all patrol waypoints
- [ ] A* returns null gracefully when path is blocked (guard stays put, no crash)

### Debug Panel
- [ ] Tab toggles the debug panel on/off
- [ ] Evidence values update when player moves / enters light / makes noise
- [ ] Posterior values change in real time

---

## 12. How the AI Works (Quick Reference)

```
Each guard tick (FixedUpdate, every ~0.2s):

  Sensors
    NoiseSensor  → NoiseLevel    ∈ {None, Low, High}
    LightSensor  → LightExposure ∈ {None, Partial, Full}
    VisionSensor → VisualContact ∈ {True, False}
           |
           ▼
  Variable Elimination
    Query: P(GuardAlertState | NoiseLevel, LightExposure, VisualContact)
    Hidden variable eliminated: PlayerPresence
    Returns: { Patrolling: 0.xx, Investigating: 0.xx, Chasing: 0.xx }
           |
           ▼
  State Transition
    NewState = argmax(posterior)
    Guard transitions if NewState ≠ CurrentState
           |
           ▼
  A* Pathfinding
    Patrolling   → navigate to next patrol waypoint
    Investigating → navigate to last known suspicious tile
    Chasing      → navigate to player's current tile
```

**Conditional independence exploited by VE:**  
`NoiseLevel ⊥ LightExposure | PlayerPresence`  
(No direct edge between them in the BN → VE factorizes efficiently)

---

## 13. Troubleshooting

| Problem | Fix |
|---------|-----|
| `TileMap.Instance is null` | Make sure a GameObject with `TileMap` script is in the scene and its `Awake()` runs before guards. |
| Guard walks through walls | Check that wall Tilemaps have the `Wall` tag AND a `TilemapCollider2D`. Run the scene and inspect TileMap gizmos. |
| Posterior always returns uniform | A CPT key is missing. Check the Console for `[BayesNet] Missing CPT entry` warnings. |
| Guard never chases | VisionSensor Wall Layer mask not set. Assign the correct layer in the Inspector. |
| NullReferenceException in GuardController | Player reference not assigned in the Inspector. |
| Fog of war not working | FogLayer Order in Layer must be higher than ground and wall layers. |
| TextMeshPro missing | Install via Window → Package Manager → TextMeshPro, then import Essential Resources. |
| VE produces 0 for all states | Evidence key doesn't match a CPT row. Print `evidence` values in `RunInference()` to debug. |
| Guards ignore player completely | NoiseSensor/LightSensor Detection Radius may be too small, or Player reference not assigned. |
