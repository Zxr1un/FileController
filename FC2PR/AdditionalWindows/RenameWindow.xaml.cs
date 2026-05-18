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
    }
}
