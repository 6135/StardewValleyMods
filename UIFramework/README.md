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

# UIFramework for Stardew Valley

A comprehensive, flexible UI framework for developing custom menus and interfaces in Stardew Valley mods.

![Version](https://img.shields.io/badge/Version-1.0.0-blue)
![API](https://img.shields.io/badge/SMAPI-4.3.2+-orange)

## Overview

UIFramework is a modular, extensible library designed to simplify the creation of custom UI elements in Stardew Valley mods. It provides a set of pre-built components, layout managers, and event handling systems that allow mod developers to create sophisticated interfaces without having to implement low-level rendering or input handling.

## Features

- **Component-Based Architecture**: Build UIs using modular, reusable components
- **Hierarchical Menu System**: Create parent-child relationships between menus
- **Flexible Layout Management**: Arrange components using Grid or Relative layouts
- **Event-Driven Interactions**: Respond to clicks, text input, and other user actions
- **Consistent Styling**: Maintain a cohesive look and feel across UI elements
- **Configurable Settings**: Customize behavior on a per-mod basis
- **Extensible Base Classes**: Create custom components by extending the provided base classes

## Installation

1. Install [SMAPI](https://smapi.io/) (3.0.0 or higher)
2. Download the UIFramework mod and place it in your Stardew Valley Mods folder
3. Reference the UIFramework in your mod's project
4. Access the API through SMAPI's mod registry

## Getting Started

### Accessing the API

```csharp
// In your mod's Entry method
public override void Entry(IModHelper helper)
{
    var uiFrameworkApi = helper.ModRegistry.GetApi<IStardewUIAPI>("6135.UIFramework");
    if (uiFrameworkApi == null)
    {
        this.Monitor.Log("UIFramework not found. Make sure it's installed correctly.", LogLevel.Error);
        return;
    }
    
    // Now you can use the API to create UI elements
}
```

### Creating a Simple Menu

```csharp
// Define menu configuration
var menuConfig = new MenuConfig
{
    Title = "My Custom Menu",
    Width = 800,
    Height = 600,
    ShowCloseButton = true,
    UseGameBackground = true,
    ToggleKey = SButton.F5
};

// Create main menu
var myMenu = uiFrameworkApi.CreateMenu("UniqueMenuId", menuConfig);

// Add components to the menu
var button = uiFrameworkApi.CreateButton("buttonId", "Click Me", new Vector2(50, 100), OnButtonClick);
var label = uiFrameworkApi.CreateLabel("labelId", "Hello World", new Vector2(50, 50));

myMenu.AddComponent(button);
myMenu.AddComponent(label);

// Register the menu with the framework
uiFrameworkApi.RegisterMenu(myMenu);

// Register hotkey to show/hide menu
uiFrameworkApi.RegisterHotkey("toggleMyMenu", SButton.F5, () => uiFrameworkApi.ShowMenu("UniqueMenuId"));

// Button click handler
private void OnButtonClick()
{
    // Handle button click
    Game1.addHUDMessage(new HUDMessage("Button clicked!"));
}
```

## Core Components

### Component Hierarchy

UIFramework follows a hierarchical structure for components:

```
BaseComponent
├── BaseClickableComponent
│   ├── Button
│   ├── Checkbox
│   └── Dropdown
└── BaseInputComponent
    ├── TextInput
    └── NumberInput
```

### Available Components

| Component | Description | Key Properties |
|-----------|-------------|----------------|
| Label | Displays text | Text, Font, Alignment, WordWrap |
| Button | Clickable button with text | Text, TextColor, BackgroundColor |
| TextInput | Text field for user input | Value, Placeholder, MaxLength |
| Checkbox | Toggle for boolean values | Checked, Text |
| Dropdown | Selection from a list of options | Options, SelectedIndex |

## Layout System

UIFramework provides two powerful layout systems to help position components:

### Grid Layout

The GridLayout organizes components into rows and columns:

```csharp
// Create a 3x3 grid with 100x50 cells
var grid = uiFrameworkApi.CreateGridLayout(3, 3, 100, 50);

// Add a component spanning 2 columns and 1 row
grid.AddComponent(myButton, 0, 0, 2, 1);

// Add a component in cell 2,2
grid.AddComponent(myLabel, 2, 2);
```

### Relative Layout

The RelativeLayout positions components relative to others:

```csharp
var relativeLayout = uiFrameworkApi.CreateRelativeLayout();

// Add component to the center of the screen
relativeLayout.AddComponent(myLabel, RelativeLayout.AnchorPoint.Center, Vector2.Zero);

// Position component 10 pixels below another component
relativeLayout.AddComponent(myButton, myLabel, RelativeLayout.AnchorPoint.Bottom, new Vector2(0, 10));
```

## Menu System

The UIFramework provides a flexible menu system with support for:

- **BaseMenu**: Standard menu container
- **SubMenu**: Child menus that can be triggered by parent elements
- **ScrollableMenu**: Menu with scrollable content

### Creating Sub-Menus

```csharp
// Create main menu
var mainMenu = uiFrameworkApi.CreateMenu("mainMenu", mainConfig);

// Create sub menu
var subMenuConfig = new MenuConfig { Width = 400, Height = 300 };
var subMenu = new SubMenu("subMenu", subMenuConfig);

// Create button that opens sub menu
var button = uiFrameworkApi.CreateButton("openSubMenu", "Open Sub Menu", 
    new Vector2(50, 100), 
    () => subMenu.Show());

mainMenu.AddComponent(button);

// Add the sub menu as a child of the main menu
mainMenu.AddSubMenu(subMenu);

// Position the sub menu relative to the button
subMenu.PositionRelativeTo(button, SubMenu.MenuPosition.Below);
```

## Event Handling

UIFramework uses an event-driven architecture for handling user interactions:

```csharp
// Create a button with click handler
var button = uiFrameworkApi.CreateButton("myButton", "Click Me", 
    new Vector2(50, 100), 
    () => Game1.addHUDMessage(new HUDMessage("Button clicked!")));

// Alternative registration of click handler
uiFrameworkApi.RegisterClickHandler("myButton", (e) => {
    Game1.addHUDMessage(new HUDMessage($"Clicked at position: {e.X}, {e.Y}"));
});

// Register input handler for text field
var textInput = uiFrameworkApi.CreateTextInput("myInput", new Vector2(50, 150), "", null);
uiFrameworkApi.RegisterInputHandler("myInput", (e) => {
    Monitor.Log($"Text changed from '{e.OldValue}' to '{e.NewValue}'");
});
```

## Configuration

The UIFramework allows for configuration at several levels:

### Global Configuration

```csharp
// Set global tooltip delay
uiFrameworkApi.SetGlobalTooltipDelay(500); // milliseconds
```

### Menu Configuration

```csharp
var menuConfig = new MenuConfig
{
    Title = "My Menu",
    Width = 800,
    Height = 600,
    ShowCloseButton = true,
    UseGameBackground = true,
    BackgroundColor = Color.Black * 0.8f, // Semi-transparent black
    Modal = true,
    Draggable = false,
    ToggleKey = SButton.F5,
    TooltipDelay = 300 // Override global tooltip delay
};
```

### Component Configuration

```csharp
var label = uiFrameworkApi.CreateLabel("myLabel", "Hello World", new Vector2(50, 50));
label.TextColor = Color.Red;
label.Font = Game1.dialogueFont;
label.Scale = 1.5f;
label.Alignment = Label.TextAlignment.Center;
```

## Custom Components

You can extend the framework by creating custom components:

```csharp
public class MyCustomComponent : BaseComponent
{
    public MyCustomComponent(string id, Vector2 position, Vector2 size) 
        : base(id, position, size)
    {
        // Initialize component
    }

    public override void Draw(SpriteBatch b)
    {
        // Custom drawing logic
        b.Draw(Game1.staminaRect, Bounds, Color.Purple);
    }

    public override void Update(GameTime time)
    {
        // Custom update logic
        base.Update(time);
    }
}
```

## Styling Guidelines

For consistent look and feel across your mod's UI:

- Use the same font (Game1.smallFont or Game1.dialogueFont) for related components
- Maintain consistent spacing between components (typically 10-20 pixels)
- Use a consistent color scheme (consider using Game1.textColor for text)
- Provide visual feedback for interactive elements (change color on hover/click)
- Include tooltips for complex functionality

## Advanced Usage

### Custom Menus

```csharp
public class MyCustomMenu : BaseMenu
{
    private Label _titleLabel;

    public MyCustomMenu(string id, MenuConfig config) : base(id, config)
    {
        _titleLabel = new Label("titleLabel", new Vector2(width/2, 20), "My Custom Menu");
        _titleLabel.Alignment = Label.TextAlignment.Center;
        
        AddComponent(_titleLabel);
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        // Initialize your custom components
    }

    protected override void DrawComponents(SpriteBatch b)
    {
        // Custom drawing logic if needed
        base.DrawComponents(b);
    }
}
```

### Layout Management

For complex UIs, consider combining layout approaches:

```csharp
// Create grid layout for the form
var formGrid = uiFrameworkApi.CreateGridLayout(2, 3, 200, 40);

// Create relative layout for the buttons
var buttonLayout = uiFrameworkApi.CreateRelativeLayout();

// Add components to the form grid
formGrid.AddComponent(nameLabel, 0, 0);
formGrid.AddComponent(nameInput, 1, 0);
formGrid.AddComponent(emailLabel, 0, 1);
formGrid.AddComponent(emailInput, 1, 1);

// Add buttons to the relative layout
buttonLayout.AddComponent(saveButton, RelativeLayout.AnchorPoint.BottomLeft, Vector2.Zero);
buttonLayout.AddComponent(cancelButton, saveButton, RelativeLayout.AnchorPoint.Right, new Vector2(10, 0));

// Add the layouts to menu
foreach (var component in formGrid.GetComponents())
    menu.AddComponent(component);

foreach (var component in buttonLayout.GetComponents())
    menu.AddComponent(component);
```

## Best Practices

1. **Unique Identifiers**: Always use unique, descriptive IDs for components and menus
2. **Error Handling**: Check for null references when accessing components or menus
3. **Resource Management**: Hide menus when they're no longer needed
4. **Responsive Design**: Use relative positioning to handle different screen sizes
5. **Performance**: Minimize the number of components in a menu for better performance
6. **Accessibility**: Include keyboard shortcuts for important actions
7. **Consistency**: Follow Stardew Valley's UI visual language for a seamless experience
8. **Extensibility**: Design custom components to be easily extended by other modders

## Troubleshooting

### Common Issues

- **Components Not Visible**: Check if the component and its parent menu are set to visible
- **Click Events Not Firing**: Ensure the component is both visible and enabled
- **Layout Issues**: Verify grid coordinates or relative positioning parameters
- **Menu Not Showing**: Confirm the menu is registered with the framework and shown properly
- **Performance Problems**: Reduce the number of components or optimize drawing code

## Contributing

Contributions to UIFramework are welcome! Please feel free to submit pull requests or open issues on GitHub.

## License

UIFramework is released under the MIT License.

## Acknowledgements

- Inspired by the UI systems in Stardew Valley and other modding frameworks
- Thanks to the Stardew Valley modding community for their invaluable support and feedback

---

For further information or support, please open an issue on the GitHub repository or contact the author directly.
