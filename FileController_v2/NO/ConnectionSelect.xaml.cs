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

namespace FileController_v2.NO
{
    /// <summary>
    /// Логика взаимодействия для ConnectionSelect.xaml
    /// </summary>
    public partial class ConnectionSelect : Window
    {

        private MainWindow MW;
        public RemoteRepositoryWindow  RRW { get; set; }

        public ConnectionSelect(MainWindow mw)
        {
            MW = mw;
            InitializeComponent();
            RRW = new(this);
            _ = UpdateLoop();
        }

        public async Task UpdateLoop(int timing_ms = 5000)
        {
            while (true)
            {
                await Task.Delay(timing_ms);
                if (IsVisible && IsActive)
                {
                    UpdateData();
                }
                else return;
            }
        }



        public void UpdateData()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                UpdateDataSoft();
                IPTextBox.Text = MainProgramLogic.settings.ServerIP;
                PortTextBox.Text = MainProgramLogic.settings.ServerPort.ToString();
            });
            
        }
        public void UpdateDataSoft()
        {
            GroupIP.Header = "Сменить IP узла сервера/пользователя (текущий: " + MainProgramLogic.settings.ServerIP + ":" + MainProgramLogic.settings.ServerPort + ")";
            if (NetworkOperations.server_retranslator == null) ServerStatusText.Text = "Отключён";
            else if (NetworkOperations.server_retranslator.Connected != true) ServerStatusText.Text = "Соединение";
            else
            {
                ConnectServerButton.Content = "Подключиться к серверу";
                if (NetworkOperations.p2pMode)
                {
                    ServerStatusText.Text = "ПодключенP2P";
                    ConnectServerButton.Content = "Авторизироваться повторно";
                }
                else ServerStatusText.Text = "Подключен";

            }
            UsersListBox.ItemsSource = MainProgramLogic.UiUsers;
            P2PUsersListBox.Items.Clear();
            foreach (NodeListItem n in NetworkOperations.incoming_connections)
            {
                P2PUsersListBox.Items.Add(n);
            }
        }
        private async void ConnectServerButton_Click(object sender, RoutedEventArgs e)
        {
            if (NetworkOperations.p2pMode)
            {
                await NetworkOperations.GetNodes(NetworkOperations.server_retranslator);
            }
            _ = NetworkOperations.TryConnectToServer();
            UpdateData();
        }

        private async void UpdateDataButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateData();
            if (NetworkOperations.server_retranslator != null)
            {
                if (NetworkOperations.server_retranslator.Connected == true)
                {
                    await NetworkOperations.GetNodes(NetworkOperations.server_retranslator);
                }
            }
        }


        private  void UpdateServerDataButton_Click(object sender, RoutedEventArgs e)
        {
            MainProgramLogic.settings.ServerIP = IPTextBox.Text;
            try
            {
                MainProgramLogic.settings.ServerPort = Convert.ToInt32(PortTextBox.Text);
            }
            catch { }
            UpdateData();
            MainProgramLogic.SaveSettings();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            MW.MainGrid.IsEnabled = true;
            Hide();
        }

        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateData();
        }

        public async void ConnectToUserButton_Click(object sender, RoutedEventArgs e)
        {
            ConnectToUserButton.IsEnabled = false;
            if (UsersListBox.SelectedItem == null)
            {
                MessageBox.Show("Сначала выберите пользователя");
                return;
            }
            NetworkOperations.current_password = PasswordBox.Password;
            if (UsersListBox.SelectedItem is NodeListItem nli)
            {
                NetworkOperations.selectedUser.name = nli.name;
                NetworkOperations.selectedUser.id = nli.id;
            }
            forseConnection();
            
        }

        public async void forseConnection()
        {
            if (NetworkOperations.p2pMode)
            {
                bool cont = false;
                await Application.Current.Dispatcher.Invoke(async () =>
                {
                    PasswordInput pi = new();
                    pi.ShowDialog();
                    if (pi.DialogResult == true)
                    {
                        NetworkOperations.current_password = pi.password;
                        cont = true;
                    }
                    else {
                        ConnectToUserButton.IsEnabled = true;
                        
                    }
                });
                if (cont == false) return;
            }
            await Application.Current.Dispatcher.Invoke(async () =>
            {
                NetworkOperations.AccessLevel = 0;
                Packet p = new Packet();
                p.dest = NetworkOperations.selectedUser.id;
                await NetworkOperations.Ping(NetworkOperations.server_retranslator, p);
                await Task.Delay(1000);
                if (NetworkOperations.AccessLevel == 0) await Task.Delay(1000);
                if (NetworkOperations.AccessLevel == 0) await Task.Delay(1000);
                ConnectToUserButton.IsEnabled = true;
                if (NetworkOperations.AccessLevel == 0) return;
                if (RRW != null)
                {
                    if (RRW.IsLoaded && RRW.IsVisible)
                    {
                        RRW.Close();
                    }
                }
                RRW = new(this);
                if (NetworkOperations.AccessLevel == 1) RRW.SetRootAccess(1);
                else RRW.SetRootAccess(2);
                RRW.Show();
                MainProgramLogic.CS.Hide();
                ConnectToUserButton.IsEnabled = true;
            });
            



        }

        private void CloseConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            NetworkOperations.closeMainConnection();
            UpdateData();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            NetworkOperations.closeMainConnection();
            NetworkOperations.incoming_connections.Clear();
            UpdateData();
            Transmission.failureProtocol();
        }
    }
}
