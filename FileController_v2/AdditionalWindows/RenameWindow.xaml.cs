using FileController_v2.VC;
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

namespace FileController_v2
{
    /// <summary>
    /// Логика взаимодействия для RenameWindow.xaml
    /// </summary>
    public partial class RenameWindow : Window
    {
        private Repository repository;
        private MainWindow MW;
        public RenameWindow(Repository repo, MainWindow mw)
        {
            MW = mw;
            repository = repo;
            InitializeComponent();
            IDfield.Text = "ID: " + repo.ID;
            NameField.Text = repo.Name;
            repo.CalculateSize();
            SizeField.Text = FormatSize(repo.size);
            Datefield.Text = repo.LastDate.ToString("yy:MM:dd HH:mm");
            mw.MainGrid.IsEnabled = false;
            Show();
        }

        private void NameField_TextChanged(object sender, TextChangedEventArgs e)
        {
            repository.Name = NameField.Text;
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            repository.FO.SaveHistory();
            await MW.UpdateUI();
            MW.MainGrid.IsEnabled = true;
        }

        public static string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };

            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }
}
