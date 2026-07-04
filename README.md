# Vtool Avatar Auto-Fixer Pro

> [!WARNING]
> **Use at your own risk.** Always back up your avatar before using this tool. The developer is **not responsible** if the tool breaks your avatar, removes parts, changes materials, or causes upload failures.

Welcome to the ultimate "A to Z" VRChat Avatar Fixer. This tool diagnoses and fixes common Unity / VRChat upload errors — with a focus on **safe fixes that do not change how your avatar looks**.

## Features

### Diagnostics Dashboard
- Polygon, mesh, material, bone, PhysBone, contact, and particle metrics
- **Avatar height** and Quest polygon limit checks
- **Texture analysis** — 4K/2K counts, estimated VRAM, missing mipmaps, broken shaders
- Missing scripts, null material slots, non-unit scale warnings
- Multiple avatars in scene warning
- Quest shader compatibility check
- Overall health summary with critical issues and warnings

### 1-Click Master Fix (Safe / Non-Visual)
Runs fixes that should **not** change your avatar's appearance:
- Remove missing scripts
- Fix missing material slots (preserves submesh order — hair stays intact)
- Fix skinned mesh bounds
- Fix audio sources (3D spatialization + volume cap)
- Enable texture mipmaps
- Auto-align view position
- Auto-setup viseme lip sync

> Scale changes, mesh reimports, and texture resolution caps are **separate** and clearly marked.

### Performance Tab
- **Cap texture import size** (512 / 1024 / 2048 / 4096) — non-destructive to source files
- **Restore texture import sizes** from original source resolution
- Enable mipmaps
- Prefab unpack and hierarchy cleanup

### Quest / Android Conversion
- One-click conversion to `VRChat/Mobile/Toon Lit`
- Optional material duplication under `Assets/Vtool/QuestMaterials`

---

## Installation via VRChat Creator Companion (VCC)

**Repository URL:**
`https://raw.githubusercontent.com/DARKSIDE957/Vtool/main/index.json`

1. Copy the URL above.
2. Open **VRChat Creator Companion (VCC) → Settings → Packages**.
3. Click **Add Repository** and paste the URL.
4. Go to **Projects → Manage Project** on your avatar project.
5. Add **Vtool Avatar Auto-Fixer Pro**.

### Updating
1. Confirm the Vtool repository is in **VCC → Settings → Packages**.
2. Go to **Projects → Manage Project**.
3. Click **Update** next to Vtool if available.

Current version: **1.2.0**

## Usage

**`Vtool -> Avatar Auto-Fixer Pro`**

1. Assign your avatar root or click **Auto-Detect Avatar in Scene**.
2. Read the **disclaimer** and review **Diagnostics**.
3. Click **Backup Avatar** before any manual or performance changes.
4. Run **Master Fixes** for safe upload error fixes.
5. Use **Performance** for texture import settings (cap or restore).
6. Use **Quest/Android** before Quest uploads.

## Requirements

- Unity 2022.3+
- VRChat Avatars SDK 3.x (`com.vrchat.avatars`)

## Changelog

### 1.2.0

**Added**
- Version number shown in the tool UI
- Liability disclaimer in the tool window
- Redesigned UI with clearer sections
- Texture and memory diagnostics (4K, 2K, VRAM estimate, mipmaps, broken shaders)
- Avatar height and Quest polygon limit checks
- Multiple avatars in scene warning
- Performance tab with texture cap and restore (non-destructive to source files)
- Mipmap fix in Master Fix
- Safe vs manual fix categories in Auto-Fixes tab

**Fixed**
- Master Fix clearly separated from visual-changing operations
- Texture tools restore import sizes back to source file resolution

### 1.1.1

**Fixed**
- Master Fix removing hair by deleting null material slots
- Material slots preserve submesh order
- Mesh Read/Write and scale removed from Master Fix

### 1.1.0

**Added**
- Full diagnostics dashboard, lip sync setup, Quest material duplication, confirmation dialogs

**Fixed**
- Polygon double-counting, lip sync blendshapes, bounds, undo, scene dirty marking

### 1.0.0
- Initial release

*Developed by DARKSIDE957*
