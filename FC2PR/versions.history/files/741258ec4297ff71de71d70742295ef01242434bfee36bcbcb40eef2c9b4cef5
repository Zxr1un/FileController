using FileController_v2.NO;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;

namespace FileController_v2
{
    public partial class SettingsW : Window
    {
        private User? SelectedUser => UsersList.SelectedItem as User;

        public SettingsW()
        {
            InitializeComponent();

            
        }

        public void UpdateData()
        {
            UserName.Text = MainProgramLogic.settings.UserName;
            UserID.Text = MainProgramLogic.settings.ID;
            ServerIP.Text = MainProgramLogic.settings.ServerIP;
            ServerPort.Text = MainProgramLogic.settings.ServerPort.ToString();
            CheckBoxServer.IsChecked = MainProgramLogic.settings.connect_to_server_at_start;
            NetworkAvaible.IsChecked = !MainProgramLogic.settings.avaibleNetworkOperations;
            NetworkUserName.Text = MainProgramLogic.settings.NetworkUserName;

            SaveFolderPath.Text = MainProgramLogic.settings.SavePath;
            MyIPDebug.Text = MainProgramLogic.settings.LocalIP;
            MyPortDebug.Text = MainProgramLogic.settings.LocalPort.ToString();

            UsersList.ItemsSource = MainProgramLogic.settings.Users;

            if (MainProgramLogic.settings.Users.Count > 0) UsersList.SelectedIndex = 0;
        }

        private void UsersList_SelectionChanged( object sender, SelectionChangedEventArgs e)
        {
            if (SelectedUser == null)
            {
                return;
            }
            DataContext = SelectedUser;
            userNameSel.Text = SelectedUser.Name;
            UserPasswordBox.Password = SelectedUser.Password;
            AccessToPush.IsChecked = SelectedUser.canPush;
        }

        private void AddUser_Click(object sender, RoutedEventArgs e)
        {
            User user = new()
            {
                Name = "New User"
            };

            MainProgramLogic.settings.Users.Add(user);

            UsersList.SelectedItem = user;
        }

        private void RemoveUser_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedUser == null)
                return;

            MainProgramLogic.settings.Users.Remove(SelectedUser);
        }

        private void AddPath_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedUser == null) return;

            RepositorySelection selector = new();

            if (selector.ShowDialog() != true) return;

            string? selectedPath = selector.SelectedRepository.WorkingDirectory;

            if (selectedPath == null)
                return;

            if (!SelectedUser.AvailablePaths.Contains(selectedPath))
            {
                SelectedUser.AvailablePaths.Add(selectedPath);
            }
        }

        private void RemovePath_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedUser == null)
                return;

            if (PathsList.SelectedItem is string path)
            {
                SelectedUser.AvailablePaths.Remove(path);
            }
        }

        private void ApplyChanges_Click(object sender, RoutedEventArgs e)
        {
            MainProgramLogic.settings.UserName = UserName.Text;
            MainProgramLogic.settings.ServerIP = ServerIP.Text;

            if (int.TryParse(ServerPort.Text, out int port))
            {
                MainProgramLogic.settings.ServerPort = port;
            }

            MainProgramLogic.settings.connect_to_server_at_start = CheckBoxServer.IsChecked == true;
            MainProgramLogic.settings.NetworkUserName = NetworkUserName.Text;

            MainProgramLogic.settings.SavePath = SaveFolderPath.Text;
            MainProgramLogic.settings.LocalIP = MyIPDebug.Text;
            if (int.TryParse(MyPortDebug.Text, out int port1))
            {
                MainProgramLogic.settings.LocalPort = port1;
            }
            if (SelectedUser != null)
            {
                SelectedUser.Name = userNameSel.Text;
                SelectedUser.Password = UserPasswordBox.Password;
                SelectedUser.canPush = AccessToPush.IsChecked == true; //не буду уже чинить
            }

            MainProgramLogic.SaveSettings();
            MainProgramLogic.MW.Title = "FileController*" + MainProgramLogic.settings.UserName;
            UpdateData();
            Hide();
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            CheckBoxServer.IsChecked = true;
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            CheckBoxServer.IsChecked = false;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Hide();
            e.Cancel = true;
        }

        private void SaveFolderPathButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog dialog = new OpenFolderDialog
            {
                Title = "Выберите папку"
            };

            if (dialog.ShowDialog() == true)
            {
                SaveFolderPath.Text = dialog.FolderName;
            }

            
        }

        private void CheckBox1_Checked(object sender, RoutedEventArgs e)
        {
            MainProgramLogic.settings.avaibleNetworkOperations = false;
            MainProgramLogic.SaveSettings();
        }

        private void CheckBox1_Unchecked(object sender, RoutedEventArgs e)
        {
            MainProgramLogic.settings.avaibleNetworkOperations = true;
            MainProgramLogic.SaveSettings();
        }
        
        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateData();
        }

        private void AccessToPush_Checked(object sender, RoutedEventArgs e)
        {
            if(SelectedUser != null)
            {
                SelectedUser.canPush = AccessToPush.IsChecked == true;
            }
        }

        private void AccessToPush_Unchecked(object sender, RoutedEventArgs e)
        {
            if (SelectedUser != null)
            {
                SelectedUser.canPush = AccessToPush.IsChecked == false;
            }
        }
    }
}