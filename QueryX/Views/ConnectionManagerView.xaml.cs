using System.Windows;

namespace QueryX.Views // Ensure namespace matches
{
    /// <summary>
    /// Interaction logic for ConnectionManagerView.xaml
    /// </summary>
    public partial class ConnectionManagerView : Window
    {
        public ConnectionManagerView()
        {
            InitializeComponent();
            // PasswordBox handling might require code-behind or attached properties
            // For now, password won't be bound automatically
        }

        // Optional: Helper to get password if not using advanced binding
        public string GetPassword() => passwordBox.Password;
    }
}