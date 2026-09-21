using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using UIFramework.Api;
using UIFramework.Components;

namespace UIFramework.Core
{
    /// <summary>
    /// A form generated from a plain object (see <see cref="IUIForm"/>): a two-column grid of caption / input rows
    /// (with optional section headers and per-row validation messages) above a Save / Cancel / Undo / Redo button
    /// row. Inputs read the model through reflection every frame and write it through <see cref="Commit"/>, which
    /// validates, records the change for undo and raises <see cref="OnChanged"/>. The container itself stacks its
    /// two children vertically.
    /// </summary>
    internal sealed class AutoForm : UIContainer, IUIForm
    {
        private const int Spacing = 12;
        private const int SectionGap = 8;

        private readonly ConsumerContext owner;
        private readonly List<FormField> fields = new();
        private readonly Dictionary<string, object?> snapshot = new();
        private readonly List<FormChange> undoStack = new();
        private readonly List<FormChange> redoStack = new();
        private readonly Grid grid;
        private readonly Stack buttons;
        private readonly Button undoButton;
        private readonly Button redoButton;
        private bool refreshing;

        internal AutoForm(string id, object model, ConsumerContext owner) : base(id)
        {
            Model = model;
            this.owner = owner;

            grid = new Grid(id + ".grid", "auto,*", "auto")
            {
                ColumnSpacing = 24,
                RowSpacing = Spacing,
                HorizontalAlign = UIAlign.Stretch
            };
            BuildRows(id);
            Add(grid);

            buttons = new Stack(id + ".buttons", horizontal: true, spacing: 16)
            {
                HorizontalAlign = UIAlign.Center
            };
            buttons.Add(new Button(id + ".save", Translate("form.save", "Save"), _ => Save()));
            buttons.Add(new Button(id + ".cancel", Translate("form.cancel", "Cancel"), _ => Cancel()));
            undoButton = new Button(id + ".undo", Translate("form.undo", "Undo"), _ => Undo());
            redoButton = new Button(id + ".redo", Translate("form.redo", "Redo"), _ => Redo());
            buttons.Add(undoButton);
            buttons.Add(redoButton);
            Add(buttons);

            TakeSnapshot();
            SyncButtons();
        }

        /// <summary>The consumer's object the form edits.</summary>
        internal object Model { get; }

        // ---------------------------------------------------------------------------------------------------------
        //  Build
        // ---------------------------------------------------------------------------------------------------------

        private void BuildRows(string id)
        {
            int row = 0;
            int sections = 0;
            foreach (FormProperty property in FormReflection.Describe(Model))
            {
                if (property.Section != null)
                {
                    AddSection(id + ".section" + sections++, property.Section, ref row);
                }

                var field = new FormField(this, property, id);
                field.Caption.Row = row;
                field.Cell.Row = row;
                field.Cell.Column = 1;
                grid.Add(field.Caption);
                grid.Add(field.Cell);
                fields.Add(field);
                row++;
            }
        }

        /// <summary>A gap (except before the first row) and a dialogue-font title spanning both columns.</summary>
        private void AddSection(string id, string title, ref int row)
        {
            if (row > 0)
            {
                var gap = new Spacer(id + ".gap", 0, SectionGap)
                {
                    Row = row++,
                    ColumnSpan = 2
                };
                grid.Add(gap);
            }

            var header = new Label(id, () => title)
            {
                Font = UIFont.Dialogue,
                Row = row++,
                ColumnSpan = 2
            };
            grid.Add(header);
        }

        private static Func<string> Translate(string key, string fallback) => () => Text(key, fallback);

        private static string Text(string key, string fallback)
        {
            StardewModdingAPI.ITranslationHelper? translation = UIServices.Translation;
            return translation == null ? fallback : translation.Get(key).Default(fallback).ToString();
        }

        // ---------------------------------------------------------------------------------------------------------
        //  IUIForm
        // ---------------------------------------------------------------------------------------------------------

