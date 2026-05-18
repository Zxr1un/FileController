using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace FileController_v2.VC
{
    public class Repository
    {
        public string status = ""; //для отображения надписи блокировки
        public bool isBlocked { get; set; } = false;
        public string Name { get; set; } = "Unnamed";
        //затычка для инициализации репозитория при слиянии
        public string ID { get; set; } = Guid.NewGuid().ToString();
        public string HEAD { get; set; } //ID текущего коммита

        public long size { get; set; } = 0;
        public DateTime LastDate { get; set; } = DateTime.MinValue;
        //местоположения
        public string WorkingDirectory { get; set; } = "";
        //history
        public string VcDirectory => Path.Combine(WorkingDirectory, "versions.history");
        //индексированные файлы
        public string FilesDirectory => Path.Combine(VcDirectory, "files");
        //папка, где храниться json сливаемого репозитория
        public string MergeDirectory => Path.Combine(VcDirectory, "merge");
        //все коммиты
        public List<Commit> Commits { get; set; } = new();

        public json_History History { get; set; } = new();
        public FileOperations FO;

        public Repository()
        {
            FO = new(this); 
        }

        public long CalculateSize()
        {
            long sum = 0;
            foreach (Commit commit in Commits)
            {
                foreach (RepoFile rf in commit.Files)
                {
                    sum += rf.Size;
                }
            }
            size = sum;
            return sum;
        }

    }

    public class json_History
    {
        
        public DateTime LastDate { get; set; } = DateTime.MinValue;
        public string Name { get; set; } = "Unnamed";
        public string ID { get; set; } = Guid.NewGuid().ToString();
        public string HEAD { get; set; } = "-1";
        public List<json_commit_info> commits { get; set; } = new();

    }

    public class json_Repository
    {
        public string ID { get; set;} = Guid.NewGuid().ToString();
        public string Name { get; set; } = "Unnamed";
        public string WorkingDirectory { get; set; } = "";
    }
}
