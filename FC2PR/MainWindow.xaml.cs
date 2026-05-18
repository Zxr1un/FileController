using FileController_v2.NO;
using FileController_v2.VC;
using Microsoft.Win32;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FileController_v2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public CommitGraphRenderer _renderer;
        private double _zoom = 1.0;
        private Point _lastMousePos;
        private bool _isPanning;

        public MainWindow()
        {
            MainProgramLogic.MW = this;
            InitializeComponent();
            MainProgramLogic.Initialize();
            _renderer = new CommitGraphRenderer(CommitCanvas, this);
            Transmission.tp = new();
            UpdateUI();
            Show();
            if (MainProgramLogic.settings.connect_to_server_at_start && MainProgramLogic.settings.avaibleNetworkOperations) _ = NetworkOperations.TryConnectToServer();
            if(MainProgramLogic.settings.avaibleNetworkOperations) _ = NetworkOperations.StartReceivingLoopForP2P();


            
        }
        private void SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog dialog = new OpenFolderDialog
            {
                Title = "Выберите папку"
            };

            if (dialog.ShowDialog() == true)
            {
                PathTextBox.Text = dialog.FolderName;
                MainProgramLogic.Selected_repo = null;
                UpdateUI();
            }
        }

        private void CreateRepoButton_Click(object sender, RoutedEventArgs e)
        {
            Repository repository = new Repository();
            bool success = repository.FO.CreateRepository(PathTextBox.Text);
            if (success) {
                MainProgramLogic.Repositories.Add(repository);
                MainProgramLogic.SaveSettings();
                UpdateUI();
            }
            else
            {

            }
        }

        private void AddExistRepository_Click(object sender, RoutedEventArgs e)
        {
            Repository? someRep = MainProgramLogic.Repositories.FirstOrDefault(c => c.WorkingDirectory == PathTextBox.Text);
            if (someRep != null)
            {
                System.Windows.MessageBox.Show("Такой репозиторий уже существует");
                return;
            }
            Repository repository = new Repository();
            bool success = repository.FO.OpenRepository(PathTextBox.Text);
            if (success)
            {
                MainProgramLogic.Repositories.Add(repository);
                MainProgramLogic.SaveSettings();
                UpdateUI();
            }

        }

        public async Task UpdateUI(bool deepSkan = false)
        {
            if(MainProgramLogic.SelectedCommit != null && MainProgramLogic.Selected_repo != null)
            {
                if (MainProgramLogic.SelectedCommit.ID == MainProgramLogic.Selected_repo.HEAD) LoadCommitButton.Content = "сбросить изменения";
                else LoadCommitButton.Content = "Загрузить выбранный";
            }
           
            RepoList.Items.Clear();
            MainProgramLogic.counter = 0;
            foreach (var repo in MainProgramLogic.Repositories)
            {
                RepoList.Items.Add(repo);
            }


            RepoList.DisplayMemberPath = "WorkingDirectory";
            if (MainProgramLogic.Selected_repo != null && MainProgramLogic.Repositories.Contains(MainProgramLogic.Selected_repo))
            {
                LoadRepository(MainProgramLogic.Selected_repo);
                if (deepSkan)
                {
                    await MainProgramLogic.Selected_repo.FO.scan(false);
                    MainProgramLogic.Selected_repo.FO.CompareWithLastCommit();
                }
                else await MainProgramLogic.Selected_repo.FO.scan(true);
                RepoList.SelectedItem = MainProgramLogic.Selected_repo;
                if (MainProgramLogic.SelectedCommit != null && MainProgramLogic.Selected_repo.Commits.Contains(MainProgramLogic.SelectedCommit))
                {
                    FilesGroupBox.Header = "Файлы" + $"({MainProgramLogic.SelectedCommit.Name})";
                    BuildFileTree(MainProgramLogic.SelectedCommit.Files);

                }
                else
                {
                    FilesGroupBox.Header = "Файлы (сейчас)";
                    BuildFileTree(MainProgramLogic.Selected_repo.FO.files);
                }
            }
            else
            {
                BuildFileTree(new());
                _renderer.Render(null);
                FilesGroupBox.Header = "Файлы";

            }
        }

        private void RepoList_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (RepoList.SelectedItem is Repository repo)
            {
                if (repo.isBlocked) return;
                MainProgramLogic.SelectedCommit = null;
                MainProgramLogic.Selected_repo = repo;
                if (!MainProgramLogic.Selected_repo.FO.CheckExisting())
                {
                    System.Windows.MessageBox.Show("Внимание, репозиторий не найден. Вероятно он был удалён.");

                    return;
                }
                UpdateUI();
                
            }
        }
        private async void LoadRepository(Repository repo)
        {

            PathTextBox.Text = repo.WorkingDirectory;
            MainProgramLogic.counter = repo.Commits.Count;

            repo.FO.files = new List<RepoFile>();
            await repo.FO.scan(true);
            //BuildFileTree(repo.FO.files);
            LoadCommits(repo);
        }
        private void LoadCommits(Repository repo)
        {
            _renderer.Render(repo);
        }
        private void BuildFileTree(List<RepoFile> files)
        {
            FileTree.Items.Clear();
            // Список папок, в которых есть изменённые файлы
            HashSet<string> changedDirectories = new();

            foreach (var file in files)
            {
                if (!file.NeedToStore) continue;
                string dir = Path.GetDirectoryName(file.Path);
                while (!string.IsNullOrEmpty(dir))
                {
                    changedDirectories.Add(dir);
                    dir = Path.GetDirectoryName(dir);
                }
            }
            foreach (var file in files)
            {
                string[] parts = file.Path.Split(Path.DirectorySeparatorChar);
                ItemCollection currentLevel = FileTree.Items;
                string currentPath = "";
                for (int i = 0; i < parts.Length; i++)
                {
                    string part = parts[i];
                    currentPath = Path.Combine(currentPath, part);
                    TreeViewItem existing = null;
                    foreach (TreeViewItem item in currentLevel)
                    {
                        string cleanHeader = item.Header.ToString().Replace(" *changes", "");
                        if (cleanHeader == part)
                        {
                            existing = item;
                            break;
                        }
                    }
                    if (existing == null)
                    {
                        bool isFile = i == parts.Length - 1;

                        string additional_part = "";

                        if (_renderer._selected == null && MainProgramLogic.Selected_repo != null)
                        {
                            if (isFile)
                            {
                                if (file.NeedToStore)
                                    additional_part = " *changes";
                            }
                            else
                            {
                                if (changedDirectories.Contains(currentPath))
                                    additional_part = " *changes";
                            }
                        }
                        TreeViewItem newItem = new TreeViewItem
                        {
                            Header = part + additional_part
                        };

                        currentLevel.Add(newItem);
                        existing = newItem;
                    }

                    currentLevel = existing.Items;
                }
            }
        }

        private void CreateCommitButton_Click(object sender, RoutedEventArgs e)
        {
            Repository repository = MainProgramLogic.Selected_repo;
            if (repository != null)
            {
                repository.FO.CreateAndSaveCommit();
                UpdateUI();
            }
        }

        private void LoadCommitButton_Click(object sender, RoutedEventArgs e)
        {
            if(MainProgramLogic.Selected_repo != null)
            {
                StatusLine.Text = "Загрузка коммита. Ожидайте";
                if (MainProgramLogic.SelectedCommit != null) MainProgramLogic.Selected_repo.FO.LoadCommit(MainProgramLogic.SelectedCommit);
                else MessageBox.Show("Не выбран коммит");
                UpdateUI();
                StatusLine.Text = "Готов к работе";
            }
            else
            {
                MessageBox.Show("Не выбран репозторий");
            }
        }

        private async void UdpateStatusButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainProgramLogic.Selected_repo != null)
            {
                StatusLine.Text = "Быстрое сканирование папок";
                await UpdateUI();
                StatusLine.Text = "Готов к работе";
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            MainProgramLogic.SaveSettings();
            Cleaner.clean(MainProgramLogic.Repositories);
            Application.Current.Shutdown();
        }


        private void CommitCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!(e.OriginalSource is Canvas)) return;
            _isPanning = true;
            _lastMousePos = e.GetPosition(this);
            CommitCanvas.CaptureMouse();
        }
        private void CommitCanvas_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isPanning = false;
            CommitCanvas.ReleaseMouseCapture();
        }
        private void CommitCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isPanning) return;
            if (e.OriginalSource is Border) return;

            var current = e.GetPosition(this);
            Vector delta = current - _lastMousePos;


            double viewportWidth = CommitViewport.ActualWidth;
            double viewportHeight = CommitViewport.ActualHeight;

            double canvasWidth = CommitCanvas.ActualWidth * _zoom;
            double canvasHeight = CommitCanvas.ActualHeight * _zoom;

            double minX = Math.Min(0, viewportWidth - canvasWidth);
            double minY = Math.Min(0, viewportHeight - canvasHeight);

            double maxX = 0;
            double maxY = 0;

            CanvasTranslate.X = Math.Clamp(CanvasTranslate.X + delta.X, minX, maxX);
            CanvasTranslate.Y = Math.Clamp(CanvasTranslate.Y + delta.Y, minY, maxY);


            //CanvasTranslate.X += delta.X;
            //CanvasTranslate.Y += delta.Y;

            _lastMousePos = current;
        }


        private void CommitCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double zoomFactor = e.Delta > 0 ? 1.1 : 1 / 1.1;

            _zoom *= zoomFactor;
            _zoom = Math.Clamp(_zoom, 0.2, 3.0);

            CanvasScale.ScaleX = _zoom;
            CanvasScale.ScaleY = _zoom;
        }

        private async void CheckChangesButton_Click(object sender, RoutedEventArgs e)
        {

            if (MainProgramLogic.Selected_repo != null)
            {
                StatusLine.Text = "Поиск изменений, это может занять время.";
                MainGrid.IsEnabled = false;
                await UpdateUI(true);
                MainGrid.IsEnabled = true;
                StatusLine.Text = "Готов к работе";
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            MainProgramLogic.SW.Show();
        }

        private void PathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (PathTextBox.Text.Length > 0) {
                CreateRepoButton.IsEnabled = true;
                AddExistRepository.IsEnabled = true;
                DeleteRepoButton.IsEnabled = true;
                ClearRepository.IsEnabled = true;
            }
            else
            {
                CreateRepoButton.IsEnabled = false;
                AddExistRepository.IsEnabled = false;
                DeleteRepoButton.IsEnabled = false;
                ClearRepository.IsEnabled = false;
            }
        }

        //кнопки сетевых функций меню
        private async void ConnectServerRetrButton(object sender, RoutedEventArgs e)
        {
            _ = NetworkOperations.TryConnectToServer();
        }

        private void ConnectRemoteButton(object sender, RoutedEventArgs e)
        {

        }

        private void RepoList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RepoList.SelectedItem != null) {
                if(RepoList.SelectedItem is Repository r)
                {
                    if (r.isBlocked)
                    {
                        RenameRepoButton.IsEnabled = false;
                        DeleteRepoButton.IsEnabled = false;
                        ClearRepository.IsEnabled = false;
                        MergeWithRepository.IsEnabled = false;
                        return;
                    }
                }
                RenameRepoButton.IsEnabled = true;
                DeleteRepoButton.IsEnabled= true;
                ClearRepository.IsEnabled = true;
                MergeWithRepository.IsEnabled = true;
            }
            else
            {
                RenameRepoButton.IsEnabled = false;
                DeleteRepoButton.IsEnabled = false;
                ClearRepository.IsEnabled = false;
                MergeWithRepository.IsEnabled = false;
            }
        }

        private void RenameRepoButton_Click(object sender, RoutedEventArgs e)
        {
            if(RepoList.SelectedItem != null)
            {
                if(RepoList.SelectedItem is Repository rep) new RenameWindow(rep, this);
            }
        }

        private void DeleteRepoButton_Click(object sender, RoutedEventArgs e)
        {
            if (RepoList.SelectedItem == null) return;
            MessageBoxResult result = MessageBox.Show(
                "Вы точно хотите удалить репозиторий из данной папки?",
                "Подтвердить",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                if (RepoList.SelectedItem != null) {
                    if(RepoList.SelectedItem is Repository rep)
                    {
                        rep.FO.DeleteRepo();
                        RepoList.SelectedItem = null;
                        UpdateUI();
                    }
                }
            }
        }

        private void ClearRepoButton_Click(object sender, RoutedEventArgs e)
        {
            if (RepoList.SelectedItem == null) return;
            MessageBoxResult result = MessageBox.Show(
                "Вы точно хотите удалить репозиторий из списка (репозиторий останется на диске)?",
                "Подтвердить",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                if (MainProgramLogic.Selected_repo != null)
                {
                    MainProgramLogic.Repositories.Remove(MainProgramLogic.Selected_repo);
                    MainProgramLogic.Selected_repo = null;
                    MainProgramLogic.SaveSettings();
                    UpdateUI();
                }
            }
        }

        private void NetworkFunctions_Click(object sender, RoutedEventArgs e)
        {
            if (!MainProgramLogic.settings.avaibleNetworkOperations)
            {
                MessageBox.Show("Сетевые операции отключены настройками");
                return;
            }
            MainProgramLogic.CS.Show();
            MainGrid.IsEnabled = false;
        }

        private async void MergeButton_Click(object sender, RoutedEventArgs e)
        {
            RepositorySelection selector = new();
            if (selector.ShowDialog() != true) return;
            if (RepoList.SelectedItem != null)
            {
                if (selector.SelectedRepository.isBlocked)
                {
                    MessageBox.Show("Репозиторий заблокирован на время сетевой операции, повторите позже");
                    return;
                }
                if(RepoList.SelectedItem is Repository rep) await rep.FO.MergeTo(selector.SelectedRepository);
            }
            else return;
            UpdateUI();
            MessageBox.Show("Слияние успешно");
        }
    }
}