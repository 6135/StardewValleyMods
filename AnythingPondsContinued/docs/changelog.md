Changelog
===========

### Anything Ponds Continued 1.0.0 (unreleased)
* Continuation by 6135 for Stardew Valley 1.6.15.
* Items now go into ponds through the vanilla pond code (a transpiler on `FishPond.doAction`), so vanilla messages, pond requests and multiplayer sync all work.
* Pond entries are added with `Id`s (`6135.AnythingPonds_Seaweed`, `_GreenAlgae`, `_WhiteAlgae`, `_Generic`); edit them with Content Patcher instead of the old `pond_*.json` files.
* The generic catch-all pond is now off by default (`EnableGenericPonds`).
* Empty-pond days are stored on the pond (`modData`) instead of in save data.
* Removed the Legendary Fish Ponds pack (vanilla since 1.6). Doll Ponds updated to Content Patcher 2.0.
* Added Generic Mod Config Menu support.

### Version 1.0.0
* Initial release by MouseyPounds.
