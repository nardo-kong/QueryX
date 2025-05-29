using QueryX.Models; // For DatabaseConnectionInfo
using System;

namespace QueryX.ViewModels
{
    public class SelectableConnectionViewModel : ViewModelBase
    {
        private bool _isSelected;

        public Guid ConnectionId { get; }
        public string ConnectionName { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        // Store original connection info if needed for more details, but not strictly necessary for this UI
        // public DatabaseConnectionInfo OriginalConnectionInfo { get; }

        public SelectableConnectionViewModel(Guid id, string name, bool selected)
        {
            ConnectionId = id;
            ConnectionName = name;
            _isSelected = selected; // Initialize field directly to avoid premature notification
        }
    }
}