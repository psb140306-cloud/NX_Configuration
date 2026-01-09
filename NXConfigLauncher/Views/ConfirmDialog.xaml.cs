using System.ComponentModel;
using System.Windows;

namespace NXConfigLauncher.Views
{
    public partial class ConfirmDialog : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private string _dialogTitle = "확인";
        private string _message = "";
        private string _warningMessage = "";
        private bool _hasWarning = false;

        public string DialogTitle
        {
            get => _dialogTitle;
            set
            {
                _dialogTitle = value;
                base.Title = value; // Window.Title 설정
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DialogTitle)));
            }
        }

        public string Message
        {
            get => _message;
            set
            {
                _message = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Message)));
            }
        }

        public string WarningMessage
        {
            get => _warningMessage;
            set
            {
                _warningMessage = value;
                HasWarning = !string.IsNullOrEmpty(value);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WarningMessage)));
            }
        }

        public bool HasWarning
        {
            get => _hasWarning;
            set
            {
                _hasWarning = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasWarning)));
            }
        }

        public bool Result { get; private set; } = false;

        public ConfirmDialog()
        {
            InitializeComponent();
            DataContext = this;
        }

        public ConfirmDialog(string title, string message, string? warningMessage = null) : this()
        {
            DialogTitle = title;
            Message = message;
            if (!string.IsNullOrEmpty(warningMessage))
            {
                WarningMessage = warningMessage;
            }
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            Result = true;
            DialogResult = true;
            Close();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 정적 메서드로 간편하게 대화상자 표시
        /// </summary>
        public static bool Show(string title, string message, string? warningMessage = null, Window? owner = null)
        {
            var dialog = new ConfirmDialog(title, message, warningMessage);
            if (owner != null)
            {
                dialog.Owner = owner;
            }
            dialog.ShowDialog();
            return dialog.Result;
        }
    }
}
