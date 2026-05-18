using System.Text;
using System.Security.Cryptography;
using System.IO;
using System.Windows;

namespace FileController_v2.VC
{
    //получение хэша из файла
    public static class _HashTools
    {
        public static string ComputeHash(string filePath)
        {
            using var sha = SHA256.Create();
            try
            {
                using var stream = File.OpenRead(filePath);
                var hash = sha.ComputeHash(stream);
                return BytesToString(hash);
            }
            catch {
                
                return "0";
            }
            
            
            
        }
        private static string BytesToString(byte[] bytes)
        {
            StringBuilder sb = new();
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
