using Common.Logging;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using WpfUI.Properties; 

namespace WpfUI.Dialogs
{
    public partial class ConfigWindow : Window
    {
        public ConfigWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            TxtClientPath.Text = Settings.Default.ClientExePath;
            TxtServerPath.Text = Settings.Default.ServerExePath;

            if (Settings.Default.Protocol == "TCP")
            {
                RbTcp.IsChecked = true;
            }
            else
            {
                RbHttp.IsChecked = true;
            }
        }

        private void BtnBrowseClient_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filter = "Executable (*.exe)|*.exe" };
            if (dialog.ShowDialog() == true) TxtClientPath.Text = dialog.FileName;
        }

        private void BtnBrowseServer_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filter = "Executable (*.exe)|*.exe" };
            if (dialog.ShowDialog() == true) TxtServerPath.Text = dialog.FileName;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Settings.Default.ClientExePath = TxtClientPath.Text;
                Settings.Default.ServerExePath = TxtServerPath.Text;
                Settings.Default.Protocol = (RbTcp.IsChecked == true) ? "TCP" : "HTTP";

                Settings.Default.Save();

                LogManager.Instance.LogInfomation($"Configuration updated. Protocol={Settings.Default.Protocol}");

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"Failed to save settings: {ex.Message}");
                MessageBox.Show($"Failed to save settings: {ex.Message}", "Error");
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}