# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).


## [0.4.0] - 2020-03-11
### Added
- Meta versions of Unity:
2017.4.37f1,
2018.4.17f1, 2018.4.18f1,
2019.2.20f1, 2019.2.21f1,
2019.3.0f6, 2019.3.1f1, 2019.3.2f1, 2019.3.3f1, 2019.3.4f1,
2020.1.0a21, 2020.1.0a22, 2020.1.0a23, 2020.1.0a24, 2020.1.0a25,
2020.1.0b1.

### Changed
- Meta versions merged into one single file. (Loading time tremendously faster and file insanely reduced)
- Type search has its own cache database.
- Type search provides more details and options.
- UI/UX greatly improved.

## [0.3.5] - 2020-01-28
### Added
- Type search.
- Meta versions of Unity 2018.4.16f1, 2019.2.18f1, 2019.2.19f1, 2020.1.0a19, 2020.1.0a20.

## [0.3.4] - 2020-01-15
### Added
- Context to an exception when loading a meta.
- Meta versions of Unity 2017.4.36f1, 2018.4.15f1, 2019.3.0f5, 2020.1.018.

## [0.3.3] - 2019-12-22
### Fixed
- Core assemblies not being resolved correctly.

## [0.3.2] - 2019-12-21
### Changed
- Order of results by version.
- Found items' label now displaying the count.

## [0.3.1] - 2019-12-21
### Changed
- Core dependencies are now fully separated from NG Tools.
- Order of displayed assembly meta versions.

## [0.3.0] - 2019-12-21
### Added
- Minimum and maximum of each minor versions of Unity.

### Changed
- Menu item is now in Window instead of Window/NG Tools.
- Order of results is now sorted by version.

### Removed
- Removed unused menu items.

## [0.2.0] - 2019-12-19
### Added
- Export all in the context menu.

### Changed
- Improved UI (Tooltip, more accurate information, plural forms)
- Refactored code.

### Removed
- Removed unused code.

## [0.1.0] - 2019-12-18
### Added
- First introduction.
