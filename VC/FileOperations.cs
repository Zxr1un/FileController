using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FileController.VC
{
    public class FileOperations
    {
        public Repository Rep;

        public List<RepoFile> CurrentFiles { get; private set; } = new();
        private string HistoryPath => Path.Combine(Rep.VcDirectory, "history.json");

        

        public FileOperations(Repository rep)
        {
            Rep = rep;
        }

        // 📁 Создание репозитория
        public void CreateRepository(string path)
        {
            Rep.WorkingDirectory = path;

            Directory.CreateDirectory(Rep.VcDirectory);
            Directory.CreateDirectory(Rep.CommitsDirectory);
            Directory.CreateDirectory(Rep.FilesDirectory);

            Rep.History = new History();
            SaveHistory();

            Rep.IsReady = true;
        }
        private void SaveHistory()
        {
            Rep.History.LastCommitNumber = Rep.LastCommitNumber;
            JsonHelper.Save(HistoryPath, Rep.History);
        }

        // 🔍 Скан файлов
        public void ScanFiles()
        {
            CurrentFiles.Clear();

            ScanRecursive(Rep.WorkingDirectory);
        }

        private void ScanRecursive(string path)
        {
            foreach (var file in Directory.GetFiles(path))
            {
                if (file.Contains("versions.history")) continue;

                var relative = Path.GetRelativePath(Rep.WorkingDirectory, file);

                CurrentFiles.Add(new RepoFile
                {
                    Path = relative,
                    Hash = HashHelper.ComputeHash(file)
                });
            }

            foreach (var dir in Directory.GetDirectories(path))
            {
                if (dir.Contains("versions.history")) continue;
                ScanRecursive(dir);
            }
        }

        // ⚖️ Сравнение с последним коммитом
        public List<RepoFile> CompareWithLastCommit()
        {
            var result = new List<RepoFile>();

            var lastCommit = Rep.Commits.LastOrDefault();

            foreach (var file in CurrentFiles)
            {
                var old = lastCommit?.Files.FirstOrDefault(f => f.Path == file.Path);

                if (old == null || old.Hash != file.Hash)
                {
                    file.NeedToStore = true;
                }
                else
                {
                    file.StorageId = old.StorageId;
                }

                result.Add(file);
            }

            return result;
        }

        // 💾 Создание коммита
        public Commit CreateCommit(string name)
        {
            ScanFiles();

            var files = CompareWithLastCommit();

            foreach (var file in files.Where(f => f.NeedToStore))
            {
                StoreFile(file);
            }

            var commit = new Commit
            {
                Name = name,
                Number = ++Rep.LastCommitNumber,
                Time = DateTime.Now,
                Files = files
            };

            Rep.Commits.Add(commit);

            SaveCommit(commit);

            return commit;
        }

        // 📦 Сохранение файла в storage
        private void StoreFile(RepoFile file)
        {
            string id = Guid.NewGuid().ToString();
            file.StorageId = id;

            string source = Path.Combine(Rep.WorkingDirectory, file.Path);
            string dest = Path.Combine(Rep.FilesDirectory, id);

            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

            File.Copy(source, dest, true);
        }

        // 📄 Сохранение коммита
        private void SaveCommit(Commit commit)
        {
            string path = Path.Combine(Rep.CommitsDirectory, $"commit.{commit.Number}.json");

            JsonHelper.Save(path, commit);

            // добавляем в history
            Rep.History.Commits.Add(new CommitInfo
            {
                Number = commit.Number,
                Name = commit.Name,
                Time = commit.Time
            });

            SaveHistory();
        }

        // 🔄 Checkout
        public void Checkout(Commit commit)
        {
            foreach (var file in commit.Files)
            {
                RestoreFile(file);
            }

            Rep.CurrentCommitNumber = commit.Number;
        }

        private void RestoreFile(RepoFile file)
        {
            string source = Path.Combine(Rep.FilesDirectory, file.StorageId);
            string dest = Path.Combine(Rep.WorkingDirectory, file.Path);

            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

            File.Copy(source, dest, true);
        }

        public void LoadRepository(string path)
        {
            Rep.WorkingDirectory = path;

            if (!Directory.Exists(Rep.VcDirectory))
                return;

            Rep.History = JsonHelper.Load<History>(HistoryPath);

            Rep.LastCommitNumber = Rep.History.LastCommitNumber;

            Rep.Commits.Clear();

            foreach (var info in Rep.History.Commits)
            {
                string commitPath = Path.Combine(Rep.CommitsDirectory, $"commit.{info.Number}.json");

                if (File.Exists(commitPath))
                {
                    var commit = JsonHelper.Load<Commit>(commitPath);
                    Rep.Commits.Add(commit);
                }
            }

            Rep.IsReady = true;
        }
    }
}