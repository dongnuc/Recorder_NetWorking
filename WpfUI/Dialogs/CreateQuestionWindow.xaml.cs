using System; 
using System.Windows;

namespace WpfUI
{
    public partial class CreateQuestionWindow : Window
    {
        public string QuestionName { get; private set; }
        public bool UseDatabase { get; private set; }

        public CreateQuestionWindow()
        {
            InitializeComponent();
            TxtQuestionName.Text = $"Question_{DateTime.Now:yyyyMMdd_HHmmss}";
            TxtQuestionName.Focus();
            TxtQuestionName.SelectAll();
        }

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtQuestionName.Text))
            {
                MessageBox.Show("Question Name cannot be empty.", "Warning");
                return;
            }

            this.QuestionName = TxtQuestionName.Text;
            this.UseDatabase = RbDbYes.IsChecked == true;

            this.DialogResult = true;
            this.Close();
        }
    }
}