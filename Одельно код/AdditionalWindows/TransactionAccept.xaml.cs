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
using System.Windows.Threading;

namespace FileController_v2
{
    /// <summary>
    /// Логика взаимодействия для TransactionAccept.xaml
    /// </summary>
    public partial class TransactionAccept : Window
    {
        private DispatcherTimer _timer;
        private string TextYes = "OK";
        private string TextNo = "Cancel";
        private int _secondsLeft = 5;
        public TransactionAccept(string input = "Обнаружена транзакция, репозиторий N будет заблокирован. Автоматическое подтверждение", string textyes ="OK", string textno = "Cancel", int timer = 5)
        {
            
            InitializeComponent();

            _secondsLeft = timer;
            TextYes = textyes;
            TextNo = textno;
            OkButton.Content = TextYes + "(" + _secondsLeft.ToString() + ")";
            NoButton.Content = TextNo;
            
            MainText.Text = input;
            StartCountdown();
        }
        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
        private void StartCountdown()
        {
            OkButton.IsEnabled = true;

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);

            _timer.Tick += (s, e) =>
            {
                _secondsLeft--;

                OkButton.Content = TextYes + " (" + _secondsLeft.ToString() + ")";

                if (_secondsLeft < 0)
                {
                    _timer.Stop();
                    if (IsVisible)
                    {
                        DialogResult = true; // авто-OK
                        Close();
                    }
                }
            };

            _timer.Start();
        }
    }
}
