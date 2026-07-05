# Vtool Avatar Auto-Fixer Pro

> [!WARNING]
> **Use at your own risk.** Back up your avatar first. The developer is **not responsible** if this tool breaks your avatar or causes upload failures.

Pre-upload tool for VRChat avatars. Scans for **30+ common upload problems** and fixes what it safely can.

## What it checks

### Upload blockers (must fix)
- Missing VRCAvatarDescriptor
- Missing PipelineManager
- Missing humanoid Animator / no controller (T-Pose)
- Missing scripts
- Null material slots
- Broken shaders (pink materials)
- Missing meshes on renderers
- Extreme polygon count

### Warnings (should fix)
- Missing Chest bone mapping
- View position / lip sync not set
- Root scale not (1,1,1) / negative scales
- High polygons, skinned meshes, material slots
- 4K / 2K+ textures, high VRAM, missing mipmaps
- Legacy Dynamic Bones, too many PhysBones
- Bad audio (not 3D, loud, playOnAwake)
- Too many particle systems
- Other avatars active in scene
- Non-Quest shaders (Android uploads)

## What Fix All does (safe / non-visual)
- Remove missing scripts
- Fix material slots (hair-safe)
- Add PipelineManager if missing
- Assign dummy animator controller
- Fix skinned mesh bounds
- Fix audio (3D, volume, playOnAwake)
- Enable texture mipmaps
- Disable other avatars in scene
- Align view position
- Setup lip sync

## Textures & Quest tab
- Reduce texture import size (512 / 1024 / 2048)
- Restore original sizes
- Convert to Quest mobile shaders

---

## Install (VCC)

`https://raw.githubusercontent.com/DARKSIDE957/Vtool/main/index.json`

Current version: **2.1.1**

Support: [Buy Me a Coffee](https://buymeacoffee.com/Omv1)

## Usage

**`Vtool → Avatar Auto-Fixer Pro`**

1. Assign avatar root
2. **Pre-Upload Check** — read blockers and warnings
3. **Backup Avatar**
4. **Fix All Upload Errors**
5. **Textures & Quest** — reduce textures if needed
6. Upload via VRChat SDK

*Developed by DARKSIDE957*
