// Port of the framework's measure / arrange pass: Core/UIElement.cs, Core/UIContainer.cs and the components'
// MeasureCore / MinWidthCore / ArrangeCore (Components/*.cs, Core/AutoForm.cs). Containers are ported line by line;
// leaves use the framework's fixed sizes and the injected text measurer. The theme's SpacingScale / FontScale are the
// vanilla 1 (Theme.Space, Theme.ScaleForText and Theme.ExtraTextHeight are then identities / 0).
// The element classes live in sibling modules by concern; this module re-exports them.
export { LContainer, LElement, type ElementInfo, type LayoutContext, type ScrollState, type Vec } from './elementBase';
export { LStack } from './stackElement';
export { LGrid } from './gridElement';
export { clampOffset, LCanvas, LPanel, LScrollView, LSpacer, SCROLLBAR_RESERVED, SCROLLBAR_WIDTH, scrollbarParts, scrollOffsetAt } from './panelElements';
export { LButton, LCheckbox, LDropdown, LImage, LItemImage, LLabel, LPlaceholder, LSlider, LTextBox } from './textElements';
export { LDataGrid, LForm, LListView, type GridColumn } from './collectionElements';
