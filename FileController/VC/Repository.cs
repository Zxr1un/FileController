using System;
using System.Collections.Generic;
using System.Text.Json;
using System.IO;

namespace FileController.VC
{
    public class Repository
    {
        public History History { get; set; } = new(); //чисто для записи json
        public int LastCommitNumber { get; set; } = 0;
        public int CurrentCommitNumber { get; set; } = 0;
        public bool IsReady { get; set; } = false;

        public List<Commit> Commits { get; set; } = new();

        public string WorkingDirectory { get; set; } = "";
        public string VcDirectory => Path.Combine(WorkingDirectory, "versions.history");
        public string CommitsDirectory => Path.Combine(VcDirectory, "commits");
        public string FilesDirectory => Path.Combine(VcDirectory, "files");
    }


    public class History
    {
        public int LastCommitNumber { get; set; }
        public List<CommitInfo> Commits { get; set; } = new();
    }

    public class CommitInfo
    {
        public int Number { get; set; }
        public string Name { get; set; } = "";
        public DateTime Time { get; set; }
    }

    public static class JsonHelper
    {
        public static void Save<T>(string path, T obj)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string json = JsonSerializer.Serialize(obj, options);
            File.WriteAllText(path, json);
        }

        public static T Load<T>(string path)
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json)!;
        }
    }
}