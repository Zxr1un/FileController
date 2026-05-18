using System.IO;
using System.Windows;


namespace FileController_v2.VC
{
    public class FileOperations
    {
        private Repository Rep;
        public List<RepoFile> files = new();
        public FileOperations(Repository repos) {
            Rep = repos;
        }
        //полная проверка текущего репозитория на изменения
        public async Task scan(bool quickHash = false)
        {
            List<RepoFile> tempFiles = new();
            await Task.Run(() =>
            {
                ScanRecursive(Rep.WorkingDirectory, quickHash, tempFiles);
            });
            files = tempFiles;
        }
        private void ScanRecursive(string path, bool quickHash, List<RepoFile> target)
        {
            foreach (string file_pth in Directory.GetFiles(path))
            {
                if (file_pth.Contains("versions.history")) continue;
                string relative = Path.GetRelativePath(Rep.WorkingDirectory, file_pth);
                RepoFile RF = new RepoFile();
                RF.Path = relative;
                RF.Size = new FileInfo(file_pth).Length;
                if (!quickHash) RF.Hash = _HashTools.ComputeHash(file_pth);
                target.Add(RF);
            }

            foreach (var dir in Directory.GetDirectories(path))
            {
                if (dir.Contains("versions.history")) continue;
                ScanRecursive(dir, quickHash, target);
            }
        }
        //инициализация репозитория по пути
        public bool CreateRepository(string path, bool ignoreMessage = false)
        {
            Rep.WorkingDirectory = path;
            try
            {
                if(Directory.Exists(Rep.VcDirectory) || File.Exists(Rep.VcDirectory)){
                    if(!ignoreMessage)  MessageBox.Show("В данной папке уже существует репозиторий или файл с именем \"versions.history\"");
                    return false;
                }
            }
            catch { }
            try
            {
                if (!Directory.Exists(Rep.VcDirectory)) Directory.CreateDirectory(Rep.VcDirectory);

            }
            catch {}
            try
            {
                if (!Directory.Exists(Rep.FilesDirectory)) Directory.CreateDirectory(Rep.FilesDirectory);
            }
            catch {}
            try
            {
                if (!Directory.Exists(Rep.MergeDirectory)) Directory.CreateDirectory(Rep.MergeDirectory);
            }
            catch { }
            DirectoryInfo dir = new DirectoryInfo(Rep.VcDirectory);
            dir.Attributes |= FileAttributes.System | FileAttributes.Hidden; //скрытая и системная папка
            CreateAndSaveCommit();
            Rep.History = new json_History();
            Rep.History.ID = Rep.ID;
            Rep.History.Name = Rep.Name;
            SaveHistory();
            return true;
        }

        public bool OpenRepository(string path, bool ignoreMessage = false)
        {
            try
            {
                if (!Directory.Exists(Path.Combine(path, Rep.VcDirectory)))
                {
                    if (!ignoreMessage) MessageBox.Show($"В данной папке {Path.Combine(path, Rep.VcDirectory)} нет репозитория");
                    return false;
                }
            }
            catch { }

            Rep.WorkingDirectory = path;
            LoadHistory();
            return true;
        }
        //public bool OpenRepository
        public void SaveHistory()
        {
            UpdateWithoutSaveHistory();
            _jsonTools.Save(Path.Combine(Rep.VcDirectory, "history.json"), Rep.History);
        }

