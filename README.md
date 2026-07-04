# Vtool Avatar Auto-Fixer Pro

> [!WARNING]
> **Work in Progress:** This tool is under active development. Some features may still evolve with VRChat SDK updates.

Welcome to the ultimate "A to Z" VRChat Avatar Fixer. This tool automatically diagnoses and fixes the most common Unity errors that prevent avatars from uploading to VRChat or cause issues in-game.

## Features

### Diagnostics Dashboard
- Accurate polygon counting (unique meshes only — no double-counting)
- Material slots and unique material counts
- Skinned mesh, bone, PhysBone, contact, and particle system metrics
- Missing script detection
- Non-unit scale warnings
- Legacy Dynamic Bone detection
- Quest shader compatibility check
- Overall health summary with critical issues and warnings

### 1-Click Master Fix
Runs a suite of safe, essential fixes in one pass:
- Remove missing scripts
- Clean missing materials
- Fix skinned mesh bounds
- Fix audio sources (3D spatialization + volume cap)
- Enable mesh Read/Write where needed
- Normalize root scale
- Auto-align view position
- Auto-setup viseme lip sync

### Individual Fixes
- **Missing Scripts & Materials Cleaner** — strips broken references that block builds
- **VRChat Auto-Setup** — aligns View Position and configures viseme lip sync (`vrc.v_*` blendshapes)
- **Blueprint ID Detach** — clears the blueprint ID for fresh uploads
- **Prefab Unpack** — fully unpack prefab instances for deep edits
- **Hierarchy Cleanup** — removes unused empty GameObjects (bones protected)

### Quest / Android Conversion
- One-click conversion to `VRChat/Mobile/Toon Lit`
- Optional material duplication to preserve PC shaders under `Assets/Vtool/QuestMaterials`

---

## Installation via VRChat Creator Companion (VCC)

This tool is hosted as a VCC Custom Repository. Install and update it directly inside the VRChat Creator Companion.

**Repository URL:**
`https://raw.githubusercontent.com/DARKSIDE957/Vtool/main/index.json`

### Step-by-Step Installation
1. Copy the Repository URL above.
2. Open the **VRChat Creator Companion (VCC)**.
3. Go to **Settings** (bottom left).
4. Open the **Packages** tab.
5. Click **Add Repository**.
6. Paste the URL and click **Add**.
7. Go to **Projects** and click **Manage Project** on your avatar project.
8. Find **Vtool Avatar Auto-Fixer Pro** and click **+** to add it.

## Usage

Once installed, open your Unity project. The tool is in the top menu:

**`Vtool -> Avatar Auto-Fixer Pro`**

1. Drag your avatar root GameObject into the tool, or click **Auto-Detect Avatar in Scene**.
2. Review the **Diagnostics** tab for upload blockers and performance warnings.
3. Use **Backup Avatar** before destructive changes.
4. Run **Master Fixes** or apply individual fixes as needed.
5. Use the **Quest/Android** tab for mobile shader conversion before Quest uploads.

## Requirements

- Unity 2022.3+
- VRChat Avatars SDK 3.x (`com.vrchat.avatars`)

## Changelog

### 1.1.1

**Fixed**
- Master Fix removing hair and avatar parts by deleting null material slots
- Material slots now stay in the same order so submeshes (hair, body, clothes) stay mapped correctly
- Mesh Read/Write and root scale normalization removed from Master Fix to prevent accidental model changes

### 1.1.0

**Added**
- Full diagnostics dashboard with health summary
- Unique material, PhysBone, contact, and particle metrics
- Missing script and non-unit scale detection
- Legacy Dynamic Bone warning
- Quest shader compatibility check
- Complete lip sync setup (viseme mesh, blendshapes, and mode)
- View position head-bone fallback
- Quest material duplication option
- Confirmation dialogs for destructive actions
- Master fix summary after running all fixes

**Fixed**
- Polygon double-counting on shared meshes
- Lip sync only assigning the mesh, not the blendshapes
- VRChat reflection failing on some SDK field types
- Skinned mesh bounds using a fixed 3x3x3 size
- Quest conversion overwriting PC materials without a backup option
- Empty GameObject cleanup not supporting undo
- Scene not saving after fixes
- Deprecated FindObjectsOfType API on Unity 2022.3+

### 1.0.0
- Initial release

*Developed by DARKSIDE957*
