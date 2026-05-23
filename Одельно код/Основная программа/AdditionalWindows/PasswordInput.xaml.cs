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
    /// Логика взаимодействия для PasswordInput.xaml
    /// </summary>
    public partial class PasswordInput : Window
    {
        bool res = false;
        public string password = "";
        public PasswordInput()
        {
            InitializeComponent();
            res = false;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            MainProgramLogic.CS.Show();
            DialogResult = res;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            password = PasswordField.Password;
            res = true;
            Close();
        }
    }
}
