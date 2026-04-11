namespace FileController.VC
{
    public class RepoFile
    {
        public string Path { get; set; } = "";     // относительный путь
        public string Hash { get; set; } = "";     // SHA256
        public string StorageId { get; set; } = ""; // имя в storage
        public bool NeedToStore { get; set; } = false;
    }
}