        public bool IsDirty
        {
            get
            {
                foreach (FormField f in fields)
                {
                    if (!Equals(f.Read(), snapshot.GetValueOrDefault(f.Property.Name)))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool CanUndo => undoStack.Count > 0;

        public bool CanRedo => redoStack.Count > 0;

        public bool ShowButtons
        {
            get => buttons.Visible;
            set => buttons.Visible = value;
        }

        internal Action<IUIForm>? OnSaved { get; set; }
        internal Action<IUIForm>? OnCancelled { get; set; }
        internal Action<IUIForm>? OnChanged { get; set; }

        Action<IUIForm> IUIForm.OnSaved { get => OnSaved!; set => OnSaved = value; }
        Action<IUIForm> IUIForm.OnCancelled { get => OnCancelled!; set => OnCancelled = value; }
        Action<IUIForm> IUIForm.OnChanged { get => OnChanged!; set => OnChanged = value; }

        public IUIElement FieldFor(string propertyName)
        {
            FormField? field = FindField(propertyName ?? string.Empty);
            return field?.Control!;
        }

        private FormField? FindField(string propertyName) => fields.Find(f => f.Property.Name == propertyName);

        public void Undo()
        {
            if (!CanUndo)
            {
                return;
            }

            FormChange change = undoStack[^1];
            undoStack.RemoveAt(undoStack.Count - 1);
            change.Field.Write(change.OldValue);
            redoStack.Add(change);
            RefreshControls();
            AfterChange();
        }

        public void Redo()
        {
            if (!CanRedo)
            {
                return;
            }

            FormChange change = redoStack[^1];
            redoStack.RemoveAt(redoStack.Count - 1);
            change.Field.Write(change.NewValue);
            undoStack.Add(change);
            RefreshControls();
            AfterChange();
        }

        public void Save()
        {
            TakeSnapshot();
            ClearHistory();
            RefreshControls();
            SyncButtons();
            RaiseForm("OnSaved", OnSaved);
        }

        public void Cancel()
        {
            foreach (FormField field in fields)
            {
                field.Write(snapshot.GetValueOrDefault(field.Property.Name));
            }

            ClearHistory();
            RefreshControls();
            AfterChange();
            RaiseForm("OnCancelled", OnCancelled);
        }

        public void Refresh() => RefreshControls();

        // ---------------------------------------------------------------------------------------------------------
        //  Edits (called by the generated inputs)
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Input-side validation hook (text / number inputs): shows / hides the row's message; false rejects the edit.</summary>
        internal bool Validate(FormField field, object controlValue)
        {
            if (refreshing)
            {
                return true;
            }

            object? value = field.FromControl(controlValue);
            if (value == null)
            {
                return false;
            }

            return ValidateValue(field, value);
        }

        /// <summary>Write an edited value into the model (after validation for inputs without their own hook) and record it for undo.</summary>
        internal void Commit(FormField field, object controlValue)
        {
            if (refreshing)
            {
                return;
            }

            object? newValue = field.FromControl(controlValue);
            if (newValue == null)
            {
                return;
            }

            object? oldValue = field.Read();
            if (Equals(oldValue, newValue))
            {
                return;
            }

            if (!field.ValidatesInline && !ValidateValue(field, newValue))
            {
                return;
            }

            field.Write(newValue);
            undoStack.Add(new FormChange(field, oldValue, newValue));
            redoStack.Clear();
            AfterChange();
        }

        private bool ValidateValue(FormField field, object value)
        {
            string generic = Text("form.invalid", "Invalid value");
            string? message = owner.Invoke(field.Control.Id, "Validate", () => FormReflection.Validate(Model, field.Property, value, generic), null);
            field.ShowError(message);
            return message == null;
        }

        /// <summary>Run a reflection call against the model through the consumer's callback guard (it is the consumer's code that runs).</summary>
        internal T Guard<T>(string elementId, string eventName, Func<T> func, T fallback) => owner.Invoke(elementId, eventName, func, fallback);

        internal void Guard(string elementId, string eventName, Action action) => owner.Invoke(elementId, eventName, action);

        private void AfterChange()
        {
            SyncButtons();
            RaiseForm("OnChanged", OnChanged);
        }

        private void RaiseForm(string eventName, Action<IUIForm>? callback)
        {
            if (callback != null)
            {
                owner.Invoke(Id, eventName, () => callback(this));
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  State
        // ---------------------------------------------------------------------------------------------------------

        private void TakeSnapshot()
        {
            snapshot.Clear();
            foreach (FormField field in fields)
            {
                snapshot[field.Property.Name] = field.Read();
            }
        }

        private void ClearHistory()
        {
            undoStack.Clear();
            redoStack.Clear();
        }

        /// <summary>Re-read the model into the inputs and clear validation messages, without recording edits.</summary>
        private void RefreshControls()
        {
            refreshing = true;
            try
            {
                foreach (FormField field in fields)
                {
                    field.SyncControl();
                    field.ShowError(null);
                }
            }
            finally
            {
                refreshing = false;
            }

            InvalidateLayout();
        }

        private void SyncButtons()
        {
            undoButton.Enabled = CanUndo;
            redoButton.Enabled = CanRedo;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Input
        // ---------------------------------------------------------------------------------------------------------

        // the form is layout-only: clicks on the gaps fall through unless a consumer attached a handler
        protected override bool IsHitTestVisible => HasPointerHandlers;

        /// <summary>Ctrl+Z / Ctrl+Y (or Ctrl+Shift+Z) bubbling from a focused field undo / redo.</summary>
        protected internal override bool HandleKey(UIKeyEvent e)
        {
            if (base.HandleKey(e))
            {
                return true;
            }

            if (!e.Ctrl || e.Alt)
            {
                return false;
            }

            if (e.Key == Keys.Z && !e.Shift)
            {
                Undo();
                return true;
            }

            if (e.Key == Keys.Y || (e.Key == Keys.Z && e.Shift))
            {
                Redo();
                return true;
            }

            return false;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Layout (vertical stack of the grid and the button row)
        // ---------------------------------------------------------------------------------------------------------

        protected override Vector2 MeasureCore(Vector2 available)
        {
            float height = 0, width = 0;
            int visible = 0;
            foreach (UIElement child in Children)
            {
                if (!child.Visible)
                {
                    continue;
                }

                Vector2 size = child.Measure(available);
                height += size.Y;
                width = Math.Max(width, size.X);
                visible++;
            }

            return new Vector2(width, height + (Spacing * Math.Max(0, visible - 1)));
        }

        protected override void ArrangeCore()
        {
            int cursor = Bounds.Y;
            foreach (UIElement child in Children)
            {
                if (!child.Visible)
                {
                    child.Arrange(new Rectangle(Bounds.X, Bounds.Y, 0, 0));
                    continue;
                }

                int extent = (int)Math.Ceiling(child.DesiredSize.Y);
                child.Arrange(new Rectangle(Bounds.X, cursor, Bounds.Width, extent));
                cursor += extent + Spacing;
            }
        }

        /// <summary>One committed edit: which field, and its value before / after.</summary>
        private sealed class FormChange
        {
            internal FormField Field { get; }
            internal object? OldValue { get; }
            internal object? NewValue { get; }

            internal FormChange(FormField field, object? oldValue, object? newValue)
            {
                Field = field;
                OldValue = oldValue;
                NewValue = newValue;
            }
        }
    }
}
