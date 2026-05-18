using FileController_v2.VC;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using System.Xml.Linq;

//небольшое изменение
//ещё изменение

namespace FileController_v2.NO
{
    public class NodeListItem
    {
        public string id { get; set; } = Guid.Empty.ToString();
        public string name { get; set; } = "none";

        public bool canPush = false; //
        public Socket socket;
    }

    public static class NetworkOperations
    {
        public static volatile int AccessLevel = 0;

        public static bool p2pMode = false;
        public static Socket server_retranslator = null;
        public static NodeListItem selectedUser = new(); //используется в т.ч. для P2P
        public static string current_password = "";
        public static bool isOperatingByMe {  get; set; } //если я что-то редактирую
        public static bool isOperatingByRemote { get; set; } //если кто-то 

        public static Socket p2p_remote = null; //свой сокет для p2p
        public static List<NodeListItem> incoming_connections = new(); //для всех P2P входящих
        public static ObservableCollection<NodeListItem> server_users = new(); //к кому на ретрансляторе можно подключиться


        public async static Task TryConnectToServer()
        {
            if(server_retranslator != null)
            {
                if (server_retranslator.Connected == true) return;
            }
            try
            {
                server_retranslator = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPEndPoint localIpe = new IPEndPoint(IPAddress.Parse(MainProgramLogic.settings.LocalIP), 0);
                server_retranslator.Bind(localIpe);
                IPEndPoint serverIpe = new IPEndPoint(IPAddress.Parse(MainProgramLogic.settings.ServerIP), MainProgramLogic.settings.ServerPort);
                //10 секунд на попытку
                CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await server_retranslator.ConnectAsync(serverIpe, cts.Token);
                // запуск receive loop
                _ = Task.Run(() => ReceivingLoopServer(server_retranslator));
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Connection timeout");
                try
                {
                    closeMainConnection();
                }
                catch { }

                server_retranslator = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection failed: {ex.Message}");
                try
                {
                    closeMainConnection();
                }
                catch { }

                server_retranslator = null;
            }
        }

