using System;
using System.Collections.Generic;

namespace FileController.VC
{
    public class Commit
    {
        public string Name { get; set; } = "none";
        public string ID { get; set; } = Guid.NewGuid().ToString();
        public int Number { get; set; }
        public DateTime Time { get; set; } = DateTime.Now;
        public List<RepoFile> Files { get; set; } = new();
    }
}