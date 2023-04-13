using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;

class Server
{
    private static Dictionary<TcpClient, string> Clients = new Dictionary<TcpClient, string>();
    private static List<KeyValuePair<string, TcpClient>> ClientMessages = new List<KeyValuePair<string, TcpClient>>();

    private static Queue<string> LastThreeMessages = new Queue<string>();
    private static readonly object LockObject = new object();
    private static bool BroadcastedToClients = false;

    static async Task Main(string[] args)
    {
        //This message is enqueued for testing purposes.
        LastThreeMessages.Enqueue("first message");
        IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
        int port = 33333;

        TcpListener serverTcpSocket = new TcpListener(ipAddress, port);

        serverTcpSocket.Start();

        Console.WriteLine("Server is listening to incoming requests.");

        Task t1 = ConnectionListener(serverTcpSocket);
        Task t2 = ConnectionListener(serverTcpSocket);
        Task t3 = ConnectionListener(serverTcpSocket);

        await Task.WhenAll(t1, t2, t3);
    }

    private async static Task ConnectionListener(TcpListener serverTcpSocket)
    {
        while (true)
        {
            TcpClient clientTcpSocket = await serverTcpSocket.AcceptTcpClientAsync();

            string clientAddress = ((IPEndPoint)clientTcpSocket.Client.RemoteEndPoint).Address.ToString();
            int clientPort = ((IPEndPoint)clientTcpSocket.Client.RemoteEndPoint).Port;

            Console.WriteLine("Client connected: " + clientAddress + ":" + clientPort);

            NetworkStream networkStream = clientTcpSocket.GetStream();

            StreamReader reader = new StreamReader(networkStream, Encoding.ASCII);

            string? clientName = await reader.ReadLineAsync();
            Clients[clientTcpSocket] = clientName;

            await SendInitialMessagesTask(networkStream);
            await ReceiveMessagesFromClientTask(reader, clientTcpSocket);

            BroadcastToOtherClients();
            await CloseServerTask(networkStream, serverTcpSocket);

            break;
        }
    }

    private async static Task SendInitialMessagesTask(NetworkStream networkStream)
    {
        if (LastThreeMessages.Count > 0)
        {
            foreach (var message in LastThreeMessages)
            {
                byte[] bytesToServer = Encoding.ASCII.GetBytes(message + "\n");
                await networkStream.WriteAsync(bytesToServer, 0, bytesToServer.Length);
            }
            byte[] lastMessage = Encoding.ASCII.GetBytes("Finished\n");
            await networkStream.WriteAsync(lastMessage, 0, lastMessage.Length);
        }
        else
        {
            byte[] lastMessage = Encoding.ASCII.GetBytes("Finished\n");
            await networkStream.WriteAsync(lastMessage, 0, lastMessage.Length);
        }
    }

    private async static Task ReceiveMessagesFromClientTask(StreamReader reader, TcpClient clientTcpSocket)
    {
        while (true)
        {
            string? messageFromTcpClient = await reader.ReadLineAsync();

            if (messageFromTcpClient != null && messageFromTcpClient != "Finished")
            {
                Console.WriteLine("The message from client is: " + messageFromTcpClient);
                ClientMessages.Add(new KeyValuePair<string, TcpClient>(messageFromTcpClient, clientTcpSocket));
                lock (LockObject)
                {
                    LastThreeMessages.Enqueue(messageFromTcpClient);
                    if (LastThreeMessages.Count > 3)
                    {
                        LastThreeMessages.Dequeue();
                    }
                }
            }
            else
            {
                break;
            }
        }
    }

    private static void BroadcastToOtherClients()
    {
        lock (LockObject)
        {
            if (!BroadcastedToClients)
            {
                foreach (var mc in ClientMessages)
                {
                    var client = mc.Value;
                    foreach (var cn in Clients)
                    {
                        if (cn.Key != client)
                        {
                            var networkStream = cn.Key.GetStream();
                            var message = "Message from " + cn.Value + " is sent." + mc.Key + "\n";
                            byte[] bytesToClient = Encoding.ASCII.GetBytes(message);
                            networkStream.WriteAsync(bytesToClient, 0, bytesToClient.Length);
                        }
                    }
                }

                foreach (var cn in Clients)
                {
                    var networkStream = cn.Key.GetStream();
                    byte[] lastBytesToClient = Encoding.ASCII.GetBytes("Finished\n");
                    networkStream.WriteAsync(lastBytesToClient, 0, lastBytesToClient.Length);
                }
                BroadcastedToClients = true;
            }
        }
    }

    private async static Task CloseServerTask(NetworkStream networkStream, TcpListener serverTcpSocket)
    {
        string enteredText = Console.ReadLine();
        if (enteredText == "close")
        {
            byte[] lastBytesToClient = Encoding.ASCII.GetBytes("close\n");
            await networkStream.WriteAsync(lastBytesToClient, 0, lastBytesToClient.Length);

            Console.WriteLine("Closing server listener in 4 seconds.");
            Thread.Sleep(4000);
            serverTcpSocket.Stop();
        }
    }
}