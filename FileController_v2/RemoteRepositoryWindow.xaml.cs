using FileController_v2.NO;
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
    /// Логика взаимодействия для RemoteRepositoryWindow.xaml
    /// </summary>
    public partial class RemoteRepositoryWindow : Window
    {
        public ConnectionSelect CS;
        public static List<RepositoryItem> RemoteRepos = new();
        public static string RemoteUserID = string.Empty;
        public RemoteRepositoryWindow(ConnectionSelect cs)
        {
            CS = cs;
            InitializeComponent();
            LocalMachineNameText.Text = MainProgramLogic.settings.NetworkUserName;
            if(NetworkOperations.selectedUser != null) RemoteMachineNameText.Text = NetworkOperations.selectedUser.name;
            try
            {
                RemoteUserID = NetworkOperations.selectedUser.id;
                
                
            }
            catch
            {
                TransactionAccept ta = new("Соединение потеряно, отключение.", "OK (3)", "", 3);
                ta.ShowDialog();
            }
            

        }
        public async Task InitUpdate()
        {
            MainGrid.IsEnabled = false;

            try
            {
                
                Packet p = new();
                p.dest = NetworkOperations.selectedUser.id;
                await NetworkOperations.HeadRepos(NetworkOperations.server_retranslator, p);
            }
            catch (Exception ex) {
            
                TransactionAccept ta = new("Соединение потеряно, отключение. " + ex.Message, "OK (6)", "", 6);
                ta.ShowDialog();
                Close();
            }
            await Task.Delay(3000);
            try
            {
                if (MainGrid.IsEnabled == false)
                {
                    MessageBox.Show("Не получил ответ");
                    MainGrid.IsEnabled = true;
                }
            }
            catch { }
            

        }
        public void UpdateData()
        {
            LocalRepoList.Items.Clear();
            foreach(Repository r in MainProgramLogic.Repositories)
            {
                RepositoryItem ri = new(r);
                LocalRepoList.Items.Add(ri);
            }
            RemoteRepoList.Items.Clear();
            foreach(RepositoryItem item in RemoteRepos)
            {
                RemoteRepoList.Items.Add(item);
            }
            MainGrid.IsEnabled = true;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                CS.Show();
            } catch { }
            CS.RRW = null;
        }
        public void SetRootAccess(int access = 1)
        {
            if (access == 1)
            {
                SendRepo.IsEnabled = false;
                MergeWithRemote.IsEnabled = false;
            }
            else
            {
                SendRepo.IsEnabled = true;
                MergeWithRemote.IsEnabled = true;
            }
        }

        private void UpdateDataButton_Click(object sender, RoutedEventArgs e)
        {
            InitUpdate();
        }

        private void GroupBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if(IsVisible) _ = InitUpdate();
        }

        private async void SendRepo_Click(object sender, RoutedEventArgs e)
        {
            if (LocalRepoList.SelectedItem != null) { 
                if(LocalRepoList.SelectedItem is RepositoryItem ri)
                {
                    await Transmission.StartsendOut(ri.repository);
                    

                }
            }
        }
    }
    public class RepositoryItem
    {
        public Repository  repository { get; set; }
        public string Name => repository.Name;
        public string Size => FormatSize(repository.CalculateSize());
        public int Commits => repository.Commits.Count;
        public string LastDate => repository.LastDate.ToString("yy:MM:dd HH:mm");
        public string ID => repository.ID;

        public RepositoryItem(Repository repository)
        {
            this.repository = repository;
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
