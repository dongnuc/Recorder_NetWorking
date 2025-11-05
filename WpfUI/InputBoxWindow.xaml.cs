using System.Windows;
using System.Windows.Input;

namespace WpfUI
{
    public partial class InputBoxWindow : Window
    {
        public string InputText { get; private set; }

        public InputBoxWindow(string prompt, string defaultText = "")
        {
            InitializeComponent();
            TxtPrompt.Text = prompt;
            TxtInput.Text = defaultText;
            TxtInput.Focus();
            TxtInput.SelectAll();
        }
        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtInput.Text))
            {
                MessageBox.Show("Name cannot be empty.", "Warning");
                return;
            }
            InputText = TxtInput.Text;
            this.DialogResult = true;
            this.Close();
        }
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
        private void TxtInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { BtnOk_Click(sender, e); }
        }
    }
}