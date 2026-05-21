using FileController_v2.NO;
using FileController_v2.VC;
using System.Collections.ObjectModel;
using System.Configuration;
using System.IO;
using System.Text.Json;
using System.Windows;


namespace FileController_v2
{
    //хранит все сохранённые репозитории программы, а так же пароль для удалённого доступа
    public static class MainProgramLogic
    {
        //общие настройки
        public static SettingsData settings = new SettingsData();
        public static SettingsW SW = new();
        public static MainWindow MW;
        public static ConnectionSelect CS;


        
        public static ObservableCollection<Repository> Repositories = new();
        public static string config_path = "Settings.json"; //файл с репозиториями, логинами и паролями
        public static string networkTempPath => settings.SavePath + "//Network";
        public static string downloadPath => settings.SavePath;

        public static Repository Selected_repo = null;
        public static Commit SelectedCommit = null;

        public static ObservableCollection<NodeListItem> UiUsers { get; } = new(); //копия для UI из NetworkOperations

        public static int counter = 0; //Костыль для уникальных имён коммитов
        public static void Initialize()
        {
            
            CS = new(MW);
            LoadSettings();
            try
            {
                if (Directory.Exists(networkTempPath))
                {
                    Directory.Delete(networkTempPath, true);
                }
            } catch { }

            Directory.CreateDirectory(networkTempPath);

            
        }
        public static void LoadSettings()
        {
            if (!File.Exists(config_path))
            {
                SaveSettings(true); // Создаем файл с настройками по умолчанию
                return;
            }

            try
            {
                string json = File.ReadAllText(config_path);
                var settings_l = JsonSerializer.Deserialize<SettingsData>(json);

                settings = settings_l;

                // проверка папки загрузок
                if (string.IsNullOrWhiteSpace(settings.SavePath) || !Directory.Exists(settings.SavePath))
                {
                    settings.SavePath = AppDomain.CurrentDomain.BaseDirectory;
                    Directory.CreateDirectory(settings.SavePath);
                }

                if (settings_l != null)
                {
                    Repositories = new();
                    foreach (var repository in settings_l.Repositories) {
                        Repository rep1 = new();
                        bool Opened = rep1.FO.OpenRepository(repository.WorkingDirectory);

                        bool idExists = MainProgramLogic.Repositories.Any(r => r.ID == rep1.ID);
                        if (idExists)
                        {
                            rep1.ID = Guid.NewGuid().ToString();
                            try
                            {
                                rep1.FO.SaveHistory();
                            }
                            catch { }
                        }
                        if (Opened) Repositories.Add(rep1);
                    }
                    settings = settings_l;
                    MW.Title = "FileController*" + settings.UserName;
                    SW.Title = "Settings*" + settings.UserName;
                    CS.Title = "ConnectionSelect*" + settings.UserName;
                    SW = new();
                    SaveSettings();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}");
            }
        }

        public static void SaveSettings(bool FirstTime = false)
        {
            try
            {
                settings.Repositories.Clear();
                foreach (var repository in Repositories) {
                    json_Repository rep1 = new();
                    rep1.Name = repository.Name;
                    rep1.ID = repository.ID;
                    rep1.WorkingDirectory = repository.WorkingDirectory;
                    settings.Repositories.Add(rep1);
                }
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true  // Красивое форматирование
                });
                MW.Title = "FileController*" + settings.UserName;
                SW.Title = "Settings*" + settings.UserName;
                CS.Title = "ConnectionSelect*" + settings.UserName;
                File.WriteAllText(config_path, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}");
            }
        }

        public static void OnClose()
        {
            SaveSettings();
            NetworkOperations.closeMainConnection();
            
            try
            {
                if (Directory.Exists(networkTempPath))
                {
                    Directory.Delete(networkTempPath, true);
                }
            }
            catch { }
            Cleaner.clean(Repositories);


        }

    }

    public class SettingsData
    {
        public string ID { get; set; } = Guid.NewGuid().ToString(); //ID пользователя
        public string NetworkUserName { get; set; } = Environment.MachineName;
        public string UserName { get; set; } = Environment.MachineName;
        public string SavePath { get; set; } = Environment.CurrentDirectory;

        public string LocalIP { get; set; } = "127.0.0.3"; //только для отладки по loopback, порт назначается автоматически
        public string ServerIP { get; set; } = "127.0.0.2";
        public int ServerPort { get; set; } = 5002;

        public int LocalPort { get; set; } = 5004; //порт для p2p
        public bool connect_to_server_at_start { get; set; } = false;
        public bool avaibleNetworkOperations { get; set; } = true;
        public ObservableCollection<User> Users { get; set; } = new ();

        public List<json_Repository> Repositories { get; set; } = new();
        public string StartCommitID { get; set; } = "0"; //стандартный ID Для инициализирующего коммита

        
    }



}
