using FileController_v2.VC;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;


namespace FileController_v2.NO
{
    public class NodeListItem
    {
        public string id { get; set; } = Guid.Empty.ToString();
        public string name { get; set; } = "none";

        public bool canPush = false; //
        public Socket socket;
        public bool IgnoreMessages = false;

        public void ClearClose()
        {
            NetworkOperations.incoming_connections.Remove(this);
            socket.Close();
            if (Transmission.remoteID == id)
            {
                Transmission.failureProtocol();
            }
            if (NetworkOperations.selectedUser.id == id)
            {
                NetworkOperations.selectedUser = new NodeListItem();
            }
            if (MainProgramLogic.CS != null) MainProgramLogic.CS.UpdateData();
            {
                if (MainProgramLogic.CS.RRW != null) MainProgramLogic.CS.RRW.UpdateData();
            }
        }
    }

    public static class NetworkOperations
    {
        public static int GlobalTimeout = 120000; //время ожидиния пакетов от сокетовЮ после -- отключение
        public static volatile int AccessLevel = 0;
        public static bool p2pMode = false;

        public static Socket server_retranslator = null;
        public static NodeListItem selectedUser { get; set; } = new(); //используется в т.ч. для P2P
        public static string current_password = "";
        public static bool isOperatingByMe {  get; set; } //если я что-то редактирую
        public static bool isOperatingByRemote { get; set; } //если кто-то 

        public static bool incomingP2Pchabging = false;
        public static List<NodeListItem> incoming_connections { get; set; } = new(); //для всех P2P входящих
        public static ObservableCollection<NodeListItem> server_users = new(); //к кому на ретрансляторе можно подключиться
        public static List<string> TimeAccessToPush { get; set; } = new();

        public static bool isP2PListening = false;

