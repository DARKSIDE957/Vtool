<div align="center">

<img src="Editor/Resources/VtoolLogo.png" alt="Vtool" width="128" />

# Vtool: Avatar Auto-Fixer Pro

**Pre-upload checks and fixes for VRChat avatars in Unity.**

Scan your avatar, fix the usual SDK problems, shrink textures, then upload like normal.

<br/>

[![release](https://img.shields.io/github/v/release/DARKSIDE957/Vtool?style=for-the-badge)](https://github.com/DARKSIDE957/Vtool/releases/latest)
[![unity](https://img.shields.io/badge/Unity-2022.3+-222?style=for-the-badge&logo=unity&logoColor=white)](https://unity.com/)
[![vcc](https://img.shields.io/badge/Install-VCC-9146FF?style=for-the-badge)](https://vcc.docs.vrchat.com/)

</div>

<br/>

## Overview

Vtool is a Unity Editor window for VRChat avatar creators. Point it at your avatar root and it tells you what is wrong before you upload.

| | |
|:--|:--|
| **Where** | Unity Editor only |
| **Install** | VRChat Creator Companion (VCC) |
| **Price** | Free |
| **Code** | Open source on this repo |

<br/>

## The 3 tabs

<table>
<tr>
<td width="33%" valign="top">

### Check
Blockers, warnings, and a performance snapshot.

Poly count, textures, VRChat setup, PhysBones, audio, Quest shaders, and more.

</td>
<td width="33%" valign="top">

### Fix
Safe one-click fixes for upload errors.

Scripts, materials, bounds, audio, lip sync, view position, PipelineManager, scene conflicts.

</td>
<td width="33%" valign="top">

### Textures
Lower import size or prep for Quest.

Cap to 512 / 1024 / 2048, restore originals, convert mobile shaders.

</td>
</tr>
</table>

<br/>

## Install

**Requirements:** Unity 2022.3+, VRChat Avatars SDK, VCC

**Step 1.** VCC → **Settings** → **Packages** → **Add Repository**

```text
https://raw.githubusercontent.com/DARKSIDE957/Vtool/main/index.json
```

**Step 2.** Open your project in VCC → **Manage Project** → add **Vtool Avatar Auto-Fixer Pro**

**Step 3.** In Unity: **Vtool → Avatar Auto-Fixer Pro**

> [!TIP]
> You can update while Unity is open. If the window looks old after an update, use **Vtool → Apply Package Update (Reload)**.

<br/>

## Quick start

1. Drop your **avatar root** into the tool (or **Auto-Detect**)
2. **Check** tab: read what failed
3. **Backup Avatar** (do this first)
4. **Fix** tab: **Fix All Upload Errors**
5. **Textures** tab: only if you need smaller textures or Quest shaders
6. Upload through the **VRChat SDK**

<br/>

## What gets checked

<details>
<summary><b>Upload blockers</b></summary>

- Missing `VRCAvatarDescriptor` or `PipelineManager`
- Missing humanoid Animator
- Missing scripts
- Null material slots
- Broken shaders (pink materials)
- Missing meshes
- Extreme polygon count

</details>

<details>
<summary><b>Warnings</b></summary>

- View position / lip sync not set
- Chest bone not mapped
- Bad root or negative scale
- High poly count, materials, PhysBones, particles
- 4K / 2K+ textures, missing mipmaps
- Audio not 3D, too loud, play on awake
- Other avatars active in scene
- Non-Quest shaders

</details>

<details>
<summary><b>What Fix All changes</b></summary>

- Removes missing scripts
- Fills null material slots (keeps slot order, hair-safe)
- Adds `PipelineManager` if missing
- Fixes skinned mesh bounds
- Fixes audio (3D, volume, play on awake)
- Enables mipmaps
- Disables other avatars in the scene
- Aligns view position
- Sets up lip sync

Pink shaders still need manual reassignment.

</details>

<br/>

## Safety

> [!WARNING]
> Back up your avatar before running fixes. Use **Backup Avatar** in the Fix tab or save a copy of the project.

This tool is provided as-is. Test on a copy if you are unsure.

<br/>

## Links

- [Releases](https://github.com/DARKSIDE957/Vtool/releases)
- [Report a bug](https://github.com/DARKSIDE957/Vtool/issues)
- [Buy Me a Coffee](https://buymeacoffee.com/Omv1) (optional)

<br/>

<div align="center">
<sub>by DARKSIDE957</sub>
</div>
