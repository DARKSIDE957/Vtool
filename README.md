# Vtool Avatar Auto-Fixer Pro

> [!WARNING]
> **Use at your own risk.** Back up your avatar first. The developer is **not responsible** if this tool breaks your avatar or causes upload failures.

A focused **pre-upload** tool for VRChat avatars. It does two things:

1. **Fix upload errors** before you upload
2. **Reduce texture size** to pass VRChat limits

## What it fixes (upload errors)

- Missing scripts
- Null material slots (hair-safe — keeps submesh order)
- Missing animator controller (T-Pose fix)
- Skinned mesh bounds
- Audio (3D spatialization + volume)
- View position + lip sync setup

## Textures

- Cap import size to **512 / 1024 / 2048**
- **Restore** original source resolution anytime
- Enable mipmaps for performance

Original image files are never deleted — only Unity import settings change.

---

## Install (VCC)

`https://raw.githubusercontent.com/DARKSIDE957/Vtool/main/index.json`

1. VCC → **Settings → Packages → Add Repository**
2. **Projects → Manage Project** → add **Vtool Avatar Auto-Fixer Pro**

Current version: **2.0.0**

## Usage

**`Vtool → Avatar Auto-Fixer Pro`**

1. Assign your avatar root
2. Read the **Pre-Upload Check** list
3. Click **Backup Avatar**
4. Click **Fix All Upload Errors**
5. Click **Reduce Textures** if needed
6. Upload with VRChat SDK

## Requirements

- Unity 2022.3+
- VRChat Avatars SDK 3.x

## Changelog

### 2.0.0

**Added**
- Full tool remake focused only on pre-upload errors and texture size
- Clear blocker vs warning list before upload
- One-click Fix All Upload Errors
- Dedicated Reduce Texture Size section with restore

**Fixed**
- Removed extra tabs and features that were not upload-related

### 1.2.0
- Texture diagnostics, Performance tab, UI overhaul

### 1.1.1
- Hair-safe material slot fix

### 1.0.0
- Initial release

*Developed by DARKSIDE957*
