using QueryX.ViewModels; // For ViewModelBase

namespace QueryX.Models
{
    public class SqlTemplateEditable : ViewModelBase // Inherit for INotifyPropertyChanged
    {
        private string _sqlText = string.Empty;
        public string SqlText
        {
            get => _sqlText;
            set => SetProperty(ref _sqlText, value);
        }

        public SqlTemplateEditable() { }
        public SqlTemplateEditable(string text = "")
        {
            _sqlText = text;
        }

        // Override ToString for cases where the object itself might be displayed directly (though unlikely here)
        public override string ToString() => SqlText;
    }
}