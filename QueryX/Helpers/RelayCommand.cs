using System;
using System.Windows.Input; // Required for ICommand
using System.Windows; // Required for Application, Dispatcher

namespace QueryX.Helpers // Ensure namespace matches your project
{
    // A command whose sole purpose is to relay its functionality
    // to other objects by invoking delegates.
    // The default return value for the CanExecute method is 'true'.
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute; // Action to execute
        private readonly Predicate<object?>? _canExecute; // Function to determine if command can execute

        // Initializes a new instance of the RelayCommand class.
        // parameter execute: The execution logic.
        // parameter canExecute: The execution status logic.
        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        // Occurs when changes occur that affect whether or not the command should execute.
        // WPF's CommandManager automatically hooks into this for UI updates (like button enable/disable)
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        // ---> ADD THIS METHOD <---
        /// <summary>
        /// Raises the CanExecuteChanged event to indicate that the ability of the command to execute has changed.
        /// Call this method whenever conditions affecting CanExecute might have changed.
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            // Use Application.Current.Dispatcher to ensure the event is raised on the UI thread,
            // which is important for WPF command bindings.
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                // Use CommandManager.InvalidateRequerySuggested() as a robust way
                // or directly invoke the event handlers if needed, though CommandManager is often preferred.
                CommandManager.InvalidateRequerySuggested();

                // Direct invocation (less common if CommandManager is used):
                // PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null)); // If using different base
                // CanExecuteChanged?.Invoke(this, EventArgs.Empty); // Simpler direct invocation
            });

        }
        // ---> END OF ADDED METHOD <---

        // Defines the method that determines whether the command can execute in its current state.
        // parameter parameter: Data used by the command. If the command does not require data to be passed, this object can be set to null.
        // returns: true if this command can be executed; otherwise, false.
        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        // Defines the method to be called when the command is invoked.
        // parameter parameter: Data used by the command. If the command does not require data to be passed, this object can be set to null.
        public void Execute(object? parameter)
        {
            _execute(parameter);
        }
    }
}