        public void UpdateWithoutSaveHistory()
        {
            Rep.LastDate = DateTime.Now;
            Rep.History.Name = Rep.Name;
            Rep.History.ID = Rep.ID;
            Rep.History.HEAD = Rep.HEAD;
            Rep.History.LastDate = Rep.LastDate;
            Rep.History.commits.Clear();
            foreach (var commit in Rep.Commits)
            {
                Rep.History.commits.Add(json_commit_info.Transform(commit));
            }
        }
        public void CreateAndSaveCommit()
        {
            Commit commit = new Commit();
            commit.ParentID = Rep.HEAD;
            if( Rep.Commits.Count == 0)
            {
                commit.Name = "Initial";
                commit.ID = MainProgramLogic.settings.StartCommitID;
                commit.ParentID = "-1";
                files.Clear();

                commit.Files = files;
                json_commit_info commit_Info_Init = new json_commit_info();
                commit_Info_Init.Files = commit.Files;
                commit_Info_Init.name = commit.Name;
                commit_Info_Init.ParentID = commit.ParentID;
                commit_Info_Init.ID = commit.ID;
                commit_Info_Init.Time = DateTime.Now;

                Rep.History.commits.Add(commit_Info_Init);
                Rep.Commits.Add(commit);
                SaveHistory();
                Rep.HEAD = commit.ID;
                return;
            }

            Task scan_task = new Task(() =>scan());
            scan_task.Start();

            CommitCreation CC = new CommitCreation(commit);
            bool? result = CC.ShowDialog();
            if (result == false) {
                return;
            }
            commit.ParentID = Rep.HEAD;

            scan_task.Wait();   
            List<RepoFile> toStorage = CompareWithLastCommit();


            int counter = 0;
            foreach (RepoFile file in toStorage) {
                if (file.NeedToStore)
                {
                    StoreFile(file);
                    counter++;
                }
            }
            if (counter == 0) {
                Commit? HeadCommit = Rep.Commits.Find(c => c.ID == Rep.HEAD);
                if (HeadCommit != null) {
                    if (HeadCommit.Files.Count == toStorage.Count) {
                        System.Windows.MessageBox.Show("Нет изменений с предыдущего коммита");
                        return;
                    }
                }
            }  
            commit.Files = toStorage;
            json_commit_info commit_Info = new json_commit_info();
            commit_Info.Files = commit.Files;
            commit_Info.name = commit.Name;
            commit_Info.ParentID = commit.ParentID;
            commit_Info.ID = commit.ID;
            commit_Info.Time = DateTime.Now;

            Rep.History.commits.Add(commit_Info);
            Rep.Commits.Add(commit);
            Rep.HEAD = commit.ID;

            SaveHistory();
        }


        public void LoadHistory()
        {
            json_History JH = _jsonTools.Load<json_History>(Path.Combine(Rep.VcDirectory, "history.json"));
            Rep.HEAD = JH.HEAD;
            Rep.History.HEAD = JH.HEAD;
            Rep.Name = JH.Name;
            Rep.ID = JH.ID;
            Rep.Commits.Clear();
            foreach (json_commit_info jci in JH.commits) {
                Rep.Commits.Add(json_commit_info.Transform(jci));
                Rep.History.commits.Add(jci);
            }
        }
        public void LoadCommit(Commit commit)
        {
            //if( Rep.HEAD == commit.ID)
            //{
            //    MessageBox.Show("Вы уже на этом commit");
            //    return;
            //}
            MessageBoxResult result = MessageBox.Show(
                "Загрузить commit (все не сохранённые изменения будут удалены)?",
                "Подтвердить",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                ClearWorkingDirectory();
                foreach (var file in commit.Files)
                {
                    RestoreFile(file);
                }
                Rep.HEAD = commit.ID;
                SaveHistory();
            }
        }
        private void RestoreFile(RepoFile file)
        {
            string source = Path.Combine(Rep.FilesDirectory, file.Hash);
            string dest = Path.Combine(Rep.WorkingDirectory, file.Path);

            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(source, dest, true);
        }
        public void SaveCommitTo(Commit commit, string path)
        {
            try
            {
                foreach (var file in commit.Files)
                {
                    RestoreFileToDir(file, path);
                }
            }
            catch {
                MessageBox.Show("Загрузка не удалась");
            }
            
        }
        private void RestoreFileToDir(RepoFile file, string path)
        {
            string source = Path.Combine(Rep.FilesDirectory, file.Hash);
            string dest = Path.Combine(path, file.Path);

            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(source, dest, true);
        }

        public bool DeleteCommit(Commit commit, bool cascadeMod = false, bool final = true, bool dangerMode = false)
        {
            bool result = true;
            if(commit.ID == "0")
            {
                System.Windows.MessageBox.Show("Невозможно удалить инициализирующий commit");
                return false;
            }
            List<Commit> ch_commit = Rep.Commits.FindAll(c => c.ParentID == commit.ID);
            if (cascadeMod)
            {
                if (commit.ParentID == "-1" && !dangerMode)
                {
                    System.Windows.MessageBox.Show("Невозможно вручную запустить полное удаление всех коммитов сразу");
                    return false;
                }
                if (commit.ID == Rep.HEAD)
                {
                    Rep.HEAD = commit.ParentID;
                }
                foreach (var ch in ch_commit)
                {
                    result = result && DeleteCommit(ch, true, false);
                }
            }
            else
            {
                if (commit.ParentID != "-1")
                {
                    foreach (Commit ch in ch_commit) ch.ParentID = commit.ParentID;
                    if (commit.ID == Rep.HEAD) Rep.HEAD = commit.ParentID;
                }
                else
                {

                    if (ch_commit.Count > 0)
                    {
                        Commit parent = ch_commit[0];
                        ch_commit[0].ParentID = "-1";
                        if (Rep.HEAD == commit.ID) Rep.HEAD = parent.ID;
                        for (int i = 1; i < ch_commit.Count; i++)
                        {
                            ch_commit[i].ParentID = parent.ID;
                        }
                    }
                }
            }
            Rep.Commits.Remove(commit);
            if (final)
            {
                SaveHistory();
                MainProgramLogic.MW.UpdateUI();
            }
            return result;
        }

