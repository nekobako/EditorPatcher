| English | [日本語](README-ja-jp.md) |

# Editor Patcher
Tools to enhance the Unity Editor.

## Installation
1. Press `Add to VCC` button in [VPM Listing](https://vpm.nekobako.net) and add the repository.
2. Press `Manage Project` button at your project.
4. Press `+` button at the right of `Editor Patcher` package.

## Usage

### Skinned Mesh Renderer Editor
- `Tools > Editor Patcher > Skinned Mesh Renderer Editor`
  - Replace the blend shapes drawer in the Skinned Mesh Renderer Inspector with one that supports searching and filtering.
  - Add a bone drawer to the Skinned Mesh Renderer Inspector.

![Skinned Mesh Renderer Editor](https://github.com/user-attachments/assets/1bff1f3b-907a-4f5a-b042-364a72990d63)

### Avatar Preview
- `Tools > Editor Patcher > Avatar Preview`
  - Align and keep the camera view in the Avatar Preview so that it doesn't reset each time.

https://github.com/user-attachments/assets/b3c86ca5-17aa-4cb6-a69d-200744819162

### Object Shelf
- `Tools > Editor Patcher > Object Shelf > Auto Spawn`
  - Automatically spawn the window to keep object references by D&D.
- `Tools > Editor Patcher > Object Shelf > Manual Spawn`
  - Manually spawn the window to keep object references.

https://github.com/user-attachments/assets/d86f9f7d-83e6-4ec2-b1dd-cfe2e1a27276

### Platform Switcher
- `Tools > Editor Patcher > Platform Switcher`
  - Add the buttons to switch platforms in the main toolbar.

![Platform Switcher](https://github.com/user-attachments/assets/7864e699-26e3-42f7-af0c-5109b08fe269)

### Rebase Prefab
- `Assets > Prefab > Rebase and Keep All...`
  - Recursively create new prefab variants with a specified base prefab while keeping all properties.
  - Accessible from the right-click menu on the prefab.
- `Assets > Prefab > Rebase and Keep Overrides...`
  - Recursively create new prefab variants with a specified base prefab while keeping their overrides.
  - Accessible from the right-click menu on the prefab.

### Fix Prefab Override
- `Tools > Editor Patcher > Fix Prefab Override > Enable Auto Fix`
  - Automatically revert prefab overrides that are equal to their original values.
  - Accessible only in Unity 2021.3 and later.
- `Tools > Editor Patcher > Fix Prefab Override > Fix All in Project`
  - Revert all prefab overrides in the project that are equal to their original values.
  - Backup your project before running this.
- `GameObject > Editor Patcher > Fix Prefab Override`
  - Revert all prefab overrides in the selected prefab that are equal to their original values.
  - Accessible from the right-click menu on the prefab.
