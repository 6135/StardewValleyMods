# Changelog

## 1.3.0

### Changed

- **Prismatic stones are much rarer by default:** 0.0025 on floors 100-110 (was 0.01), 0.0075 on floors 111-120 (was
  0.03) and 0.025 in the Skull Cavern (was 0.10). Existing `config.json` values are kept; reset them in GMCM to get
  the new defaults.
- Spawn rates below 0.10 can now be set in steps of 0.0025 (was 0.01).

### New

- The mining experience of each node can be changed in the config.

### Fixed

- The Pure Coal spawn rate for the Skull Cavern did nothing: those floors used the Prismatic Stone rate instead.
- The Lava Coal options were described as "ice coal".
- SMAPI now reports updates for this mod.
