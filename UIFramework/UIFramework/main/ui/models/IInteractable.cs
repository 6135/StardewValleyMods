using System;

namespace UIFramework.main.ui.models
{
    /// <summary>
    /// An object that can handle user interactions.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Handles user input.
        /// </summary>
        void HandleInput();

        /// <summary>
        /// Called when the object is clicked.
        /// </summary>
        event EventHandler Clicked;

        /// <summary>
        /// Called when the object is hovered over.
        /// </summary>
        event EventHandler Hovered;
    }
}