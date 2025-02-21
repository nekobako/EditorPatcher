# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]
### Added

### Changed

### Deprecated

### Removed

### Fixed

### Security

## [0.13.0] - 2025-02-22
### Added
- Add window to keep object references by D&D.

## [0.12.0] - 2025-02-11
### Added
- Add button to focus on head in Avatar Preview.

## [0.11.0] - 2025-02-10
### Added
- Add feature to align and keep camera view in Avatar Preview.

## [0.10.1] - 2025-02-01
### Fixed
- Array length may be applied to the parent prefab when fixing prefab override.
- Prevent blendshapes that begin or end with 3 non-word characters (e.g. `eye_><`) from being treated as group name patterns.

## [0.10.0] - 2025-01-25
### Changed
- Keep blendshape list states per renderer.

### Fixed
- Update blendshape list when mesh is updated.

## [0.9.0] - 2024-11-27
### Added
- Add `Fix Prefab Override`, ability to revert equivalent prefab overrides

## [0.8.0] - 2024-11-16
### Added
- Add prolonged sound marks to blendshape group name pattern.

### Fixed
- Fix compile error on Unity 2019.

## [0.7.0] - 2024-10-27
### Changed
- Treat a blendshape name as a group name even if it contains a sequence of non-word characters at either the beginning or the end of the name

## [0.6.3] - 2024-10-06
### Fixed
- Prevent checkboxes from appearing in the header when selecting multiple renderers.

## [0.6.2] - 2024-10-03
### Fixed
- Fix errors with missing mesh.

## [0.6.1] - 2024-10-01
### Fixed
- Do not unpatch all other patches before assembly reload.

## [0.6.0] - 2024-10-01
### Added
- Highlight blendshape rows on mouse hover.
- Add keyboard focus to blendshape search.

### Changed
- Draw property field even if there are no meshes or blendshapes.
- Support more than 32 blendshape groups.

### Fixed
- Prevent blendshape rows to be selected on right click.
- Fix UI misalignments.

## [0.5.0] - 2024-09-26
### Changed
- Use TreeView instead of ReorderableList for better performance.

### Fixed
- Fix duplicate group names filtering.

## [0.4.1] - 2024-09-25
### Fixed
- Fix required VRChat SDK version.

## [0.4.0] - 2024-09-25
### Changed
- Support Unity 2019.

## [0.3.0] - 2024-09-24
### Added
- Add toggle to show or hide blendshapes with value 0.

### Fixed
- Show message that multi-object editing is not supported.

## [0.2.1] - 2024-09-23
### Fixed
- Fix guid to avoid conflicting with other package.

## [0.2.0] - 2024-09-23
### Fixed
- Fix required Unity version.

## [0.1.0] - 2024-09-23
### Added
- Initial release.
