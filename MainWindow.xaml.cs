using FileController.VC;
using System.Windows;
using System.Windows.Forms;


namespace FileController
{
    public partial class MainWindow : Window
    {
        Repository repo;
        FileOperations fo;

        public MainWindow()
        {
            InitializeComponent();

            repo = new Repository();
            fo = new FileOperations(repo);
        }

        // 📁 выбор папки
        private void SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new FolderBrowserDialog();

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                PathBox.Text = dialog.SelectedPath;
            }
        }

        // 🆕 создать репозиторий
        private void CreateRepo_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PathBox.Text)) return;

            fo.CreateRepository(PathBox.Text);

            System.Windows.MessageBox.Show("Репозиторий создан");
        }

        // 📂 открыть существующий
        private void OpenRepo_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PathBox.Text)) return;

            fo.LoadRepository(PathBox.Text);

            UpdateCommitsList();

            System.Windows.MessageBox.Show("Репозиторий загружен");
        }

        // 💾 commit
        private void Commit_Click(object sender, RoutedEventArgs e)
        {
            if (!repo.IsReady) return;

            string name = string.IsNullOrWhiteSpace(CommitNameBox.Text)
                ? $"Commit {repo.LastCommitNumber + 1}"
                : CommitNameBox.Text;

            var commit = fo.CreateCommit(name);

            UpdateCommitsList();

            System.Windows.MessageBox.Show($"Commit {commit.Number} создан");
        }

        // 🔄 загрузка commit
        private void LoadCommit_Click(object sender, RoutedEventArgs e)
        {
            if (CommitsList.SelectedItem is Commit commit)
            {
                fo.Checkout(commit);
                System.Windows.MessageBox.Show($"Загружен commit {commit.Number}");
            }
        }

        // 🔄 обновление списка
        private void UpdateCommitsList()
        {
            CommitsList.ItemsSource = null;
            CommitsList.ItemsSource = repo.Commits;
        }
    }
}