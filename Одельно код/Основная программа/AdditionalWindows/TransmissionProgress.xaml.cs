using FileController_v2.NO;
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
    /// Логика взаимодействия для TransmissionProgress.xaml
    /// </summary>
    public partial class TransmissionProgress : Window
    {
        bool sucess = false;
        public long total { get; set; } = 1;
        public long complete { get; set; } = 0;
        bool finished { get; set; } = false;




        public TransmissionProgress()
        {
            InitializeComponent();
            _ = UpdateLoop();
        }

        public async void Reinit(string processName = "")
        {
            Show();
            if(processName != "") ProcessName.Text = processName;
            ProgressBar.Foreground = Brushes.Green;
            finished = false; 
        }

        public async Task UpdateLoop()
        {
            while (true)
            {
                total = Transmission.total;
                complete = Transmission.complete;
                if(!finished) UpdateProgress();
                await Task.Delay(300);
            }

        }
        public void UpdateProgress()
        {
            if (total <= 0)
            {
                ProgressBar.Value = 0;
                Progress.Text = "0 / 0";
                return;
            }
            double percent = 1;
            if (total > 0.01) percent = (double)complete / total * 100;

            ProgressBar.Value = percent;
            Progress.Text = $"{complete} / {total}" + "\n" + percent.ToString() + "%";
        }
        public void MarkAsSucess(string message = "Успешно!")
        {
            Reinit();
            try
            {
                finished = true;
                sucess = true;
                Progress.Text = message;
                CloseButton.Content = "Закрыть";
                ProgressBar.Value = 100;
                ProgressBar.Foreground = Brushes.DarkGreen;
            }
            catch  { }
        }
        public void MarkAsFailure(string message = "Ошибка передачи")
        {
            Reinit();
            try
            {
                finished = true;
                sucess = false;
                Progress.Text = message;
                CloseButton.Content = "Закрыть";
                ProgressBar.Value = 100;
                ProgressBar.Foreground = Brushes.Red;
            }
            catch { }
            
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

            Close();
        }
        
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
            if (Transmission.isActive)
            {
                Transmission.failureProtocol(Transmission.remoteID);
            }
            finished = true;
        }


        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                
            }
        }
    }
}
