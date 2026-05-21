using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileController_v2.VC
{
    public class RepoFile
    {
        public string Path { get; set; } = "";     // относительный путь
        public string Hash { get; set; } = "";     // SHA256 а так же имя в files
        public long Size { get; set; } = 0;
        public bool NeedToStore = false;
    }
}
