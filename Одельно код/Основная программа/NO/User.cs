using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FileController_v2.NO
{
    //класс для локального хранения
    public class User
    {
        public string ID { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = MainProgramLogic.settings.UserName;
        //реальный пароль (не хэшированный)
        public string Password { get; set; } = "";
        public bool canPush { get; set; } = true;
        public ObservableCollection<string> AvailablePaths { get; set; } = new();

    }
    //класс для передачи данных
    public class Remote_User
    {
        public string ID { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = MainProgramLogic.settings.UserName;

        //Хэш пароля со временем
        public string Password { get; set; }
        //для смешения хэша пароля
        public string passDate { get; set; }

        public ObservableCollection<string> AvailablePaths = new(); //не для отправки
        public bool canPush = true; //не для отправки

        public void HashPassword(string password)
        {
            using var sha = SHA256.Create();

            passDate = DateTime.UtcNow.ToString("dd:HH:mm");

            string saltedPassword = password + passDate;

            byte[] bytes = Encoding.UTF8.GetBytes(saltedPassword);

            byte[] hash = sha.ComputeHash(bytes);

            Password = BytesToString(hash);
        }
        public static string HashPasswordWithCurrentTime(string password, string time)
        {
            using var sha = SHA256.Create();
            string saltedPassword = password + time;
            byte[] bytes = Encoding.UTF8.GetBytes(saltedPassword);
            byte[] hash = sha.ComputeHash(bytes);
            return BytesToString(hash);
        }

        private static string BytesToString(byte[] bytes)
        {
            StringBuilder sb = new();
            foreach (byte b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        public static bool CheckUser(Remote_User ru)
        {
            foreach(User u in MainProgramLogic.settings.Users)
            {
                if(u.Name == ru.Name)
                {
                    if (ru.Password == HashPasswordWithCurrentTime(u.Password, ru.passDate)) return true;
                }
            }
            return false;
        }

    }
}
