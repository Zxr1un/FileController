using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileController_v2.VC
{
    public static class Cleaner
    {
        public static void clean(ObservableCollection<Repository> reps)
        {
            foreach (var repo in reps)
            {
                OperateRep(repo);
            }
        }

        private static void OperateRep(Repository rep)
        {

            HashSet<string> usedHashes = new();
            foreach (var commit in rep.Commits)
            {
                foreach (var file in commit.Files)
                {
                    if (!string.IsNullOrEmpty(file.Hash))
                        usedHashes.Add(file.Hash);
                }
            }
            if (!Directory.Exists(rep.FilesDirectory)) return;

            foreach (var filePath in Directory.GetFiles(rep.FilesDirectory))
            {
                string fileName = Path.GetFileName(filePath);
                if (!usedHashes.Contains(fileName))
                {
                    try
                    {
                        File.Delete(filePath);
                    }
                    catch{}
                }
            }
            //чистка сетевого мусора и файлов слияний
            if (Directory.Exists(rep.MergeDirectory))
            {
                foreach (var file in Directory.GetFiles(rep.MergeDirectory))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch { }
                }

                foreach (var dir in Directory.GetDirectories(rep.MergeDirectory))
                {
                    try
                    {
                        Directory.Delete(dir, true);
                    }
                    catch { }
                }
            }
        }
    }
}
