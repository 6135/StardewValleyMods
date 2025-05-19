# UI Framework

## Idea

Create a basic UI framework mod based on the components defined in my mod. The framework must define some basic types of UI components, like Menus, buttons, inputs and dropdowns. Tooltip delays, activation buttons and other settings must be configurable on a mod-by-mod basis.


Menus must be a hierarchical construct with the ability to have sub menus activated by buttons inside them. Layout must be controller either via a grid system or relative coordinates.


Implementations of the framework must be able to use it by default or extend the base types to create custom behavior. When no extensions are necessary the mod framework must be accessible through an API when the mod is loaded. The API must accept an extended type and still work properly.


## Structure
```
StardewUI/
├── Framework/
│   ├── StardewUI.csproj
│   ├── manifest.json
│   ├── Config/
│   │   ├── UIConfig.cs
│   │   └── MenuConfig.cs
│   ├── Components/
│   │   ├── Base/
│   │   │   ├── BaseComponent.cs
│   │   │   ├── BaseClickableComponent.cs
│   │   │   └── BaseInputComponent.cs
│   │   ├── Button.cs
│   │   ├── Checkbox.cs
│   │   ├── Dropdown.cs
│   │   ├── Label.cs
│   │   ├── NumberInput.cs
│   │   ├── TextInput.cs
│   │   └── Tooltip.cs
│   ├── Layout/
│   │   ├── GridLayout.cs
│   │   ├── RelativeLayout.cs
│   │   └── LayoutManager.cs
│   ├── Menus/
│   │   ├── BaseMenu.cs
│   │   ├── DialogMenu.cs
│   │   ├── ScrollableMenu.cs
│   │   └── SubMenu.cs
│   ├── Events/
│   │   ├── UIEventArgs.cs
│   │   ├── ClickEventArgs.cs
│   │   └── InputEventArgs.cs
│   └── API/
│       ├── IStardewUIAPI.cs
│       └── StardewUIAPI.cs
├── Example/
│   └── SimpleMenuMod.cs
└── README.md
```