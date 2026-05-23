using System.Text.Json;
using System.IO;


namespace FileController_v2.VC
{
    //пара методов для JSON
    public static class _jsonTools
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