        //запуск прослушивания входящих подключений
        public static async Task StartReceivingLoopForP2P()
        {
            while (true)
            {
                if (!MainProgramLogic.settings.avaibleNetworkOperations) break;
                try
                {
                    IPEndPoint myPoint = new IPEndPoint(IPAddress.Parse(MainProgramLogic.settings.LocalIP), MainProgramLogic.settings.LocalPort);
                    Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    socket.Bind(myPoint);
                    socket.Listen();
                    while (true)
                    {
                        Socket client = await socket.AcceptAsync();
                        if (!MainProgramLogic.settings.avaibleNetworkOperations)
                        {
                            client.Close();
                            return;
                        }
                        if (isOperatingByMe)
                        {
                            client.Close();
                            return;
                        }
                        NodeListItem connection = new NodeListItem();
                        connection.socket = client;
                        incoming_connections.Add(connection);
                        _ = Task.Run(() => ReceivingLoopServer(client, true));
                        
                    }
                }
                catch { }
            }
        }
        private static async Task ReceivingLoopServer(Socket socket, bool isP2P = false)
        {
            try
            {
                if(!isP2P) await GetNodes(socket);
                Socket anotherClient = socket;
                
                while (true)
                {
                    if (!MainProgramLogic.settings.avaibleNetworkOperations) break;
                    byte[] headerLenBuffer = new byte[4];
                    
                    await ReadExact(anotherClient, headerLenBuffer, 4);
                    int headerLength = BitConverter.ToInt32(headerLenBuffer, 0);
                    byte[] headerBuffer = new byte[headerLength];

                    await ReadExact(anotherClient, headerBuffer, headerLength);

                    string header = Encoding.UTF8.GetString(headerBuffer);

                    Packet input_headers = new Packet();
                    input_headers = input_headers.ParseAll(header);
                    long payloadLength = ParsePayloadLength(header);

                    Remote_User ru = new Remote_User();
                    ru.Name = ParseHeaderValue(header, "username");
                    ru.Password = ParseHeaderValue(header, "password");
                    ru.passDate = ParseHeaderValue(header, "timecode");
                    bool push_access = false;
                    foreach (User u in MainProgramLogic.settings.Users)
                    {
                        if (u.Name == ru.Name)
                        {
                            if (u.canPush)
                            {
                                push_access = true;
                                break;
                            }
                        }
                    }


                    string comm = ParseHeaderValue(header, "comm");

                    if (comm == "AccessDenied")
                    {
                        await SkipPayload(anotherClient, payloadLength);
                        _ = Transmission.failureProtocol(input_headers.surs);
                        MessageBox.Show("Узел: " + input_headers.surs + "  Доступ запрещён.");
                        
                    }
                    else if (comm == "ConnectionNotAvailable")
                    {
                        MessageBox.Show("Узел " + input_headers.surs + " недоступен");
                    }
                    else if (comm == "Ping")
                    {
                        bool result = Remote_User.CheckUser(ru);
                        if (!result) await AccessDenied(anotherClient, input_headers);
                        if (push_access) _ = Ping(anotherClient, input_headers, 2);
                        else _ = (anotherClient, input_headers, 1);

                    }
                    else if (comm == "Ping1") AccessLevel = 1;
                    else if (comm == "Ping2") AccessLevel = 2;
                    else if(input_headers.comm == "GetNodes")
                    {
                        //IamNotServer
                        Packet p = new Packet();
                        p.comm = "IamNotServer";
                        p.dest = input_headers.surs;
                        await IamNotServer(socket, p);
                        try
                        {
                            if (incoming_connections.Find(n => n.socket == anotherClient) == null)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    TransactionAccept ta = new("Обнаружено подключение P2P. Разрешить доступ?", "Да (3)", "Нет", 3);
                                    ta.ShowDialog();
                                    if (ta.DialogResult == false) throw new Exception("Отключен пользователем");
                                });
                            }
                        }
                        catch { }
                        
                    }
                    else if (comm == "IamNotServer")
                    {
                        selectedUser.name = input_headers.surs_name;
                        selectedUser.id = input_headers.surs;
                        await SkipPayload(anotherClient, payloadLength);
                        server_users.Clear();
                        p2pMode = true;
                        MainProgramLogic.CS.forseConnection();


                    }
                    else if (comm == "HeadRepos")
                    {
                        if (!Remote_User.CheckUser(ru) && input_headers.surs != Guid.Empty.ToString())
                        {
                            await AccessDenied(anotherClient, input_headers);
                            await SkipPayload(anotherClient, payloadLength);
                            continue;
                        }
                        await HeadAnswRepos(anotherClient, input_headers);
                    }
                    else if (comm == "AskToReadyStartTransmission")
                    {

                        if(Transmission.isActive) await AccessDenied(anotherClient, input_headers);
                        await AnswerReadyToStartTransmission(anotherClient, input_headers);
                    }
                    else if(comm == "AnswerReadyToStartTransmission")
                    {
                        await PushJsonHistory(anotherClient, selectedUser.id);
                    }
                    else if(comm == "AskForSuccess")
                    {
                        await Success(anotherClient, input_headers);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                Transmission.tp.MarkAsSucess();
                                Transmission.tp.ProgressBar.Value = 100;
                            }
                            catch { }
                        });
                    }
                    else if(comm == "Success")
                    {
                        Transmission.success = true;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                Transmission.tp.MarkAsSucess();
                                Transmission.tp.ProgressBar.Value = 100;
                            }
                            catch { }
                        });
                    }

                    if (payloadLength > 0)
                    {
                        if (comm == "NodeList")
                        {
                            byte[] payload = new byte[payloadLength];
                            await ReadExact(anotherClient, payload, (int)payloadLength);
                            string json = Encoding.UTF8.GetString(payload);
                            var nodes = JsonSerializer.Deserialize<List<NodeListItem>>(json);
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                MainProgramLogic.UiUsers.Clear();
                                foreach (var item in nodes) MainProgramLogic.UiUsers.Add(item);
                            });
                            if (nodes != null)
                            {
                                foreach (var node in nodes)
                                {
                                    server_users.Add(new NodeListItem
                                    {
                                        id = node.id,
                                        name = node.name
                                    });
                                }
                            }
                        }
                        else if (comm == "HeadAnswRepos")
                        {
                            byte[] payload = new byte[payloadLength];
                            await ReadExact(anotherClient, payload, (int)payloadLength);
                            string json = Encoding.UTF8.GetString(payload);
                            var nodes = JsonSerializer.Deserialize<List<json_History>>(json);
                            if (nodes != null)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    TransactionAccept ta = new TransactionAccept($"Пришёл новый список репозиториев от" + input_headers.surs_name + ", обновить?", "Да ", "Нет", 3);
                                    ta.ShowDialog();
                                    if (ta.DialogResult == true)
                                    {
                                        RemoteRepositoryWindow.RemoteRepos.Clear();
                                        int counter = 0;
                                        foreach (var node in nodes)
                                        {
                                            if (node is json_History jh)
                                            {
                                                Repository repository = new Repository();
                                                repository.HEAD = jh.HEAD;
                                                repository.History.HEAD = jh.HEAD;
                                                repository.Name = jh.Name;
                                                repository.ID = jh.ID;
                                                repository.LastDate = jh.LastDate;
                                                foreach (var commit_j in jh.commits)
                                                {
                                                    repository.Commits.Add(json_commit_info.Transform(commit_j));
                                                }

                                                Application.Current.Dispatcher.Invoke(() =>
                                                {
                                                    RemoteRepositoryWindow.RemoteRepos.Add(new RepositoryItem(repository));
                                                    if (MainProgramLogic.CS.RRW != null) MainProgramLogic.CS.RRW.UpdateData();
                                                });
                                            }
                                        }
                                    }
                                });

                            }
                        }
                        else if (comm == "PushFileListToCheck")
                        {
                            List<RepoFile> files = new List<RepoFile>();
                            byte[] payload = new byte[payloadLength];
                            await ReadExact(anotherClient, payload, (int)payloadLength);
                            string json = Encoding.UTF8.GetString(payload);
                            var nodes = JsonSerializer.Deserialize<List<RepoFile>>(json);
                            if (nodes != null)
                            {
                                if (nodes is List<RepoFile> rfiles)
                                {
                                    foreach (RepoFile file in rfiles)
                                    {
                                        files.Add(file);
                                    }
                                }
                            }
                            await Transmission.StartSendingFiles(anotherClient, files);
                        }
                        //далее всё, что требует прав
                        else if (!Remote_User.CheckUser(ru) && input_headers.surs != Guid.Empty.ToString())
                        {
                            await AccessDenied(anotherClient, input_headers);
                            await SkipPayload(anotherClient, payloadLength);
                            continue;
                        }
                        //далее всё, что требует прав push
                        else if (!ru.canPush)
                        {
                            await SkipPayload(anotherClient, payloadLength);
                            await AccessDenied(anotherClient, input_headers);
                        }
                        else if (comm == "PushJsonHistory")
                        {
                            Repository repository = new Repository();
                            byte[] payload = new byte[payloadLength];
                            await ReadExact(anotherClient, payload, (int)payloadLength);
                            string json = Encoding.UTF8.GetString(payload);
                            var node = JsonSerializer.Deserialize<json_History>(json);
                            if (node != null)
                            {
                                if (node is json_History jh)
                                {

                                    repository.HEAD = jh.HEAD;
                                    repository.History.HEAD = jh.HEAD;
                                    repository.Name = jh.Name;
                                    repository.ID = jh.ID;
                                    repository.LastDate = jh.LastDate;
                                    foreach (var commit_j in jh.commits)
                                    {
                                        Commit nc = json_commit_info.Transform(commit_j);
                                        foreach (RepoFile file in commit_j.Files)
                                        {
                                            nc.Files.Add(file);
                                        }
                                        repository.Commits.Add(nc);
                                    }


                                }

                                Directory.CreateDirectory(Path.Combine(MainProgramLogic.networkTempPath, repository.Name));
                                Repository repository2 = new Repository();
                                bool exist = false;
                                bool created = false;
                                exist = repository2.FO.OpenRepository(Path.Combine(MainProgramLogic.networkTempPath, repository.Name), true);
                                if (!exist) created = repository2.FO.CreateRepository(Path.Combine(MainProgramLogic.networkTempPath, repository.Name), true);
                                repository2.History = repository.History;
                                if (exist) created = true;
                                repository2.HEAD = repository.HEAD;
                                repository2.Name = repository.Name;
                                repository2.ID = repository.ID;
                                repository2.CalculateSize();
                                repository2.LastDate = repository.LastDate;
                                repository2.Commits = repository.Commits;

                                repository2.FO.SaveHistory();

                                Transmission.incomming_rep = repository2;
                                if (!created)
                                {
                                    await AccessDenied(socket, input_headers);
                                }
                                else await PushFileListToCheck(socket, input_headers.surs);
                            }
                        }
                        else if (comm == "SendFile")
                        {
                            try
                            {
                                if (Transmission.incomming_rep == null)
                                {
                                    await SkipPayload(anotherClient, payloadLength);
                                    await AccessDenied(anotherClient, input_headers);
                                    continue;
                                }

                                string hash = input_headers.filepath;

                                if (string.IsNullOrWhiteSpace(hash))
                                {
                                    await SkipPayload(anotherClient, payloadLength);
                                    continue;
                                }

                                string savePath = Path.Combine(Transmission.incomming_rep.FilesDirectory, hash);
                                Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
                                byte[] buffer = new byte[65536];
                                using FileStream fs = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None);
                                long remaining = payloadLength;
                                while (remaining > 0)
                                {
                                    int need = (int)Math.Min(buffer.Length, remaining);
                                    int read = await anotherClient.ReceiveAsync(buffer.AsMemory(0, need), SocketFlags.None);
                                    if (read == 0) throw new Exception("Disconnected while receiving file");
                                    await fs.WriteAsync(buffer.AsMemory(0, read));
                                    remaining -= read;
                                }

                                // проверка что файл реально докачался
                                if (new FileInfo(savePath).Length != payloadLength)
                                {
                                    fs.Close();
                                    try
                                    {
                                        File.Delete(savePath);
                                    }
                                    catch { }

                                    throw new Exception("Incomplete file received");
                                }
                                Transmission.filesQueue.RemoveAll(f => f.Hash == hash);
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    Transmission.tp.complete += payloadLength;
                                });

                            }
                            catch
                            {
                                try
                                {
                                    string hash = input_headers.filepath;

                                    string savePath = Path.Combine(Transmission.incomming_rep.FilesDirectory, hash);

                                    if (File.Exists(savePath)) File.Delete(savePath);
                                }
                                catch { }

                                await Transmission.failureProtocol(input_headers.surs);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    TransactionAccept ta = new TransactionAccept($"Disconnected: {ex.Message}", "OK", "", 8);
                    ta.ShowDialog();
                });
                
            }
            try
            {
                if (isP2P)
                {
                    try
                    {
                        incoming_connections.Remove(incoming_connections.Find(n => n.socket == socket));
                        MainProgramLogic.CS.UpdateDataSoft();
                    }
                    catch { }
                }
                server_users.Clear();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MainProgramLogic.UiUsers.Clear();
                });

                p2pMode = false;
                socket?.Close();
            }
            catch { }
            if (server_retranslator == null) return;
            try
            {
                incoming_connections.Remove(incoming_connections.Find(n => n.socket == socket));
            }
            catch { }
            socket = null;
            _ = TryConnectToServer();

        }

        private static async Task ReadExact(Socket socket, byte[] buffer, int size, int timeoutMs = 20000)
        {
            int total = 0;

            using CancellationTokenSource cts = new(timeoutMs);

            while (total < size)
            {
                int read = await socket.ReceiveAsync( buffer.AsMemory(total, size - total), SocketFlags.None, cts.Token);
                if (read == 0) throw new Exception("Disconnected");
                total += read;
            }
        }
        private static long ParsePayloadLength(string header)
        {
            foreach (var line in header.Split("\r\n"))
            {
                if (line.StartsWith("payload_length="))
                    return long.Parse(line.Split('=')[1]);
            }
            return 0;
        }
        private static string ParseHeaderValue(string header, string key)
        {
            foreach (var line in header.Split("\r\n"))
            {
                if (line.StartsWith(key + "="))
                    return line.Substring(key.Length + 1);
            }

            return "";
        }

        private static async Task SkipPayload(Socket socket, long size)
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
        //короткие сообщения
        private static async Task SendExact(Socket socket, byte[] data)
        {
            if (socket == null) return;
            if (!socket.Connected) return;
            int total = 0;
            while (total < data.Length)
            {
                int sent = await socket.SendAsync( data.AsMemory(total), SocketFlags.None);
                if (sent == 0) throw new Exception("Disconnected");
                total += sent;
            }
        } 
        //для работы с памятью потоков
        static async Task SendExact(Socket socket, ReadOnlyMemory<byte> data)
        {
            if (socket == null) return;
            if (!socket.Connected) return;
            int total = 0;
            while (total < data.Length)
            {
                int sent = await socket.SendAsync( data.Slice(total), SocketFlags.None);
                if (sent == 0) throw new Exception("Disconnected");
                total += sent;
            }
        } 

        

        public static async Task AccessDenied(Socket socket, Packet input_headers)
        {
            string AnswerHeader =
            $"surs={MainProgramLogic.settings.ID}\r\n" +
            $"surs_name={MainProgramLogic.settings.NetworkUserName}\r\n" +
            $"dest={input_headers.surs}\r\n" +
            $"comm=AccessDenied\r\n" +
            $"username=\r\n" +
            $"password=\r\n" +
            $"timecode={input_headers.timecode}\r\n" +
            $"repository=\r\n" +
            $"filepath=\r\n" +
            $"payload_length=0\r\n\r\n";
            byte[] errorHeaderBytes = Encoding.UTF8.GetBytes(AnswerHeader);
            byte[] errorHeaderLength = BitConverter.GetBytes(errorHeaderBytes.Length);
            // отправляем длину заголовка
            await SendExact(socket, errorHeaderLength);
            // отправляем header
            await SendExact(socket, errorHeaderBytes);
        }
        //инициализирующий запрос на пользователей
        public static async Task GetNodes(Socket socket)
        {
            string AnswerHeader =
            $"surs={MainProgramLogic.settings.ID}\r\n" +
            $"surs_name={MainProgramLogic.settings.NetworkUserName}\r\n" +
            $"dest={Guid.Empty}\r\n" +
            $"comm=GetNodes\r\n" +
            $"username=\r\n" +
            $"password=\r\n" +
            $"timecode={DateTime.Now}\r\n" +
            $"repository=\r\n" +
            $"filepath=\r\n" +
            $"payload_length=0\r\n\r\n";
            byte[] errorHeaderBytes = Encoding.UTF8.GetBytes(AnswerHeader);
            byte[] errorHeaderLength = BitConverter.GetBytes(errorHeaderBytes.Length);
            // отправляем длину заголовка
            await SendExact(socket, errorHeaderLength);
            // отправляем header
            await SendExact(socket, errorHeaderBytes);
        }
        public static async Task IamNotServer(Socket socket, Packet packet)
        {
            string AnswerHeader =
            $"surs={MainProgramLogic.settings.ID}\r\n" +
            $"surs_name={MainProgramLogic.settings.NetworkUserName}\r\n" +
            $"dest={packet.surs}\r\n" +
            $"comm=IamNotServer\r\n" +
            $"username=\r\n" +
            $"password=\r\n" +
            $"timecode={DateTime.Now}\r\n" +
            $"repository=\r\n" +
            $"filepath=\r\n" +
            $"payload_length=0\r\n\r\n";
            byte[] errorHeaderBytes = Encoding.UTF8.GetBytes(AnswerHeader);
            byte[] errorHeaderLength = BitConverter.GetBytes(errorHeaderBytes.Length);
            // отправляем длину заголовка
            await SendExact(socket, errorHeaderLength);
            // отправляем header
            await SendExact(socket, errorHeaderBytes);
        }
        public static async Task Ping(Socket socket, Packet packet, int modification = 0)
        {
            string time = DateTime.Now.ToString("dd:hh:mm");
            string comm = "Ping";
            if (modification == 1) comm = "Ping1";
            if (modification == 2) comm = "Ping2";
            string dest = packet.dest;
            if (modification != 0) dest = packet.surs;
            string AnswerHeader =
            $"surs={MainProgramLogic.settings.ID}\r\n" +
            $"surs_name={MainProgramLogic.settings.NetworkUserName}\r\n" +
            $"dest={dest}\r\n" +
            $"comm={comm}\r\n" +
            $"username={MainProgramLogic.settings.NetworkUserName}\r\n" +
            $"password={Remote_User.HashPasswordWithCurrentTime(current_password, time)}\r\n" +
            $"timecode={time}\r\n" +
            $"repository=\r\n" +
            $"filepath=\r\n" +
            $"payload_length=0\r\n\r\n";
            byte[] HeaderBytes = Encoding.UTF8.GetBytes(AnswerHeader);
            byte[] HeaderLength = BitConverter.GetBytes(HeaderBytes.Length);
            // отправляем длину заголовка
            await SendExact(socket, HeaderLength);
            // отправляем header
            await SendExact(socket, HeaderBytes);
        }
        public static async Task HeadRepos(Socket socket, Packet packet)
        {
            string time = DateTime.Now.ToString("dd:hh:mm");
            string AnswerHeader =
            $"surs={MainProgramLogic.settings.ID}\r\n" +
            $"surs_name={MainProgramLogic.settings.NetworkUserName}\r\n" +
            $"dest={packet.dest}\r\n" +
            $"comm=HeadRepos\r\n" +
            $"username={MainProgramLogic.settings.NetworkUserName}\r\n" +
            $"password={Remote_User.HashPasswordWithCurrentTime(current_password, time)}\r\n" +
            $"timecode={time}\r\n" +
            $"repository=\r\n" +
            $"filepath=\r\n" +
            $"payload_length=0\r\n\r\n";
            byte[] HeaderBytes = Encoding.UTF8.GetBytes(AnswerHeader);
            byte[] HeaderLength = BitConverter.GetBytes(HeaderBytes.Length);
            // отправляем длину заголовка
            await SendExact(socket, HeaderLength);
            // отправляем header
            await SendExact(socket, HeaderBytes);
        }
        public static async Task HeadAnswRepos(Socket socket, Packet in_packet)
        {
            List<object> allHistory = new();
            foreach (Repository r in MainProgramLogic.Repositories)
            {
                try
                {
                    string path = Path.Combine(r.VcDirectory, "history.json");
                    if (!File.Exists(path))  continue;
                    string json = File.ReadAllText(path);
                    var data = JsonSerializer.Deserialize<object>(json);
                    if (data != null)  allHistory.Add(data);
                }
                catch { }
            }
            string payloadJson = JsonSerializer.Serialize(allHistory);
            byte[] payload = Encoding.UTF8.GetBytes(payloadJson);

            string AnswerHeader =
            $"surs={MainProgramLogic.settings.ID}\r\n" +
            $"surs_name={MainProgramLogic.settings.NetworkUserName}\r\n" +
            $"dest={in_packet.surs}\r\n" +
            $"comm=HeadAnswRepos\r\n" +
            $"username=\r\n" +
            $"password=\r\n" +
            $"timecode=\r\n" +
            $"repository=\r\n" +
            $"filepath=\r\n" +
            $"payload_length={payload.Length}\r\n\r\n";
            
            byte[] HeaderBytes = Encoding.UTF8.GetBytes(AnswerHeader);
            byte[] HeaderLength = BitConverter.GetBytes(HeaderBytes.Length);

            // отправляем длину заголовка
            await SendExact(socket, HeaderLength);
            // отправляем header
            await SendExact(socket, HeaderBytes);
            //отправить нагрузку
            await SendExact(socket, payload);

        }


        public static async Task AskToStartTransmission(Socket socket, Packet packet)
        {
            string time = DateTime.Now.ToString("dd:hh:mm");
            string AnswerHeader =
            $"surs={MainProgramLogic.settings.ID}\r\n" +
            $"surs_name={MainProgramLogic.settings.NetworkUserName}\r\n" +
            $"dest={packet.surs}\r\n" +
            $"comm=AskToStartTransmission\r\n" +
             $"username={MainProgramLogic.settings.NetworkUserName}\r\n" +
            $"password={Remote_User.HashPasswordWithCurrentTime(current_password, time)}\r\n" +
            $"timecode={time}\r\n" +
            $"repository=\r\n" +
            $"filepath=\r\n" +
            $"payload_length=0\r\n\r\n";

            byte[] HeaderBytes = Encoding.UTF8.GetBytes(AnswerHeader);
            byte[] HeaderLength = BitConverter.GetBytes(HeaderBytes.Length);

            // отправляем длину заголовка
            await SendExact(socket, HeaderLength);
            // отправляем header
            await SendExact(socket, HeaderBytes);


        }
        public static async Task AskToReadyStartTransmission(Socket socket, Packet packet)
        {
            string time = DateTime.Now.ToString("dd:hh:mm");
            string AnswerHeader =
            $"surs={MainProgramLogic.settings.ID}\r\n" +
            $"surs_name={MainProgramLogic.settings.NetworkUserName}\r\n" +
            $"dest={packet.surs}\r\n" +
            $"comm=AskToReadyStartTransmission\r\n" +
             $"username={MainProgramLogic.settings.NetworkUserName}\r\n" +
            $"password={Remote_User.HashPasswordWithCurrentTime(current_password, time)}\r\n" +
            $"timecode={time}\r\n" +
            $"repository=\r\n" +
            $"filepath=\r\n" +
            $"payload_length=0\r\n\r\n";

            byte[] HeaderBytes = Encoding.UTF8.GetBytes(AnswerHeader);
            byte[] HeaderLength = BitConverter.GetBytes(HeaderBytes.Length);

            // отправляем длину заголовка
            await SendExact(socket, HeaderLength);
            // отправляем header
            await SendExact(socket, HeaderBytes);

        }
        public static async Task AnswerReadyToStartTransmission(Socket socket, Packet in_packet)
        {

            string time = DateTime.Now.ToString("dd:hh:mm");
            string AnswerHeader =
            $"surs={MainProgramLogic.settings.ID}\r\n" +
            $"surs_name={MainProgramLogic.settings.NetworkUserName}\r\n" +
            $"dest={in_packet.surs}\r\n" +
            $"comm=AnswerReadyToStartTransmission\r\n" +
            $"username={MainProgramLogic.settings.NetworkUserName}\r\n" +
            $"password={Remote_User.HashPasswordWithCurrentTime(current_password, time)}\r\n" +
            $"timecode={time}\r\n" +
            $"repository=\r\n" +
            $"filepath=\r\n" +
            $"payload_length=0\r\n\r\n";

            byte[] HeaderBytes = Encoding.UTF8.GetBytes(AnswerHeader);
            byte[] HeaderLength = BitConverter.GetBytes(HeaderBytes.Length);

            // отправляем длину заголовка
            await SendExact(socket, HeaderLength);
            // отправляем header
            await SendExact(socket, HeaderBytes);

        }

        public static async Task PushJsonHistory(Socket socket, string destID)
        {
            string time = DateTime.Now.ToString("dd:hh:mm");
            Repository repository = Transmission.local_rep_toSend;
            if (repository == null)
            {
                await Transmission.failureProtocol();
                return;
            }
            string path = Path.Combine(repository.VcDirectory, "history.json");
            string json = "";
            if (!File.Exists(path)) json = JsonSerializer.Serialize(repository.History);
            else json = File.ReadAllText(path);

            string payloadJson = json;
            byte[] payload = Encoding.UTF8.GetBytes(payloadJson);

            string AnswerHeader =
            $"surs={MainProgramLogic.settings.ID}\r\n" +
            $"surs_name={MainProgramLogic.settings.NetworkUserName}\r\n" +
            $"dest={destID}\r\n" +
            $"comm=PushJsonHistory\r\n" +
            $"username={MainProgramLogic.settings.NetworkUserName}\r\n" +
            $"password={Remote_User.HashPasswordWithCurrentTime(current_password, time)}\r\n" +
            $"timecode={time}\r\n" +
            $"repository=\r\n" +
            $"filepath=\r\n" +
            $"payload_length={payload.Length}\r\n\r\n";

            byte[] HeaderBytes = Encoding.UTF8.GetBytes(AnswerHeader);
            byte[] HeaderLength = BitConverter.GetBytes(HeaderBytes.Length);

            // отправляем длину заголовка
            await SendExact(socket, HeaderLength);
            // отправляем header
            await SendExact(socket, HeaderBytes);
            //отправить нагрузку
            await SendExact(socket, payload);

        }

        public static async Task PushFileListToCheck(Socket socket, string destID)
        {
            string time = DateTime.Now.ToString("dd:hh:mm");
            Repository repository = Transmission.incomming_rep;
            repository.FO.UpdateWithoutSaveHistory();
            if (repository == null)
            {
                await Transmission.failureProtocol();
                return;
            }
            List<RepoFile> rf = new();
            foreach (Commit c in repository.Commits) {
                foreach (RepoFile file in c.Files) {
                    if(!rf.Contains(file)) rf.Add(file);
                }
            }
            for (int i = 0; i < rf.Count; i++)
            {
                try
                {
                    if (File.Exists(Path.Combine(repository.FilesDirectory, rf[i].Hash)))
                    {
                        rf.Remove(rf[i]);
                        i--;
                    }
                }
                catch { }
            }
            long sum = 0;
            foreach(RepoFile file in rf)
            {
                sum += file.Size;
            }
            Transmission.filesQueue = rf;
            Application.Current.Dispatcher.Invoke(() =>
            {
                
                try
                {
                    Transmission.tp.Show();
                    Transmission.tp.total = sum;
                }
                catch
                {
                    Transmission.tp = new();
                    Transmission.tp.Show();
                    Transmission.tp.total = sum;
                }

                
                
            });

            string payloadJson = JsonSerializer.Serialize(rf);
            byte[] payload = Encoding.UTF8.GetBytes(payloadJson);

            string AnswerHeader =
            $"surs={MainProgramLogic.settings.ID}\r\n" +
            $"surs_name={MainProgramLogic.settings.NetworkUserName}\r\n" +
            $"dest={destID}\r\n" +
            $"comm=PushFileListToCheck\r\n" +
            $"username={MainProgramLogic.settings.NetworkUserName}\r\n" +
            $"password={Remote_User.HashPasswordWithCurrentTime(current_password, time)}\r\n" +
            $"timecode={time}\r\n" +
            $"repository=\r\n" +
            $"filepath=\r\n" +
            $"payload_length={payload.Length}\r\n\r\n";

            byte[] HeaderBytes = Encoding.UTF8.GetBytes(AnswerHeader);
            byte[] HeaderLength = BitConverter.GetBytes(HeaderBytes.Length);

            // отправляем длину заголовка
            await SendExact(socket, HeaderLength);
            // отправляем header
            await SendExact(socket, HeaderBytes);
            //отправить нагрузку
            await SendExact(socket, payload);

        }



        public static async Task SendFilePacket(Socket socket, Packet packet, string filepath, string realFilePath)
        {
            FileInfo fi = new(realFilePath);

            long payloadLength = fi.Length;
            string time = DateTime.Now.ToString("dd:hh:mm");
            string header =
                $"surs={packet.surs}\r\n" +
                $"surs_name={packet.surs_name}\r\n" +
                $"dest={packet.dest}\r\n" +
                $"comm={packet.comm}\r\n" +
                $"username={MainProgramLogic.settings.NetworkUserName}\r\n" +
                $"password={Remote_User.HashPasswordWithCurrentTime(current_password, time)}\r\n" +
                $"timecode={time}\r\n" +
                $"repository={packet.repository}\r\n" +
                $"filepath={packet.filepath}\r\n" +
                $"payload_length={packet.payload_length}\r\n\r\n";
            byte[] headerBytes = Encoding.UTF8.GetBytes(header);
            byte[] headerLen = BitConverter.GetBytes(headerBytes.Length);
            await SendExact(socket, headerLen);
            await SendExact(socket, headerBytes);
            byte[] buffer = new byte[65536];
            using FileStream fs = File.OpenRead(realFilePath);
            while (true)
            {
                int read = await fs.ReadAsync(buffer);
                if (read == 0) break;
                await SendExact(socket, buffer.AsMemory(0, read));
            }
        }
        public static async Task AskForSuccess(Socket socket, Packet packet)
        {
            string AnswerHeader =
            $"surs={MainProgramLogic.settings.ID}\r\n" +
            $"surs_name={MainProgramLogic.settings.NetworkUserName}\r\n" +
            $"dest={packet.dest}\r\n" +
            $"comm=AskForSuccess\r\n" +
            $"username=\r\n" +
            $"password=\r\n" +
            $"timecode={DateTime.Now}\r\n" +
            $"repository=\r\n" +
            $"filepath=\r\n" +
            $"payload_length=0\r\n\r\n";
            byte[] errorHeaderBytes = Encoding.UTF8.GetBytes(AnswerHeader);
            byte[] errorHeaderLength = BitConverter.GetBytes(errorHeaderBytes.Length);
            // отправляем длину заголовка
            await SendExact(socket, errorHeaderLength);
            // отправляем header
            await SendExact(socket, errorHeaderBytes);
        }
        public static async Task Success(Socket socket, Packet in_packet)
        {
            string AnswerHeader =
            $"surs={MainProgramLogic.settings.ID}\r\n" +
            $"surs_name={MainProgramLogic.settings.NetworkUserName}\r\n" +
            $"dest={in_packet.surs}\r\n" +
            $"comm=Success\r\n" +
            $"username=\r\n" +
            $"password=\r\n" +
            $"timecode={DateTime.Now}\r\n" +
            $"repository=\r\n" +
            $"filepath=\r\n" +
            $"payload_length=0\r\n\r\n";
            byte[] errorHeaderBytes = Encoding.UTF8.GetBytes(AnswerHeader);
            byte[] errorHeaderLength = BitConverter.GetBytes(errorHeaderBytes.Length);
            // отправляем длину заголовка
            await SendExact(socket, errorHeaderLength);
            // отправляем header
            await SendExact(socket, errorHeaderBytes);
        }


        public static void closeMainConnection()
        {
            if (server_retranslator != null)
            {
                server_retranslator.Shutdown(SocketShutdown.Both);
                server_retranslator.Close();
            }
            server_retranslator = null;
            p2pMode = false;
            
            server_users.Clear();
        }

        public static int GetAccessLevel()
        {
            return AccessLevel;
        }

    }


    

    public class Packet
    {
        public string surs { get; set; } = MainProgramLogic.settings.ID;
        public string surs_name { get; set; } = MainProgramLogic.settings.NetworkUserName;
        public string dest { get; set; } = Guid.Empty.ToString();
        public string comm { get; set; } = "Ping";
        public string username { get; set; } = MainProgramLogic.settings.NetworkUserName;
        public string password { get; set; } = "";
        public string timecode { get; set; } = DateTime.Now.ToString();
        public string repository { get; set; } = Guid.Empty.ToString();
        public string filepath { get; set; } = "none.txt";
        public string payload_length { get; set; } = "0";

        public Packet ParseAll(string header)
        {
            Packet packet = new Packet();
            packet.surs = ParseHeaderValue(header, "surs");
            packet.surs_name = ParseHeaderValue(header, "surs_name");
            packet.dest = ParseHeaderValue(header, "dest");
            packet.comm = ParseHeaderValue(header, "comm");
            packet.username = ParseHeaderValue(header, "username");
            packet.password = ParseHeaderValue(header, "password");
            packet.timecode = ParseHeaderValue(header, "timecode");
            packet.repository = ParseHeaderValue(header, "repository");
            packet.filepath = ParseHeaderValue(header, "filepath");
            packet.payload_length = ParseHeaderValue(header, "payload_length");
            return packet;
        }
        private static string ParseHeaderValue(string header, string key)
        {
            foreach (var line in header.Split("\r\n"))
            {
                if (line.StartsWith(key + "="))
                    return line.Substring(key.Length + 1);
            }

            return "";
        }



    }

    public static class Transmission
    {
        public static TransmissionProgress tp;

        public static bool isActive = false;
        public static bool success = false;
        public static bool failure = false;

        public static bool initialByMe = true;

        public static string remoteID = Guid.Empty.ToString();

        public static List<RepoFile> filesQueue = new List<RepoFile>();

        public static Repository local_rep_toSend = null;
        public static Repository incomming_rep = null;

        public static async Task failureProtocol(string id = "0")
        {
            //if (!isActive) return;
            if(remoteID != id) return;
            local_rep_toSend = null;
            incomming_rep = null;
            isActive = false;
            success = false;
            failure = true;
            initialByMe = true;
            remoteID = Guid.Empty.ToString();
            filesQueue = new List<RepoFile>();
            local_rep_toSend = null;
            incomming_rep = null;
            
            Application.Current.Dispatcher.Invoke(() =>
            {
                if(MainProgramLogic.CS.RRW.tp != null && MainProgramLogic.CS.RRW.tp.IsActive){
                    MainProgramLogic.CS.RRW.tp.Close();
                }
                if (tp.IsActive) tp.Close();
                TransactionAccept ta = new TransactionAccept("Передача не удалась.", "OK", "", 2);
            });

        }
        public static async Task StartsendOut(Repository repo)
        {
            isActive = true;
            Packet p = new();
            remoteID = NetworkOperations.selectedUser.id;
            p.dest = remoteID;
            if (remoteID == Guid.Empty.ToString())
            {
                await failureProtocol();
                return;
            }
            local_rep_toSend = repo;
            await NetworkOperations.AskToReadyStartTransmission(NetworkOperations.server_retranslator, p);

            //отправить запрос на начало передачи +
            //получить подтверждение +
            //передать json +
            //передать файлы
            //команда сохраняй
            //получить подтверждение
        }
        public static async Task StartSendingFiles(Socket socket, List<RepoFile> files)
        {
            
            if(local_rep_toSend == null)
            {
                await failureProtocol();
                return;
            }
            Application.Current.Dispatcher.Invoke(() =>
            {
                local_rep_toSend.isBlocked = true;
                MainProgramLogic.MW.UpdateUI();
            });
            long totalSize = 0;
            foreach (RepoFile file in files)
            {
                totalSize += file.Size;
            }
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (MainProgramLogic.CS.RRW != null)
                {
                    if (MainProgramLogic.CS.RRW.tp != null && MainProgramLogic.CS.RRW.tp.IsActive)
                    {
                        MainProgramLogic.CS.RRW.tp.total = totalSize;
                    }
                }
            });
            
            foreach (RepoFile file in files)
            {
                
                string realFilePath = Path.Combine(local_rep_toSend.FilesDirectory, file.Hash);
                try
                {
                    if (!File.Exists(realFilePath)) continue;
                }
                catch { continue; }
                Packet p = new Packet
                {
                    dest = remoteID,
                    comm = "SendFile",
                    filepath = file.Hash,
                    payload_length = file.Size.ToString(),
                };
                await NetworkOperations.SendFilePacket(socket, p, "", realFilePath);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (MainProgramLogic.CS.RRW != null)
                    {
                        if (MainProgramLogic.CS.RRW.tp != null && MainProgramLogic.CS.RRW.tp.IsActive)
                        {
                            MainProgramLogic.CS.RRW.tp.complete += file.Size;
                        }
                    }
                });
                
            }
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (MainProgramLogic.CS.RRW != null)
                {
                    if (MainProgramLogic.CS.RRW.tp != null && MainProgramLogic.CS.RRW.tp.IsActive)
                    {
                        MainProgramLogic.CS.RRW.tp.MarkAsSucess();
                    }
                }
            });
            
            
            Application.Current.Dispatcher.Invoke(() =>
            {
                local_rep_toSend.isBlocked = false;
                MainProgramLogic.MW.UpdateUI();
            });

            _ = WaitForAnswer();
            Packet p1 = new Packet
            {
                dest = remoteID,
            };

            await NetworkOperations.AskForSuccess(socket, p1);
        }

        public static async Task WaitForAnswer()
        {
            await Task.Delay(2000);
            if (success)
            {
                MessageBox.Show("Передача успешно подтверждена!");
            }
            else if (failure)
            {
                MessageBox.Show("Передача не удалась.");
            }
            success = false;
        }
        



    }

}
