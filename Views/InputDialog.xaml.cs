using System.Windows;
using System.Windows.Input;

namespace DongNoti.Views
{
    public partial class InputDialog : Window
    {
        public string Answer => InputTextBox.Text;

        public string Prompt
        {
            get => PromptTextBlock.Text;
            set => PromptTextBlock.Text = value;
        }

        public InputDialog()
        {
            InitializeComponent();
            InputTextBox.Focus();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(InputTextBox.Text))
            {
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("값을 입력해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OK_Click(sender, e);
            }
        }
    }
}
