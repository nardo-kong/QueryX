// In ViewModels/QueryTreeNodeViewModel.cs
using QueryX.Models;
using System.Collections.ObjectModel;
using System.Linq;

namespace QueryX.ViewModels
{
    public class QueryTreeNodeViewModel : ViewModelBase
    {
        private string _name = string.Empty;
        private bool _isExpanded;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public bool IsFolder { get; }
        public QueryDefinition? Query { get; } // Null if it's a folder

        public ObservableCollection<QueryTreeNodeViewModel> Children { get; } = new ObservableCollection<QueryTreeNodeViewModel>();

        public bool IsExpanded // For TreeView binding
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }


        // Constructor for a folder node
        public QueryTreeNodeViewModel(string folderName)
        {
            Name = folderName;
            IsFolder = true;
            Query = null;
            IsExpanded = false; // Folders usually start collapsed
        }

        // Constructor for a query leaf node
        public QueryTreeNodeViewModel(QueryDefinition query)
        {
            Query = query ?? throw new ArgumentNullException(nameof(query));
            Name = query.Name; // Leaf node name is the query name
            IsFolder = false;
        }

        // Helper to find or add a child folder
        public QueryTreeNodeViewModel GetOrAddChildFolder(string folderName)
        {
            var existingFolder = Children.FirstOrDefault(c => c.IsFolder && c.Name.Equals(folderName, StringComparison.OrdinalIgnoreCase));
            if (existingFolder == null)
            {
                existingFolder = new QueryTreeNodeViewModel(folderName);
                Children.Add(existingFolder);
            }
            return existingFolder;
        }
    }
}