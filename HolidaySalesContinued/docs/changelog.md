Changelog
===========

### Holiday Sales Continued 1.0.1
* Added the Nexus update key so SMAPI can show update alerts.

### Holiday Sales Continued 1.0.0
* Continuation by 6135 for Stardew Valley 1.6.15, no longer requires AtraCore.
* Store closing is now a prefix on `AreStoresClosedForFestival`, which also covers locked doors and the phone.
* Daily quest is now a postfix that keeps vanilla behavior (including marking it a daily quest) and only adds the quest when an out-of-town festival blocked it.
* Fixed: `Custom_<Mod>` maps without a map suffix never matched custom festivals.
* Fixed: possible infinite loop on circular location-context weather links.

### Version 0.2.0
* Update for Stardew 1.6
* Now favors using the location context ID over parsing the map name, if that is available.

### Version 0.1.1
* Mod-added areas that do not follow convention are now considered their own region.
* Performance improvements - much thanks to the Harmony discord for pointing me towards the right path here.

### Version 0.1.0

Initial Upload.