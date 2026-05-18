using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace FileController_v2_ServerRetranslator
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            await NetworkOperations.StartListening();
        }

        public static class NetworkOperations
        {
            //public static string MyIP = "185.18.55.107";
            public static string MyIP = "127.0.0.2";
            public static int MyPort = 5002;
            public static ConcurrentDictionary<Guid, Connection> connections = new();

            public static async Task StartListening()
            {
                Console.WriteLine("Запущено " + MyIP +":"+MyPort.ToString());
                IPEndPoint myPoint = new IPEndPoint(IPAddress.Parse(MyIP), MyPort);
                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Bind(myPoint);
                socket.Listen();
                while (true) {
                    Socket client = socket.Accept();
                    _ = Task.Run(() => ServiceClient(client));
                    Console.WriteLine("Подключен клиент");
                }
            }

            private static async Task ServiceClient(Socket client)
            {
                while (true)
                {
                    try
                    {
                        //сначала длина заголовка
                        byte[] headerLenBuffer = new byte[4];
                        await ReadExact(client, headerLenBuffer, 4);
                        int headerLength = BitConverter.ToInt32(headerLenBuffer);
                        //потом он сам
                        byte[] headerBuffer = new byte[headerLength];
                        await ReadExact(client, headerBuffer, headerLength);
                        string headerText = Encoding.UTF8.GetString(headerBuffer);
                        Console.WriteLine(headerText);
                        //парсим его для удобства
                        PacketHeader packet = ParseConnectionHeader(headerText);
                        // Записываем его в словарь
                        Connection sourceConnection = connections.GetOrAdd(packet.SourceID, id =>
                        {
                            return new Connection()
                            {
                                user = client,
                                ID = packet.SourceID,
                                name = packet.SourceName,
                                IP = ((IPEndPoint)client.RemoteEndPoint!).Address.ToString(),
                                Port = ((IPEndPoint)client.RemoteEndPoint!).Port
                            };
                        });
                        //запрос серверу на список пользователей
                        if (packet.DestinationID == Guid.Empty)
                        {
                            var nodes = connections.Values.Where(x => x.ID != packet.SourceID).Select(x => new
                            { id = x.ID, name = x.name }).ToList();
                            string json = JsonSerializer.Serialize(nodes);
                            byte[] payloadBytes = Encoding.UTF8.GetBytes(json);
                            string responseHeader =
                                $"surs={Guid.Empty}\r\n" +
                                $"dest={packet.SourceID}\r\n" +
                                $"comm=NodeList\r\n" +
                                $"payload_length={payloadBytes.Length}\r\n\r\n";
                            byte[] responseHeaderBytes = Encoding.UTF8.GetBytes(responseHeader);
                            byte[] responseHeaderLength = BitConverter.GetBytes(responseHeaderBytes.Length);
                            // отправляем длину заголовка
                            await SendExact( sourceConnection,responseHeaderLength, responseHeaderLength.Length);
                            // отправляем header
                            await SendExact(sourceConnection,responseHeaderBytes,responseHeaderBytes.Length);
                            // отправляем payload
                            await SendExact(sourceConnection, payloadBytes,payloadBytes.Length);
                            if (packet.PayloadLength != 0) await SkipPayload(client, packet.PayloadLength);
                            Console.WriteLine("Список пользователей отправлен");
                            continue;
                        }
                        //ищем кому надо пересылать
                        if (!connections.TryGetValue(packet.DestinationID, out Connection? destConnection))
                        {
                            Console.WriteLine("Destination not found");
                            string errorHeader =
                            $"surs={packet.DestinationID}\r\n" +
                            $"dest={packet.SourceID}\r\n" +
                            $"comm=ConnectionNotAvailable\r\n" +
                            $"filepath=\r\n" +
                            $"payload_length=0\r\n\r\n";
                            byte[] errorHeaderBytes = Encoding.UTF8.GetBytes(errorHeader);
                            byte[] errorHeaderLength = BitConverter.GetBytes(errorHeaderBytes.Length);
                            // отправляем длину заголовка
                            await SendExact(sourceConnection, errorHeaderLength, errorHeaderLength.Length);
                            // отправляем header
                            await SendExact(sourceConnection, errorHeaderBytes, errorHeaderBytes.Length);
                            await SkipPayload(client, packet.PayloadLength);
                            continue;
                        }
                        Socket dest = destConnection.user;
                        //пересылаем первые 4 бита и заголовок
                        await SendExact(destConnection, headerLenBuffer, headerLenBuffer.Length);
                        //await dest.SendAsync(headerLenBuffer);
                        await SendExact(destConnection, headerBuffer, headerBuffer.Length);
                        //await dest.SendAsync(headerBuffer);
                        // Отправка дальше
                        byte[] relayBuffer = new byte[8192];
                        long remaining = packet.PayloadLength;
                        //чтобы не грузить оперативную память (у меня сервер на 1ГБ)
                        while (remaining > 0)
                        {
                            int need = (int)Math.Min(relayBuffer.Length, remaining);
                            int received = await client.ReceiveAsync(relayBuffer.AsMemory(0, need), SocketFlags.None);
                            if (received == 0) throw new Exception("Disconnected");
                            await SendExact(destConnection, relayBuffer, received);
                            remaining -= received;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка обработки: {ex.Message}");
                        RemoveConnection(client);
                        return;

                    }

                }
            }


            private static async Task SkipPayload( Socket socket, long size)
            {
                byte[] buffer = new byte[8192];

                long remaining = size;

                while (remaining > 0)
                {
                    int need = (int)Math.Min(buffer.Length, remaining);

                    int received = await socket.ReceiveAsync(
                        buffer.AsMemory(0, need),
                        SocketFlags.None);

                    if (received == 0)
                        throw new Exception("Disconnected");

                    remaining -= received;
                }
            }
            //читает поток приходящего tcp
            private static async Task ReadExact( Socket socket, byte[] buffer, int size)
            {
                int total = 0;
                while (total < size)
                {
                    int received = await socket.ReceiveAsync( buffer.AsMemory(total, size - total),  SocketFlags.None);
                    if (received == 0)
                    {
                        RemoveConnection(socket);
                        throw new Exception("Disconnected");
                    }
                    total += received;
                }
            }
            //вспомогательно, вместо Socket.SendAsync()
            private static async Task SendExact(Connection connection,byte[] buffer,int size)
            {
                bool entered =  await connection.SendSemaphore.WaitAsync(TimeSpan.FromSeconds(10));
                if (!entered) throw new Exception("Send timeout");
                try
                {
                    int total = 0;
                    while (total < size)
                    {
                        int sent = await connection.user.SendAsync( buffer.AsMemory(total, size - total), SocketFlags.None);
                        if (sent == 0)
                        {
                            RemoveConnection(connection.user);
                            throw new Exception("Disconnected");
                        }
                        total += sent;
                    }
                }
                finally
                {
                    connection.SendSemaphore.Release();
                }
            }
            private static PacketHeader ParseConnectionHeader(string header)
            {
                PacketHeader packet = new PacketHeader();

                string[] lines = header.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    string[] parts = line.Split('=', 2);

                    if (parts.Length != 2)
                        continue;

                    string key = parts[0];
                    string value = parts[1];

                    switch (key)
                    {
                        case "surs":
                            packet.SourceID = Guid.Parse(value);
                            break;

                        case "surs_name":
                            packet.SourceName = value;
                            break;

                        case "dest":
                            packet.DestinationID = Guid.Parse(value);
                            break;

                        case "comm":
                            packet.Command = value;
                            break;

                        case "filepath":
                            packet.FilePath = value;
                            break;

                        case "payload_length":
                            packet.PayloadLength = long.Parse(value);
                            break;
                        default:
                            break;
                    }
                }

                return packet;
            }

            private static void RemoveConnection(Socket socket)
            {
                var item = connections.FirstOrDefault(x => x.Value.user == socket);

                if (item.Value != null)
                {
                    connections.TryRemove(item.Key, out _);
                    Console.WriteLine($"Connection removed: {item.Key}");
                }

                try
                {
                    socket.Shutdown(SocketShutdown.Both);
                }
                catch { }

                socket.Close();
            }
        }

        public class Connection
        {
            public SemaphoreSlim SendSemaphore = new SemaphoreSlim(1, 1); //семафор, чтобы не мешать пакеты при ретрансляции
            public Socket user = null;
            public string name = "Unknown";
            public Guid ID = Guid.Empty;
            public string IP = "127.0.0.1";
            public int Port = 5005;
        }
        public class PacketHeader
        {
            public Guid SourceID;

            public string SourceName = "";

            public Guid DestinationID;

            public string Command = "";

            public string FilePath = "";

            public long PayloadLength;
        }

    }
   
}