        public List<RepoFile> CompareWithLastCommit()
        {
            List<RepoFile> result = new List<RepoFile>();

            Commit? HeadCommit = Rep.Commits.Find(c => c.ID == Rep.HEAD);

            foreach (RepoFile file in files)
            {
                RepoFile? old = HeadCommit?.Files.FirstOrDefault(f => f.Path == file.Path);

                if (old == null || old.Hash != file.Hash)
                {
                    file.NeedToStore = true;
                }
                else
                {
                    file.Hash = old.Hash;
                }

                result.Add(file);
            }

            return result;
        }
        private void StoreFile(RepoFile file)
        {
            string source = Path.Combine(Rep.WorkingDirectory, file.Path);
            string dest = Path.Combine(Rep.FilesDirectory, file.Hash);
            //на случай если не создалось
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(source, dest, true);
        }
        private void ClearWorkingDirectory()
        {
            foreach (var file in Directory.GetFiles(Rep.WorkingDirectory, "*", SearchOption.AllDirectories))
            {
                if (file.Contains("versions.history"))
                    continue;

                File.Delete(file);
            }

            foreach (var dir in Directory.GetDirectories(Rep.WorkingDirectory, "*", SearchOption.AllDirectories))
            {
                if (dir.Contains("versions.history"))
                    continue;

                try
                {
                    Directory.Delete(dir, true);
                }
                catch
                {
                    // папка может быть уже удалена или не пустая — игнорируем
                }
            }
        }

        public async Task MergeTo(Repository repository)
        {
            Repository merged_repo = new();
            merged_repo.WorkingDirectory = Rep.MergeDirectory;
            // очистка merge папки
            if (Directory.Exists(merged_repo.WorkingDirectory))
            {
                Directory.Delete(merged_repo.WorkingDirectory, true);
            }
            Directory.CreateDirectory(merged_repo.WorkingDirectory);
            Directory.CreateDirectory(merged_repo.FilesDirectory);
            merged_repo.ID = repository.ID;
            merged_repo.Name = repository.Name;

            foreach (Commit commit in Rep.Commits) merged_repo.Commits.Add(commit);
            foreach (Commit commit in repository.Commits) if (!merged_repo.Commits.Any( c => c.ID == commit.ID)) merged_repo.Commits.Add(commit);

            // копирование файлов первого repo
            if (Directory.Exists(Rep.FilesDirectory))
            {
                foreach (string file in Directory.GetFiles( Rep.FilesDirectory))
                {
                    string dest = Path.Combine( merged_repo.FilesDirectory, Path.GetFileName(file));
                    File.Copy(file, dest, true);
                }
            }
            // копирование файлов второго repo
            if (Directory.Exists(repository.FilesDirectory))
            {
                foreach (string file in Directory.GetFiles(repository.FilesDirectory))
                {
                    string dest = Path.Combine(merged_repo.FilesDirectory, Path.GetFileName(file));
                    File.Copy(file, dest, true);
                }
            }

            merged_repo.HEAD = repository.HEAD;
            merged_repo.FO.SaveHistory();

            

            // недостающие files
            foreach (string file in Directory.GetFiles(
                merged_repo.FilesDirectory))
            {
                string dest = Path.Combine(
                    repository.FilesDirectory,
                    Path.GetFileName(file));

                if (!File.Exists(dest))
                {
                    File.Copy(file, dest);
                }
            }
            File.Copy(Path.Combine(merged_repo.VcDirectory, "history.json"), Path.Combine(repository.VcDirectory, "history.json"), true);

            MessageBoxResult result = MessageBox.Show(
                "Хотите удалить вливаемый репозиторий?",
                "Подтвердить",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                DeleteRepo();
            }
            await Task.CompletedTask;
        }
        //с учётом догрузки файлов
        public async Task NetworkMergeTo(Repository repository)
        {


        }
        public bool CheckExisting()
        {
            try
            {
                if (Directory.Exists(Rep.VcDirectory) || File.Exists(Rep.VcDirectory))
                {
                    return true;
                }
            }
            catch { }
            return false;
        }

        public bool DeleteRepo()
        {
            if(Rep == null) return false;
            if(!CheckExisting()) return false;
            try
            {
                Directory.Delete(Rep.VcDirectory, true);
                MainProgramLogic.Repositories.Remove(Rep);
                MainProgramLogic.SaveSettings();
                return true;
            }
            catch {
                return false;
            }
            

            return false;
        }

    }
}
