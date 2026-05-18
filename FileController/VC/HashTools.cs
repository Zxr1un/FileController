using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace FileController.VC
{
    public static class HashHelper
    {
        public static string ComputeHash(string filePath)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha.ComputeHash(stream);
            return BytesToString(hash);
        }
        private static string BytesToString(byte[] bytes)
        {
            StringBuilder sb = new();
            foreach (var b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}