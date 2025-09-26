using System.Windows;

namespace QueryX.Helpers
{
    /// <summary>
    /// Helper class for showing consistent dialog prompts
    /// </summary>
    public static class DialogHelper
    {
        /// <summary>
        /// Shows a save confirmation dialog with consistent messaging
        /// </summary>
        /// <param name="itemType">The type of item being saved (e.g., "query", "connection")</param>
        /// <param name="itemName">The name of the specific item</param>
        /// <returns>MessageBoxResult indicating user choice</returns>
        public static MessageBoxResult ShowSaveConfirmation(string itemType, string? itemName = null)
        {
            string message = string.IsNullOrWhiteSpace(itemName) 
                ? $"You have unsaved changes to the current {itemType}. Do you want to save them before continuing?"
                : $"You have unsaved changes to '{itemName}'. Do you want to save them before continuing?";

            return MessageBox.Show(
                message,
                "Unsaved Changes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question
            );
        }

        /// <summary>
        /// Shows a save confirmation dialog when closing a window
        /// </summary>
        /// <param name="itemType">The type of item being saved</param>
        /// <param name="itemName">The name of the specific item</param>
        /// <returns>MessageBoxResult indicating user choice</returns>
        public static MessageBoxResult ShowCloseConfirmation(string itemType, string? itemName = null)
        {
            string message = string.IsNullOrWhiteSpace(itemName)
                ? $"You have unsaved changes to the current {itemType}. Do you want to save them before closing?"
                : $"You have unsaved changes to '{itemName}'. Do you want to save them before closing?";

            return MessageBox.Show(
                message,
                "Unsaved Changes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question
            );
        }
    }
}