using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileController_v2.VC
{
    public class Commit
    {
        public string Owner { get; set; } = MainProgramLogic.settings.UserName;
        public string Name { get; set; } = "none";
        public string ID { get; set; } = Guid.NewGuid().ToString();
        public string ParentID { get; set; } = "-1";
        public DateTime Time { get; set; } = DateTime.Now;
        public List<RepoFile> Files { get; set; } = new();

        public bool IsHead => ID == MainProgramLogic.Selected_repo?.HEAD;
    }

    public class json_commit_info
    {
        public string Owner { get; set; } = MainProgramLogic.settings.UserName;
        public string ID { get; set; } = "Star_commit";
        public string name { get; set; } = "none";
        public string ParentID { get; set; } = "-1";
        public DateTime Time { get; set; }
        public List<RepoFile> Files { get; set; } = new();

        public static Commit Transform(json_commit_info jci)
        {
            Commit commit = new();
            commit.ID = jci.ID;
            commit.Name = jci.name;
            commit.Owner = jci.Owner;
            commit.Time = jci.Time;
            commit.ParentID = jci.ParentID;
            commit.Files = jci.Files.ToList();
            return commit;
        }
        public static json_commit_info Transform(Commit commit)
        {
            json_commit_info jci = new();
            jci.ID = commit.ID;
            jci.name = commit.Name;
            jci.Owner = commit.Owner;
            jci.Time = commit.Time;
            jci.ParentID = commit.ParentID;
            jci.Files = commit.Files.ToList();
            return jci;
        }


    }
}