        public async static Task TryConnectToServer()
        {
            if(server_retranslator != null)
            {
                if (server_retranslator.Connected == true) return;
            }
            try
            {
                server_retranslator = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    IPEndPoint localIpe = new IPEndPoint(IPAddress.Parse(MainProgramLogic.settings.LocalIP), 0);
                    server_retranslator.Bind(localIpe);
                    server_retranslator.ReceiveBufferSize = 262144;
                    server_retranslator.SendBufferSize = 262144;
                    server_retranslator.NoDelay = true;
                    IPEndPoint serverIpe = new IPEndPoint(IPAddress.Parse(MainProgramLogic.settings.ServerIP), MainProgramLogic.settings.ServerPort);
                    //10 секунд на попытку
                    CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await server_retranslator.ConnectAsync(serverIpe, cts.Token);
                    // запуск receive loop
                    _ = Task.Run(() => ReceivingLoop(server_retranslator));
                }
                catch (SocketException ex)
                {
                    server_retranslator.Close();
                    server_retranslator = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    server_retranslator.ReceiveBufferSize = 262144;
                    server_retranslator.SendBufferSize = 262144;
                    server_retranslator.NoDelay = true;
                    IPEndPoint serverIpe = new IPEndPoint(IPAddress.Parse(MainProgramLogic.settings.ServerIP), MainProgramLogic.settings.ServerPort);
                    //10 секунд на попытку
                    CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await server_retranslator.ConnectAsync(serverIpe, cts.Token);
                    // запуск receive loop
                    _ = Task.Run(() => ReceivingLoop(server_retranslator));
                }
                
            }
            catch (OperationCanceledException)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    TransactionAccept ta = new("Connection timeout", "ОК", "", 1);
                    ta.ShowDialog();
                });
                try
                {
                    closeMainConnection();
                }
                catch { }

                server_retranslator = null;
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    TransactionAccept ta = new($"Connection failed: {ex.Message}", "ОК", "", 1);
                    ta.ShowDialog();
                });
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
                if (!MainProgramLogic.settings.avaibleNetworkOperations)
                {
                    isP2PListening = false;
                    break;
                }
                try
                {
                    IPEndPoint myPoint = new IPEndPoint(IPAddress.Parse(MainProgramLogic.settings.LocalIP), MainProgramLogic.settings.LocalPort);
                    int realport = MainProgramLogic.settings.LocalPort;
                    Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    bool success = false;
                    int counter = 100;
                    while (!success)
                    {
                        try
                        {
                            
                            socket.Bind(myPoint);
                            success = true;

                        }
                        catch
                        {
                            realport++;
                            if(counter < 0)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    TransactionAccept ta = new("Порт для P2P приёма не получилось назначить", "ОК", "", 1);
                                    ta.ShowDialog();
                                });
                            }
                            if (counter / 20 == 0)
                            {
                                
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    TransactionAccept ta = new("Такой IP и порт уже заняты, временно переключаюсь на порт: " + realport, "ОК", "", 1);
                                    ta.ShowDialog();
                                });
                            }
                            myPoint = new IPEndPoint(IPAddress.Parse(MainProgramLogic.settings.LocalIP), realport);
                            success = false;
                            counter--;
                        }
                    }
                    socket.Listen();
                    while (true)
                    {
                        Socket client = await socket.AcceptAsync();
                        if (!MainProgramLogic.settings.avaibleNetworkOperations)
                        {
                            isP2PListening = false;
                            client.Close();
                            return;
                        }
                        if (isOperatingByMe)
                        {
                            client.Close();
                            continue;
                        }

                        client.ReceiveBufferSize = 262144;
                        client.SendBufferSize = 262144;
                        client.NoDelay = true;
                        _ = Task.Run(() => ReceivingLoop(client, true, new NodeListItem()));
                    }
                }
                catch { 
                    
                }
            }
        }
        private static async Task ReceivingLoop(Socket socket, bool isP2P = false, NodeListItem P2Pconnection = null)
        {
            bool skip_messages = false;
            if (P2Pconnection != null && P2Pconnection.IgnoreMessages) skip_messages = true;
            try
            {
                if(!isP2P) await GetNodes(socket);
                
                while (true)
                {
                    if (!MainProgramLogic.settings.avaibleNetworkOperations) break;
                    byte[] headerLenBuffer = new byte[4];
                    
                    await ReadExact(socket, headerLenBuffer, 4);
                    int headerLength = BitConverter.ToInt32(headerLenBuffer, 0);
                    byte[] headerBuffer = new byte[headerLength];
                    await ReadExact(socket, headerBuffer, headerLength);
                    string header = Encoding.UTF8.GetString(headerBuffer);


                    Packet inp_h = new Packet();
                    inp_h = inp_h.ParseAll(header);
                    long payloadLength = ParsePayloadLength(header);
                    if (isP2P)
                    {
                        P2Pconnection.socket = socket;
                        P2Pconnection.name = inp_h.surs_name;
                        P2Pconnection.id = inp_h.surs;
                    }

                    Remote_User ru = new Remote_User();
                    ru.Name = inp_h.username;
                    ru.ID = inp_h.surs;
                    ru.Password = inp_h.password;
                    ru.passDate = inp_h.timecode;
                    bool push_access = false;
                    foreach (User u in MainProgramLogic.settings.Users)
                    {
                        if (u.Name == ru.Name)
                        {
                            if(Remote_User.HashPasswordWithCurrentTime(u.Password, ru.passDate) == ru.Password)
                            {
                                ru.AvailablePaths = u.AvailablePaths;
                                if (u.canPush)
                                {
                                    push_access = true;
                                    break;

                                }
                            }
                        }
                    }
                    foreach (string a_id in TimeAccessToPush)
                    {
                        if (ru.ID == a_id)
                        {
                            push_access = true;
                            break;
                        }
                    }


                    
                    //Операции, доступные всем
                    if(inp_h.comm == "Disconnect")
                    {
                        if(!isP2P && p2pMode)
                        {
                            closeMainConnection();
                            return;
                        }
                        if (isP2P)
                        {
                            try
                            {
                                if (P2Pconnection != null) P2Pconnection.ClearClose();
                            }
                            catch { }
                            socket.Close();
                            return;
                        }
                    }
                    if(inp_h.comm == "KeepAlive_Cloning")
                    {
                        Transmission.LastHandshake = DateTime.Now;
                        KeepAlive_CloningAnswer(socket, inp_h);
                    }
                    else if(inp_h.comm == "KeepAlive_CloningAnswer")
                    {
                        Transmission.LastHandshake = DateTime.Now;
                    }
                    else if (inp_h.comm == "AccessDenied")
                    {
                        await SkipPayload(socket, payloadLength);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            _ = Transmission.failureProtocol(inp_h.surs);
                            TransactionAccept ta = new("Узел " + inp_h.surs_name + ": Доступ запрещён.", "ОК", "", 4);
                            ta.ShowDialog();
                        });
                    }
                    else if (inp_h.comm == "ClientIsBusy")
                    {
                        await SkipPayload(socket, payloadLength);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            _ = Transmission.failureProtocol(inp_h.surs);
                            TransactionAccept ta = new("Клиент " + inp_h.surs_name + " сейчас занят, повторите попытку позже.", "ОК", "", 4);
                            ta.ShowDialog();
                        });

                    }
                    else if (inp_h.comm == "ConnectionNotAvailable")
                    {
                        await SkipPayload(socket, payloadLength);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            _ = Transmission.failureProtocol(inp_h.surs);
                            TransactionAccept ta = new("Узел " + inp_h.surs_name + " недоступен.", "ОК", "", 4);
                            ta.ShowDialog();
                        });
                    }
                    //команды проверки уровня доступа
                    else if (inp_h.comm == "Ping")
                    {
                        bool result = Remote_User.CheckUser(ru);
                        if (!result)
                        {
                            await AccessDenied(socket, inp_h);
                            continue;
                        }
                        try
                        {
                            if (P2Pconnection != null) {
                                NodeListItem nodeListItem = incoming_connections.Find(n => n.id == P2Pconnection.id);
                                if (nodeListItem != null)
                                {
                                    nodeListItem.socket = socket;
                                }
                                else
                                {
                                    bool need_to_desconnect = false;
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        TransactionAccept ta = new("Обнаружено подключение P2P. Разрешить доступ?", "Да ", "Нет", 3);
                                        ta.ShowDialog();
                                        if (ta.DialogResult == false)
                                        {
                                            need_to_desconnect = true;
                                            return;
                                        }
                                        P2Pconnection = new NodeListItem();
                                        P2Pconnection.socket = socket;
                                        incoming_connections.Add(P2Pconnection);
                                        MainProgramLogic.CS.UpdateDataSoft();
                                    });
                                    if (need_to_desconnect) throw new Exception("Отключен пользователем");
                                }
                            }
                        }
                        catch { }
                        if (push_access) await Ping(socket, inp_h, 2);
                        else await Ping(socket, inp_h, 1);

                    }
                    else if (inp_h.comm == "Ping1") AccessLevel = 1;
                    else if (inp_h.comm == "Ping2") AccessLevel = 2;
                    //запрос на список пользователей (серверу, одновременно сигнал о подключении, если p2p)
                    else if (inp_h.comm == "GetNodes")
                    {
                        //IamNotServer
                        Packet p = new Packet();
                        p.comm = "IamNotServer";
                        p.dest = inp_h.surs;
                        await IamNotServer(socket, p);

                    }
                    //ответ от узла для p2p
                    else if (inp_h.comm == "IamNotServer")
                    {
                        selectedUser.name = inp_h.surs_name;
                        selectedUser.id = inp_h.surs;
                        await SkipPayload(socket, payloadLength);
                        server_users.Clear();
                        p2pMode = true;
                        MainProgramLogic.CS.forseConnection();
                    }
                    //операции с ограниченным доступом
                    else if (inp_h.comm == "HeadRepos")
                    {
                        if (!Remote_User.CheckUser(ru) && inp_h.surs != Guid.Empty.ToString())
                        {

                            await AccessDenied(socket, inp_h);
                            await SkipPayload(socket, payloadLength);
                            continue;
                        }
                        List<Repository> reps = new();
                        foreach (Repository repos in MainProgramLogic.Repositories)
                        {
                            foreach (string path in ru.AvailablePaths)
                            {
                                if (repos.WorkingDirectory.StartsWith(path))
                                {
                                    if (!reps.Contains(repos)) reps.Add(repos);
                                }
                            }
                        }
                        await HeadAnswRepos(socket, inp_h, reps);
                    }
                    //запрос на отправку
                    else if (inp_h.comm == "AskToInitialTransmission")
                    {
                        Repository repository = null;
                        if (inp_h.surs == Transmission.remoteID) Transmission.LastHandshake = DateTime.Now;

                        if (inp_h.surs != Guid.Empty.ToString())
                        {
                            foreach (Repository repos in MainProgramLogic.Repositories)
                            {
                                if (repos.ID == inp_h.repository)
                                {
                                    if (repos.Name == inp_h.filepath) {
                                        foreach (string path in ru.AvailablePaths)
                                        {
                                            if (repos.WorkingDirectory.StartsWith(path))
                                            {
                                                repository = repos;
                                            }
                                        }
                                    }

                                }
                            }
                            if (repository == null)
                            {
                                await SkipPayload(socket, payloadLength);
                                await AccessDenied(socket, inp_h);
                                continue;
                            }
                            await Application.Current.Dispatcher.Invoke(async () =>
                            {
                                TransactionAccept ta = new("У вас собираются загрузить репозиторий", "Продолжить (3)", "Нет", 3);
                                ta.ShowDialog();
                                if (ta.DialogResult == false)
                                {
                                    await ClientIsBusy(socket, inp_h);
                                    throw new Exception("передача от устройства отменена пользователем");
                                }
                            });
                            if (Transmission.isActive) await ClientIsBusy(socket, inp_h);
                            else {
                                NodeListItem n = new();
                                n.id = inp_h.surs;
                                if (isP2P) selectedUser = P2Pconnection;
                                else selectedUser = n;
                                Transmission.remoteID = inp_h.surs;
                                Transmission.local_rep_toSend = repository;
                                await Transmission.StartsendOut(Transmission.local_rep_toSend, socket, inp_h.merge);
                            };
                        }
                    }
                    //запрос на приём
                    else if (inp_h.comm == "AskToReadyStartTransmission")
                    {
                        Transmission.remoteID = inp_h.surs;
                        Transmission.LastHandshake = DateTime.Now;
                        if (!Remote_User.CheckUser(ru) && inp_h.surs != Guid.Empty.ToString() && !Transmission.isIncomming)
                        {
                            await AccessDenied(socket, inp_h);
                            await SkipPayload(socket, payloadLength);
                            continue;
                        }
                        Repository mergerep = null; ////здесь

                        foreach (Repository repos in MainProgramLogic.Repositories)
                        {
                            if (repos.ID == inp_h.merge)
                            {
                                foreach (string path in ru.AvailablePaths)
                                {
                                    if (repos.WorkingDirectory.StartsWith(path))
                                    {
                                        mergerep = repos;
                                        Transmission.local_merge = mergerep;
                                    }
                                }

                            }
                        }
                        if (inp_h.merge != "" && mergerep == null) await AccessDenied(socket, inp_h); ///////////////////////////////////////////////////////////////////НЕ ЗАБЫТЬ ПОМЕНЯТЬ
                        else if (!push_access) await AccessDenied(socket, inp_h);
                        else if (Transmission.isActive && Transmission.remoteID != Guid.Empty.ToString() && !Transmission.isIncomming) await ClientIsBusy(socket, inp_h);
                        else {
                            await Application.Current.Dispatcher.Invoke(async () =>
                            {
                                TransactionAccept ta = new("Вам собираются загрузить файлы, это может заблокировать некоторые репозитории на время", "Продолжить (3)", "Нет", 3);
                                ta.ShowDialog();
                                if (ta.DialogResult == false)
                                {
                                    await ClientIsBusy(socket, inp_h);
                                    throw new Exception("передача на ваше устройство отменена");
                                }
                            });
                            await AnswerReadyToStartTransmission(socket, inp_h);
                        }

                    }
                    //ответ на запрос на приём
                    else if (inp_h.comm == "AnswerReadyToStartTransmission")
                    {
                        if (inp_h.surs == Transmission.remoteID) Transmission.LastHandshake = DateTime.Now;
                        if (Transmission.remoteID != Guid.Empty.ToString()) await PushJsonHistory(socket, selectedUser.id);
                        else await Transmission.failureProtocol(selectedUser.id);
                    }
                    //вопрос об успешеости
                    else if (inp_h.comm == "AskForSuccess")
                    {
                        if (inp_h.surs == Transmission.remoteID) Transmission.LastHandshake = DateTime.Now;
                        if (Transmission.filesQueue.Count < 1)
                        {
                            try
                            {
                                string basePath = Path.Combine(MainProgramLogic.settings.SavePath, Transmission.incomming_rep.Name);
                                string finalPath = basePath;
                                int counter = 1;
                                while (Directory.Exists(finalPath))
                                {
                                    finalPath = basePath + "_" + counter;
                                    counter++;
                                }
                                if (Transmission.local_merge != null)
                                {
                                    await Application.Current.Dispatcher.Invoke(async () =>
                                    {

                                        try
                                        {
                                            await Transmission.incomming_rep.FO.MergeTo(Transmission.local_merge, false);
                                            Transmission.local_merge.CalculateSize();
                                            MainProgramLogic.SaveSettings();
                                        }
                                        catch {
                                            await UnSuccessMerge(socket, inp_h);
                                        }

                                    });
                                }
                                else {
                                    // Запускаем фоновую задачу с CancellationToken для keep-alive сигналов
                                    CancellationTokenSource keepAliveCts = new CancellationTokenSource();
                                    
                                    // Задача для отправки keep-alive каждые 2 секунды
                                    Task keepAliveTask = Task.Run(async () =>
                                    {
                                        while (!keepAliveCts.Token.IsCancellationRequested)
                                        {
                                            try
                                            {
                                                await KeepAlive_Cloning(socket, inp_h);
                                                await Task.Delay(2000, keepAliveCts.Token);
                                            }
                                            catch (OperationCanceledException)
                                            {
                                                break;
                                            }
                                            catch
                                            {
                                                // Ошибка отправки - продолжаем пытаться
                                            }
                                        }
                                    });
                                    
                                    // Запускаем копирование в отдельной задаче
                                    Task copyTask = Task.Run(() =>
                                    {
                                        try
                                        {
                                            Directory.CreateDirectory(finalPath);
                                            FileOperations.CopyDirectory(Transmission.incomming_rep.WorkingDirectory, finalPath);
                                        }
                                        catch (Exception ex)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"Ошибка при копировании: {ex.Message}");
                                            throw;
                                        }
                                    });

                                    // Дожидаемся завершения копирования
                                    await copyTask;
                                    
                                    // Отменяем keep-alive задачу
                                    keepAliveCts.Cancel();
                                    await keepAliveTask;
                                    keepAliveCts.Dispose();

                                    // После копирования выполняем оставшиеся операции в UI потоке
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        try
                                        {
                                            if (Transmission.incomming_rep.FO == null) throw new Exception();
                                            Transmission.incomming_rep.FO.OpenRepository(finalPath);
                                            Repository rep = new();
                                            rep.FO.OpenRepository(finalPath, true);
                                            bool idExists = MainProgramLogic.Repositories.Any(r => r.ID == rep.ID);
                                            if (idExists)
                                            {
                                                rep.ID = Guid.NewGuid().ToString();
                                            }
                                            try
                                            {
                                                rep.FO.SaveHistory();
                                            }
                                            catch { }
                                            rep.CalculateSize();
                                            MainProgramLogic.Repositories.Add(rep);
                                            MainProgramLogic.SaveSettings();
                                        }
                                        catch (Exception ex)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"Ошибка при инициализации репозитория: {ex.Message}");
                                        }
                                    });
                                }


                                await Success(socket, inp_h);
                                if (!Transmission.isIncomming)
                                {
                                    foreach (User u in MainProgramLogic.settings.Users)
                                    {
                                        if (u.Name == ru.Name)
                                        {
                                            if (Remote_User.HashPasswordWithCurrentTime(u.Password, ru.passDate) == ru.Password)
                                            {
                                                Application.Current.Dispatcher.Invoke(() =>
                                                {
                                                    u.AvailablePaths.Add(finalPath);
                                                    ru.AvailablePaths.Add(finalPath);
                                                    MainProgramLogic.SaveSettings();

                                                });
                                                break;

                                            }
                                        }
                                    }
                                }
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    MainProgramLogic.MW.UpdateUI();
                                    try
                                    {
                                        Transmission.tp.MarkAsSucess("Данные приняты.");
                                        Transmission.success = true;
                                    }
                                    catch { }
                                });

                            }
                            catch {
                                await UnSuccess(socket, inp_h);
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    try
                                    {
                                        Transmission.tp.MarkAsFailure("Ошибка получения.");
                                        Transmission.success = false;
                                    }
                                    catch { }
                                });
                            }
                        }
                        else
                        {
                            await UnSuccess(socket, inp_h);
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                try
                                {
                                    Transmission.tp.MarkAsFailure("Ошибка, " + Transmission.filesQueue.Count + " файлов не было получено");
                                }
                                catch { }
                            });
                        }


                    }
                    //ответ об успешности
                    else if (inp_h.comm == "Success")
                    {
                        if (inp_h.surs == Transmission.remoteID) Transmission.LastHandshake = DateTime.Now;
                        Transmission.success = true;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                Transmission.tp.MarkAsSucess("Успешно передано!");
                            }
                            catch { }
                        });
                        Transmission.isActive = false;
                    }
                    //ответ о неудаче
                    else if (inp_h.comm == "UnSuccess")
                    {
                        if (inp_h.surs == Transmission.remoteID) Transmission.LastHandshake = DateTime.Now;
                        Transmission.success = false;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                Transmission.tp.MarkAsFailure("Передача не удалась.");
                            }
                            catch { }
                        });
                        Transmission.isActive = false;
                    }
                    else if (inp_h.comm == " UnSuccessMerge")
                    {
                        if (inp_h.surs == Transmission.remoteID) Transmission.LastHandshake = DateTime.Now;
                        Transmission.success = false;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                Transmission.tp.MarkAsFailure("Слияние не удалось.");
                            }
                            catch { }
                        });
                    }
                    //ответ из списка пользователей сервера
                    if (inp_h.comm == "NodeList")
                    {
                        byte[] payload = new byte[payloadLength];
                        await ReadExact(socket, payload, (int)payloadLength);
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
                        MainProgramLogic.CS.UpdateDataSoft();
                    }
                    //запрос на список репозиториев
                    else if (inp_h.comm == "HeadAnswRepos")
                    {
                        byte[] payload = new byte[payloadLength];
                        await ReadExact(socket, payload, (int)payloadLength);
                        string json = Encoding.UTF8.GetString(payload);
                        var nodes = JsonSerializer.Deserialize<List<json_History>>(json);
                        if (nodes != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                //TransactionAccept ta = new TransactionAccept($"Список удалённых репозиториев обновлён" + inp_h.surs_name + ", обновить?", "ОК ", "", 1);
                                //ta.ShowDialog();
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
                                        RemoteRepositoryWindow.RemoteRepos.Add(new RepositoryItem(repository));
                                        
                                        if (MainProgramLogic.CS.RRW != null) MainProgramLogic.CS.RRW.UpdateData();
                                    }
                                }
                                if (MainProgramLogic.CS.RRW != null) MainProgramLogic.CS.RRW.UpdateData();
                                MainProgramLogic.CS.RRW.MainGrid.IsEnabled = true;
                            });

                        }
                    }
                    //список файлов для передачи
                    else if (inp_h.comm == "PushFileListToCheck")
                    {
                        if (inp_h.surs == Transmission.remoteID) Transmission.LastHandshake = DateTime.Now;
                        List<RepoFile> files = new List<RepoFile>();
                        byte[] payload = new byte[payloadLength];
                        await ReadExact(socket, payload, (int)payloadLength);
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
                        await Application.Current.Dispatcher.Invoke(async () =>
                        {
                            try
                            {
                                Transmission.tp.Reinit("Передача файлов: ");
                                Transmission.isActive = true;
                                _ = Transmission.OutcommingProtocolTimer();
                            }
                            catch { }

                        });
                        await Transmission.StartSendingFiles(socket, files);
                    }

                    //далее всё, что требует прав push
                    else if (!ru.canPush)
                    {
                        await SkipPayload(socket, payloadLength);
                        await AccessDenied(socket, inp_h);
                    }
                    //передача истории репозитория
                    else if (inp_h.comm == "PushJsonHistory")
                    {
                        if (!Remote_User.CheckUser(ru) && !Transmission.isIncomming)
                        {
                            await AccessDenied(socket, inp_h);
                            await SkipPayload(socket, payloadLength);
                            continue;
                        }
                        if (inp_h.surs == Transmission.remoteID) Transmission.LastHandshake = DateTime.Now;
                        Repository repository = new Repository();
                        byte[] payload = new byte[payloadLength];
                        await ReadExact(socket, payload, (int)payloadLength);
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
                                    repository.Commits.Add(nc);
                                }


                            }
                            string downloadPath = Path.Combine(MainProgramLogic.networkTempPath, repository.Name);
                            Directory.CreateDirectory(Path.Combine(MainProgramLogic.networkTempPath, repository.Name));
                            if (Transmission.local_merge != null)
                            {
                                try
                                {
                                    FileOperations.CopyDirectory(Transmission.local_merge.WorkingDirectory, downloadPath);
                                }
                                catch { }
                            }
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
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                repository2.FO.SaveHistory();
                            });
                            Transmission.incomming_rep = repository2;

                            if (!created)
                            {
                                await AccessDenied(socket, inp_h);
                            }
                            else
                            {

                                await PushFileListToCheck(socket, inp_h.surs);
                            }
                        }
                    }
                    //сам приём файлов во временное хранилище
                    else if (inp_h.comm == "SendFile")
                    {
                        if (!Remote_User.CheckUser(ru) && !Transmission.isIncomming)
                        {
                            await AccessDenied(socket, inp_h);
                            await SkipPayload(socket, payloadLength);
                            continue;
                        }
                        if (inp_h.surs == Transmission.remoteID) Transmission.LastHandshake = DateTime.Now;
                        string hash = inp_h.filepath;
                        try
                        {
                            if (Transmission.incomming_rep == null)
                            {
                                await SkipPayload(socket, payloadLength);
                                await AccessDenied(socket, inp_h);
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(hash))
                            {
                                await SkipPayload(socket, payloadLength);
                                continue;
                            }

                            string savePath = Path.Combine(Transmission.incomming_rep.FilesDirectory, hash);


                            try
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
                            }
                            catch (Exception dirEx)
                            {
                                await SkipPayload(socket, payloadLength);
                                await Transmission.failureProtocol(inp_h.surs);
                                continue;
                            }

                            byte[] buffer = new byte[65536];
                            long bytesReceived = 0;

                            try
                            {
                                using FileStream fs = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None);
                                long remaining = payloadLength;
                                while (remaining > 0)
                                {
                                    int need = (int)Math.Min(buffer.Length, remaining);
                                    int read = await socket.ReceiveAsync(buffer.AsMemory(0, need), SocketFlags.None);
                                    if (read == 0) throw new Exception("Disconnected while receiving file");
                                    await fs.WriteAsync(buffer.AsMemory(0, read));
                                    bytesReceived += read;
                                    remaining -= read;
                                    Transmission.complete += read;
                                    Transmission.LastHandshake = DateTime.Now;
                                }
                                await fs.FlushAsync();
                            }
                            catch (Exception streamEx)
                            {
                                try { File.Delete(savePath); } catch { }
                                await Transmission.failureProtocol(inp_h.surs);
                                continue;
                            }

                            // Проверка размера с логированием
                            FileInfo fi = new FileInfo(savePath);
                            long actualSize = fi.Length;



                            if (actualSize != payloadLength)
                            {
                                try
                                {
                                    File.Delete(savePath);
                                }
                                catch { }

                                await Transmission.failureProtocol(inp_h.surs);
                            }
                            else
                            {

                                Transmission.filesQueue.Remove(hash);
                                Transmission.complete += payloadLength;
                            }
                        }
                        catch (Exception ex)
                        {
                            try
                            {
                                string savePath = Path.Combine(Transmission.incomming_rep?.FilesDirectory ?? "", hash);
                                if (File.Exists(savePath)) File.Delete(savePath);
                            }
                            catch { }

                            await Transmission.failureProtocol(inp_h.surs);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                
                if(!skip_messages)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            TransactionAccept ta = new TransactionAccept($"Disconnected: {ex.Message}", "OK", "", 2);
                            ta.ShowDialog();
                        }
                        catch { }

                    });
                }
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
                NodeListItem nli = incoming_connections.Find(n => n.socket == socket);
                if (nli != null) nli.ClearClose();
            }
            catch { }
            MainProgramLogic.CS.UpdateDataSoft();
            if (!isP2P && !skip_messages) _ = TryConnectToServer();

        }

        private static async Task ReadExact(Socket socket, byte[] buffer, int size, int timeoutMs = 60000)
        {
            if (timeoutMs == 60000) timeoutMs = GlobalTimeout;
            int total = 0;

            using CancellationTokenSource cts = new(timeoutMs);

            while (total < size)
            {
                int read = await socket.ReceiveAsync( buffer.AsMemory(total, size - total), SocketFlags.None, cts.Token);
                if(cts.IsCancellationRequested) throw new TimeoutException("Timeout");
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
                try
                {
                    int sent = await socket.SendAsync(data.AsMemory(total), SocketFlags.None);
                    if (sent == 0) throw new Exception("Disconnected");
                    total += sent;
                }
                catch { }
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

        
        public static async Task Disconnect(Socket socket, Packet input_headers)
        {
            string AnswerHeader =
            $"surs={MainProgramLogic.settings.ID}\r\n" +
            $"surs_name={MainProgramLogic.settings.NetworkUserName}\r\n" +
            $"dest={input_headers.surs}\r\n" +
            $"comm=Disconnect\r\n" +
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
        public static async Task ClientIsBusy(Socket socket, Packet input_headers)
        {
            string AnswerHeader =
            $"surs={MainProgramLogic.settings.ID}\r\n" +
            $"surs_name={MainProgramLogic.settings.NetworkUserName}\r\n" +
            $"dest={input_headers.surs}\r\n" +
            $"comm=ClientIsBusy\r\n" +
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
        public static async Task Confermed(Socket socket, Packet input_headers)
        {
            string AnswerHeader =
            $"surs={MainProgramLogic.settings.ID}\r\n" +
            $"surs_name={MainProgramLogic.settings.NetworkUserName}\r\n" +
            $"dest={input_headers.surs}\r\n" +
            $"comm=Confermed\r\n" +
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
        } //не используется
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
        public static async Task HeadAnswRepos(Socket socket, Packet in_packet, List<Repository> reps)
        {
            List<object> allHistory = new();
            foreach (Repository r in reps)
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

        

        public static async Task AskToReadyStartTransmission(Socket socket, Packet packet, string merge = "")
        {
            string time = DateTime.Now.ToString("dd:hh:mm");
            string AnswerHeader =
            $"surs={MainProgramLogic.settings.ID}\r\n" +
            $"surs_name={MainProgramLogic.settings.NetworkUserName}\r\n" +
            $"dest={packet.dest}\r\n" +
            $"comm=AskToReadyStartTransmission\r\n" +
            $"username={MainProgramLogic.settings.NetworkUserName}\r\n" +
            $"password={Remote_User.HashPasswordWithCurrentTime(current_password, time)}\r\n" +
            $"timecode={time}\r\n" +
            $"repository=\r\n" +
            $"merge={merge}\r\n" +
            $"filepath=\r\n" +
            $"payload_length=0\r\n\r\n";

            byte[] HeaderBytes = Encoding.UTF8.GetBytes(AnswerHeader);
            byte[] HeaderLength = BitConverter.GetBytes(HeaderBytes.Length);
            socket.NoDelay = true;
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

        //запрос на загрузку файла
        public static async Task AskToInitialTransmission(Socket socket, Packet packet, string repo_name = "", string merge = "")
        {
            string time = DateTime.Now.ToString("dd:hh:mm");
            string AnswerHeader =
            $"surs={MainProgramLogic.settings.ID}\r\n" +
            $"surs_name={MainProgramLogic.settings.NetworkUserName}\r\n" +
            $"dest={packet.dest}\r\n" +
            $"comm=AskToInitialTransmission\r\n" +
            $"username={MainProgramLogic.settings.NetworkUserName}\r\n" +
            $"password={Remote_User.HashPasswordWithCurrentTime(current_password, time)}\r\n" +
            $"timecode={time}\r\n" +
            $"repository={packet.repository}\r\n" +
            $"merge={merge}\r\n" +
            $"filepath={repo_name}\r\n" +
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
                await Transmission.failureProtocol(destID);
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
            Transmission.total = 0;
            Transmission.complete = 0;
            string time = DateTime.Now.ToString("dd:hh:mm");
            Repository repository = Transmission.incomming_rep;
            repository.FO.UpdateWithoutSaveHistory();
            if (repository == null)
            {
                await Transmission.failureProtocol();
                return;
            }
            List<RepoFile> rf = new();
            HashSet<string> addedHashes = new();

            foreach (Commit c in repository.Commits)
            {
                foreach (RepoFile file in c.Files)
                {
                    if (addedHashes.Add(file.Hash))
                    {
                        rf.Add(file);
                    }
                }
            }

            for (int i = 0; i < rf.Count; i++)
            {
                try
                {
                    if (File.Exists(Path.Combine(repository.FilesDirectory, rf[i].Hash)))
                    {
                        addedHashes.Remove(rf[i].Hash);
                        rf.RemoveAt(i);
                        i--;
                    }
                }
                catch { }
            }

            long sum = 0;

            foreach (RepoFile file in rf)
            {
                sum += file.Size;
            }

            Transmission.filesQueue = addedHashes;
            Transmission.total = sum;
            await Application.Current.Dispatcher.Invoke(async() =>
            {
                try
                {
                    Transmission.tp.Reinit("Приём файлов: ");
                    Transmission.isActive = true;
                    _ = Transmission.OutcommingProtocolTimer();
                }
                catch {}
                
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
                Transmission.LastHandshake = DateTime.Now;
                Transmission.complete += read;
            }
        }
        public static async Task AskForSuccess(Socket socket, Packet packet)
        {
            string time = DateTime.Now.ToString("dd:hh:mm");
            string AnswerHeader =
            $"surs={MainProgramLogic.settings.ID}\r\n" +
            $"surs_name={MainProgramLogic.settings.NetworkUserName}\r\n" +
            $"dest={packet.dest}\r\n" +
            $"comm=AskForSuccess\r\n" +
            $"username={MainProgramLogic.settings.NetworkUserName}\r\n" +
            $"password={Remote_User.HashPasswordWithCurrentTime(current_password, time)}\r\n" +
            $"timecode={time}\r\n" +
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
        public static async Task UnSuccess(Socket socket, Packet in_packet)
        {
            // Логирование недостающих файлов
            if (Transmission.incomming_rep != null && Transmission.filesQueue.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"\n❌ НЕДОСТАЮЩИЕ ФАЙЛЫ ({Transmission.filesQueue.Count}):");
                
                foreach (string missingHash in Transmission.filesQueue)
                {
                    // Ищем файл по хешу во всех коммитах
                    foreach (Commit commit in Transmission.incomming_rep.Commits)
                    {
                        RepoFile foundFile = commit.Files.FirstOrDefault(f => f.Hash == missingHash);
                        if (foundFile != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"   📄 {foundFile.Path}");
                            System.Diagnostics.Debug.WriteLine($"      Hash: {missingHash}");
                            System.Diagnostics.Debug.WriteLine($"      Size: {foundFile.Size} bytes");
                            System.Diagnostics.Debug.WriteLine($"      Commit: {commit.Name} ({commit.ID})");
                            break;
                        }
                    }
                }
            }

            string AnswerHeader =
            $"surs={MainProgramLogic.settings.ID}\r\n" +
            $"surs_name={MainProgramLogic.settings.NetworkUserName}\r\n" +
            $"dest={in_packet.surs}\r\n" +
            $"comm=UnSuccess\r\n" +
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

        public static async Task UnSuccessMerge(Socket socket, Packet in_packet)
        {
            string AnswerHeader =
            $"surs={MainProgramLogic.settings.ID}\r\n" +
            $"surs_name={MainProgramLogic.settings.NetworkUserName}\r\n" +
            $"dest={in_packet.surs}\r\n" +
            $"comm=UnSuccess\r\n" +
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

        public static async Task KeepAlive_Cloning(Socket socket, Packet packet)
        {
            string AnswerHeader =
            $"surs={MainProgramLogic.settings.ID}\r\n" +
            $"surs_name={MainProgramLogic.settings.NetworkUserName}\r\n" +
            $"dest={packet.dest}\r\n" +
            $"comm=KeepAlive_Cloning\r\n" +
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
        public static async Task KeepAlive_CloningAnswer(Socket socket, Packet in_packet)
        {
            string AnswerHeader =
            $"surs={MainProgramLogic.settings.ID}\r\n" +
            $"surs_name={MainProgramLogic.settings.NetworkUserName}\r\n" +
            $"dest={in_packet.surs}\r\n" +
            $"comm=KeepAlive_CloningAnswer\r\n" +
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

        public static async Task closeMainConnection()
        {

            if (server_retranslator != null)
            {
                if(server_retranslator.Connected && selectedUser != null)
                {
                    Packet packet = new Packet();
                    packet.dest = selectedUser.id;
                    await Disconnect(server_retranslator, packet);
                }
                
                server_retranslator.Shutdown(SocketShutdown.Both);
                server_retranslator.Close();
            }
            server_retranslator = null;
            p2pMode = false;
            
            server_users.Clear();
        }
        public static async void closeAllp2pConnections()
        {
            foreach(NodeListItem n in incoming_connections)
            {
                try
                {
                    n.IgnoreMessages = true;
                    Packet packet = new Packet();
                    packet.dest = n.id;
                    await Disconnect(n.socket, packet);
                    n.socket.Close();
                }
                catch { }
                
            }
            incoming_connections.Clear();
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
        public string merge { get; set; } = ""; //ID репозитория, в который сливать
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
            packet.merge = ParseHeaderValue(header, "merge");
            packet.filepath = ParseHeaderValue(header, "filepath");
            packet.payload_length = ParseHeaderValue(header, "payload_length");
            return packet;
        }
        private static string ParseHeaderValue(string header, string key)
        {
            foreach (var line in header.Split("\r\n"))
            {
                if (line.StartsWith(key + "=")) return line.Substring(key.Length + 1);
            }

            return "";
        }



    }

    public static class Transmission
    {
        public static TransmissionProgress tp;
        public static long total { get; set; } = 0;
        public static long complete { get; set; } = 0;
        public static int timeotTime { get; set; } = 30; //в секундах

        public static bool isActive { get; set; } = false;
        public static bool isIncomming { get; set; } = false; //режим, когда сам запросил
        public static bool success { get; set; } = false;

        public static Repository local_merge { get; set; } = null;
        public static Repository rem_merge { get; set; } = null;


        //c кем работаем
        public static string remoteID { get; set; } = Guid.Empty.ToString();

        public static HashSet<string> filesQueue { get; set; } = new();

        //public static List<RepoFile> filesQueue { get; set; } = new List<RepoFile>();

        public static Repository local_rep_toSend { get; set; } = null;
        public static Repository incomming_rep { get; set; } = null;

        public static DateTime LastHandshake = DateTime.MinValue;


        public static async Task failureProtocol(string id = "0")
        {
            if (!isActive) return;
            if(remoteID != id) return;
            if(success) return;  // Предотвратить перезапись успеха в ошибку
            local_rep_toSend = null;
            incomming_rep = null;
            isActive = false;
            success = false;
            remoteID = Guid.Empty.ToString();
            filesQueue.Clear();
            local_rep_toSend = null;
            incomming_rep = null;
            rem_merge = null;
            local_merge = null;
            
            Application.Current.Dispatcher.Invoke(() =>
            {

                if (tp != null && tp.IsLoaded && tp.IsVisible)
                {
                    tp.Close();
                }
                TransactionAccept ta = new TransactionAccept("Передача не удалась.", "OK", "", 2);
                ta.ShowDialog();
            });

        }
        public static async Task StartsendOut(Repository repo, Socket socket, string merge = "")
        {
            if (isActive){
                Application.Current.Dispatcher.Invoke(() =>
                {
                    TransactionAccept ta = new TransactionAccept("Уже выполняется передача данных. Пожалуйста, дождитесь её завершения.", "OK", "", 2);
                    ta.ShowDialog();
                    return;
                });
            }
            Packet p = new();
            remoteID = NetworkOperations.selectedUser.id;
            if(isIncomming) p.surs = remoteID;
            else p.dest = remoteID;
            if (remoteID == Guid.Empty.ToString())
            {
                await failureProtocol(remoteID);
                return;
            }
            local_rep_toSend = repo;
            isActive = true;
            if (merge != "") await NetworkOperations.AskToReadyStartTransmission(socket, p, merge);
            else await NetworkOperations.AskToReadyStartTransmission(socket, p);
            LastHandshake = DateTime.Now;
            _ = OutcommingProtocolTimer();
            

            //отправить запрос на начало передачи +
            //получить подтверждение +
            //передать json +
            //передать файлы
            //команда сохраняй
            //получить подтверждение
        }
        public static async Task StartSendingFiles(Socket socket, List<RepoFile> files)
        {
            total = 0;
            complete = 0;
            if (local_rep_toSend == null)
            {
                await failureProtocol(remoteID);
                return;
            }
            Application.Current.Dispatcher.Invoke(() =>
            {
                local_rep_toSend.isBlocked = true;
                MainProgramLogic.MW.UpdateUI();

                if(tp != null && tp.IsLoaded && tp.IsVisible)
                {
                    tp.ProcessName.Text = "Передача файлов:";
                }
                
            });
            long totalSize = 0;
            foreach (RepoFile file in files)
            {
                totalSize += file.Size;
            }

            total = totalSize;
            bool confermed = false;
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
                //complete += file.Size;

            }
            Application.Current.Dispatcher.Invoke(() =>
            {
                local_rep_toSend.isBlocked = false;
                MainProgramLogic.MW.UpdateUI();
            });
            Packet p1 = new Packet
            {
                dest = remoteID,
            };
            await NetworkOperations.AskForSuccess(socket, p1);
        }
       


        public static async Task StartIncommingProtocol(Repository rep, Socket socket, Repository rem_merge_pack = null)
        {
            if (tp != null)
            {
                tp.Hide();
            }
            if (isActive) return;
            Packet p = new();
            p.dest = remoteID;
            p.repository = rep.ID;
            if (remoteID == Guid.Empty.ToString())
            {
                await failureProtocol(remoteID);
                return;
            }
            if (socket == null || socket.Connected == false)
            {
                await failureProtocol(remoteID);
                return;
            }
            isActive = true;
            isIncomming = true;
            NetworkOperations.TimeAccessToPush.Add(remoteID);
            LastHandshake = DateTime.Now;
            if(rem_merge_pack == null) await NetworkOperations.AskToInitialTransmission(socket, p, rep.Name);
            else await NetworkOperations.AskToInitialTransmission(socket, p, rep.Name, rem_merge_pack.ID);

            _ = IncommingProtocolTimer();

        }
        public static async Task IncommingProtocolTimer()
        {
            success = false;
            LastHandshake = DateTime.Now;
            bool exit = false;
            while (isActive && !exit)
            {
                await Task.Delay(100);
                if (!isActive || success)
                {
                    exit = true;
                    break;
                }
                if ((DateTime.Now - LastHandshake).TotalSeconds > timeotTime)
                {
                    await failureProtocol(remoteID);
                    break;
                }
            }
            if (!success)
            {
                await failureProtocol(remoteID);
                await Application.Current.Dispatcher.Invoke(async () =>
                {
                    tp.Reinit("Приём файлов: ");
                    tp.MarkAsFailure("Ошибка. Время ответа истекло.");

                });

            }
            else
            {
                await Application.Current.Dispatcher.Invoke(async () =>
                {
                    tp.Reinit("Приём файлов: ");
                    tp.MarkAsSucess("Файлы успешно получены и сохранены.");
                });
            }
            NetworkOperations.TimeAccessToPush.Remove(remoteID);
            isActive = false;
            isIncomming = false;
            rem_merge = null;
            local_merge = null;
        }

        public static async Task OutcommingProtocolTimer()
        {
            success = false;
            LastHandshake = DateTime.Now;
            bool exit = false;
            while (isActive && !exit)
            {
                    await Task.Delay(100);
                    if (!isActive || success)
                    {
                        exit = true;
                        break;
                    }
                if ((DateTime.Now - LastHandshake).TotalSeconds > timeotTime)
                {
                    await failureProtocol(remoteID);
                    break;
                }
            }
            if (!success)
            {
                await failureProtocol(remoteID);
                await Application.Current.Dispatcher.Invoke( async() =>
                {
                    if (tp != null)
                    {
                        tp.Reinit("Передача файлов: ");
                        tp.MarkAsFailure("Ошибка. Время ответа истекло.");
                    }
                });

            }
            else
            {
                await Application.Current.Dispatcher.Invoke(async() =>
                {
                    if (tp != null)
                    {
                        tp.Reinit("Передача файлов: ");
                        tp.MarkAsSucess("Файлы успешно Отправлены.");
                    }
                });
            }
     
            isActive = false;
            isIncomming = false;
            rem_merge = null;
            local_merge = null;
        }

    }

}
