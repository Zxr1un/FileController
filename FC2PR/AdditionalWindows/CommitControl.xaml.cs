using FileController_v2.VC;
using Microsoft.Win32;
using System.Windows;

namespace FileController_v2
{
    /// <summary>
    /// Логика взаимодействия для CommitControl.xaml
    /// </summary>
    public partial class CommitControl : Window
    {
        private Commit commit;
        private Repository repository;
        public CommitControl(Commit commit, Repository Rep)
        {
            this.commit = commit;
            this.repository = Rep;
            InitializeComponent();
            MainProgramLogic.MW.MainGrid.IsEnabled = false;

            CommitName.Text = commit.Name;
            CommitTime.Text = commit.Time.ToString("yyyy:MM:dd HH:mm:ss");
            CommitID.Text = commit.ID;
            CommitParentID.Text = commit.ParentID;
            Owner.Text = commit.Owner;
        }

        private void DeleteCommitButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = System.Windows.MessageBox.Show(
                "Вы точно хотите удалить текущий commit(Будет удалён только текущий commit)?",
                "Подтвердить",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                repository.FO.DeleteCommit(commit);
            }
        }

        private void DeleteCommitButtonCascade_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = System.Windows.MessageBox.Show(
                "Вы точно хотите удалить последовательность commit(Будут удалены все потомки данного commit)?",
                "Подтвердить",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                MessageBoxResult result2 = System.Windows.MessageBox.Show(
                "Данное действие удалит все commit, созданные от текущего. Подтвердить?",
                "Подтвердить",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

                if (result2 == MessageBoxResult.Yes)
                {
                    repository.FO.DeleteCommit(commit, true);
                }
            }
        }

        private void SaveCommitButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog dialog = new OpenFolderDialog
            {
                Title = "Выберите папку"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    repository.FO.SaveCommitTo(commit, dialog.FolderName);
                    MessageBox.Show("Коммит успешно сохранён");
                }
                catch { MessageBox.Show("Ошибка сохранения"); }
                
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            MainProgramLogic.MW.MainGrid.IsEnabled = true;
        }
    }
}
