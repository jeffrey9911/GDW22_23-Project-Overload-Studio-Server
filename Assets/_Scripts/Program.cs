using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

public class NetServer
{
    static Socket serverTCPSocket;
    static Socket serverUDPSocket;

    static Random rand = new Random();

    public struct Player
    {
        public byte[] buffer;
        public short playerID;
        public string playerName;
        public Socket playerSocket;

        public float[] playerInput;
        public float[] playerPosition;
        public float[] playerRotation;

        public Player(short consID, Socket consSocket)
        {
            buffer = new byte[1024];
            playerID = consID;
            playerName = string.Empty;
            playerSocket = consSocket;
            playerInput = new float[2];
            playerPosition = new float[3];
            playerRotation = new float[3];
        }
    }

    public struct Room
    {
        public int roomID;
        public string roomName;
        public List<Player> playerListInRoom;
    }

    public static Dictionary<short, Player> playerDList = new Dictionary<short, Player>();
    public static Dictionary<int, Room> roomDList = new Dictionary<int, Room>();

    public static void StartServer(string ipIn, int ipPort)
    {
        IPAddress ip = IPAddress.Parse(ipIn);
        IPEndPoint serverEP = new IPEndPoint(ip, ipPort);
        serverTCPSocket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        serverUDPSocket = new Socket(ip.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

        try
        {
            // TCP
            serverTCPSocket.Bind(serverEP);
            serverTCPSocket.Listen(50);
            Console.WriteLine("[SYSTEM] TCP Server Started");

            serverTCPSocket.BeginAccept(new AsyncCallback(tcpAcceptCallBack), null);
        }
        catch(Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }

    private static void tcpAcceptCallBack(IAsyncResult result)
    {
        Socket acceptedSocket = serverTCPSocket.EndAccept(result);
        short playerid = (short)rand.Next(1000, 9999);
        Player acceptedPlayer = new Player(playerid, acceptedSocket);
        playerDList.Add(playerid, acceptedPlayer);
        Console.WriteLine("[SYSTEM] Client Accepted From: " + result.ToString());

        Task.Run(() => { tcpReceive(playerDList[playerid]); });

        serverTCPSocket.BeginAccept(new AsyncCallback(tcpAcceptCallBack), null);
    }

    private static void tcpReceive(Player player)
    {
        Array.Clear(player.buffer, 0, player.buffer.Length);
        int recv = player.playerSocket.Receive(player.buffer);

        short[] header = new short[1];
        Buffer.BlockCopy(player.buffer, 0, header, 0, 2);
        switch(header[0])
        {
            // First Login
            case 0:
                string name = Encoding.ASCII.GetString(player.buffer, 2, player.buffer.Length - 2);
                player.playerName = name;
                byte[] msg = new byte[4];
                header[0] = 0;
                Buffer.BlockCopy(header, 0, msg, 0, 2);
                header[0] = player.playerID;
                Buffer.BlockCopy(header, 0, msg, 2, 2);
                tcpSend(player, msg);
                break;

            default:
                break;
        }

        tcpReceive(player);
    }

    private static void tcpSend(Player player, byte[] msg)
    {
        player.playerSocket.Send(msg);
    }

    public static int Main(string[] args)
    {
        Console.WriteLine("[SYSTEM] Please input ipv4 address:");
        string ipAddress = Console.ReadLine();
        Console.WriteLine("[SYSTEM] Please input port number:");
        string port = Console.ReadLine();
        StartServer(ipAddress, int.Parse(port));
        return 0;
    }
}