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
        public long total { get; set; } = 0;
        private long _complete = 0;
        public long complete {
            get
            {
                return _complete;
            }
            set
            {
                _complete = value;
                //UpdateProgress();
            }
        }

        
        public TransmissionProgress()
        {

            InitializeComponent();
        }
        public void UpdateProgress()
        {
            if (total <= 0)
            {
                ProgressBar.Value = 0;
                Progress.Text = "0 / 0";
                return;
            }

            double percent = (double)complete / total * 100;

            if (double.IsNaN(percent) || double.IsInfinity(percent))
                percent = 0;

            ProgressBar.Value = percent;
            Progress.Text = $"{complete} / {total}";
        }
        public void MarkAsSucess()
        {
            sucess = true;
            Progress.Text = "Успешно!";
            CloseButton.Content = "Закрыть";
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                if (IsLoaded && IsVisible)
                {
                    DialogResult = sucess;
                }
            }
            catch
            {
                // окно не диалоговое
            }
        }
    }
}
