using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using UnityEngine.SocialPlatforms;
using UnityEngine.UI;

using System.Threading.Tasks;

using TMPro;
using Unity.VisualScripting;
using static NetServer;
using System.Threading;
using UnityEditor.PackageManager;

public class NetServer : MonoBehaviour
{
    public static System.Random rand = new System.Random();

    public TMP_InputField _IP;
    public TMP_InputField _PORT;
    public static TMP_InputField _CONSOLE;
    public static Scrollbar _CONSCROLL;

    static Socket serverTCPSocket;
    static Socket serverUDPSocket;
    static UdpClient serverUDP;


    

    //static IPEndPoint clientUDPEP;

    public static byte[] udpBuffer = new byte[512];

    static CancellationTokenSource cts = new CancellationTokenSource();

    public struct Player
    {
        public byte[] tcpBuffer;
        public short playerID;
        public string playerName;
        public string playerKartID;
        public Socket playerTCPSocket;

        public bool udpIsSetup;
        public IPEndPoint playerEP;
        //public static EndPoint playerEP;

        public float[] playerInput;
        public float[] playerPosition;
        public float[] playerRotation;

        public Player(short consID, Socket consSocket)
        {
            tcpBuffer = new byte[512];
            playerName = "NULL";
            playerKartID = "NULL";
            playerID = consID;
            playerTCPSocket = consSocket;
            udpIsSetup = false;
            playerEP = new IPEndPoint(IPAddress.Any, 0);
            playerInput = new float[2];
            playerPosition = new float[3];
            playerRotation = new float[3];
        }

        public void SetPlayerUDPEndpoint(IPEndPoint endPoint)
        {
            playerEP = endPoint;
            udpIsSetup = true;
        }

        public void SetPlayer(ref byte[] data)
        {
            string[] contents = Encoding.ASCII.GetString(data).Split("#");
            this.playerName = contents[0];
            this.playerKartID = contents[1];
            Debug.Log(playerID + ": " + playerDList[playerID].playerName + " KID: " + playerDList[playerID].playerKartID);
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

    public static float udpSendTimeInterval = 1;
    public static float udpSendTimer = 0.0f;

    private static float tcpDlistSendInterval = 1;
    private static float tcpDlistSendTimer = 0;

    private void Awake()
    {
        
    }

    void Start()
    {
        _CONSOLE = GameObject.Find("INF_Console").GetComponent<TMP_InputField>();
        _CONSCROLL = _CONSOLE.transform.Find("Scrollbar").GetComponent<Scrollbar>();
    }

    private void Update()
    {
        udpSendTimer += Time.deltaTime;
        tcpDlistSendTimer += Time.deltaTime;

        if(tcpDlistSendTimer >= tcpDlistSendInterval)
        {
            tcpPlayerDListSend();
            tcpDlistSendTimer -= tcpDlistSendInterval;
        }
    }

    public void StartServer()
    {
        IPAddress ip = IPAddress.Parse("192.168.2.43");
        IPEndPoint serverEP = new IPEndPoint(ip, 12581);
        serverTCPSocket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        //serverUDPSocket = new Socket(ip.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

        serverUDP = new UdpClient(serverEP);

        try
        {
            // TCP
            serverTCPSocket.Bind(serverEP);
            serverTCPSocket.Listen(50);

            //clientUDPEP = new IPEndPoint(IPAddress.Any, 0);
            //serverUDPSocket.Bind(serverEP);

            Task.Run(() => { tcpAccept(); }, cts.Token);                                                    // tcp accept thread

            Task.Run(() => { udpReceive(); }, cts.Token);                                                   // udp receive

            ConPrint("[SYSTEM] TCP Server Started");
            
        }
        catch (Exception ex)
        {
            ConPrint(ex.ToString());
            throw;
        }
    }

    public static void tcpAccept()                                                                           // tcp accept
    {
        try
        {
            UnityMainThreadDispatcher.Instance().Enqueue(()
                => ConPrint("[SYSTEM] Server Accepting..."));
            
            Socket acceptedSocket = serverTCPSocket.Accept();
            short playerid = (short)rand.Next(1000, 9999);
            Player acceptedPlayer = new Player(playerid, acceptedSocket);
            playerDList.Add(playerid, acceptedPlayer);
            UnityMainThreadDispatcher.Instance().Enqueue(()
                => ConPrint("[SYSTEM] Client Accepted From: " + acceptedSocket.ToString()));
            
            Task.Run(() => { tcpReceive(playerid); }, cts.Token);                               // tcp Receive


            //Task.Run(() => { udpSend(); }, cts.Token);
        }
        catch (Exception ex)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(()
                => ConPrint(ex.ToString()));
            throw;
        }

        tcpAccept();                                                                                        // recursive
        
    }

    public static void tcpReceive(short playerID)
    {
        try
        {
            Array.Clear(playerDList[playerID].tcpBuffer, 0, playerDList[playerID].tcpBuffer.Length);
            int recv = playerDList[playerID].playerTCPSocket.Receive(playerDList[playerID].tcpBuffer);

            short[] header = new short[2];
            Buffer.BlockCopy(playerDList[playerID].tcpBuffer, 0, header, 0, 2);
            

            switch (header[0])
            {
                // First Login
                case 0:
                    byte[] content = new byte[recv - 2];
                    Buffer.BlockCopy(playerDList[playerID].tcpBuffer, 2, content, 0, recv - 2);
                    playerDList[playerID].SetPlayer(ref content);

                    string[] contents = Encoding.ASCII.GetString(playerDList[playerID].tcpBuffer, 2, recv - 2).Split("#");
                    
                    UnityMainThreadDispatcher.Instance().Enqueue(()
                        => ConPrint("Get name: " + contents[0]));

                    UnityMainThreadDispatcher.Instance().Enqueue(()
                        => ConPrint("Get kartID: " + contents[1]));

                    byte[] byID = new byte[4];
                    short[] idBackHeader = new short[2];
                    idBackHeader[0] = 0;
                    idBackHeader[1] = playerDList[playerID].playerID;
                    Buffer.BlockCopy(idBackHeader, 0, byID, 0, 4);
                    tcpSend(playerDList[playerID], byID);
                    
                    UnityMainThreadDispatcher.Instance().Enqueue(()
                        => ConPrint("ID Sent: " + idBackHeader[1]));

                    break;

                case 9:
                    byte[] byLate = new byte[2];
                    Buffer.BlockCopy(header, 0, byLate, 0, 2);
                    tcpSend(playerDList[playerID], byLate);
                    Debug.Log("Latency send Back");
                    break;

                default:
                    break;
            }
        }
        catch (Exception ex)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(()
                => ConPrint(ex.ToString()));
            throw;
        }
        tcpReceive(playerID);                                                                                     // tcp receive recursive
    }

    public static void tcpSend(Player player, byte[] msg)
    {
        player.playerTCPSocket.Send(msg);
    }

    public static void udpReceive()                                                                              // udp receive
    {
        try
        {
            Array.Clear(udpBuffer, 0, udpBuffer.Length);

            //IPEndPoint clientEP = new IPEndPoint(IPAddress.Any, 0);
            IPEndPoint clientEP = new IPEndPoint(IPAddress.Any, 0);
            udpBuffer = serverUDP.Receive(ref clientEP);
            //int recv = serverUDPSocket.ReceiveFrom(udpBuffer, ref clientEP);
            int recv = udpBuffer.Length;

            short[] shortBuffer = new short[2];
            Buffer.BlockCopy(udpBuffer, 0, shortBuffer, 0, 4);
            byte[] byContent = new byte[recv - 4];
            Buffer.BlockCopy(udpBuffer, 4, byContent, 0, recv - 4);

            if (playerDList.ContainsKey(shortBuffer[1]))
            {
                playerDList[shortBuffer[1]].SetPlayerUDPEndpoint(clientEP);
            }

            switch (shortBuffer[0])
            {
                // Update Position
                case 0:
                    if (playerDList.ContainsKey(shortBuffer[1]))
                    {
                        Buffer.BlockCopy(byContent, 0, playerDList[shortBuffer[1]].playerPosition, 0, 12);
                        UnityMainThreadDispatcher.Instance().Enqueue(()
                                 => ConPrint("ID: " + shortBuffer[1] + " Position: " 
                                 + playerDList[shortBuffer[1]].playerPosition[0] + " " 
                                 + playerDList[shortBuffer[1]].playerPosition[1] + " " 
                                 + playerDList[shortBuffer[1]].playerPosition[2]));

                        Buffer.BlockCopy(byContent, 12, playerDList[shortBuffer[1]].playerRotation, 0, 12);

                        UnityMainThreadDispatcher.Instance().Enqueue(()
                                 => ConPrint("ID: " + shortBuffer[1] + " Rotation: "
                                 + playerDList[shortBuffer[1]].playerRotation[0] + " "
                                 + playerDList[shortBuffer[1]].playerRotation[1] + " "
                                 + playerDList[shortBuffer[1]].playerRotation[2]));
                    }

                    byte[] transBuffer = new byte[2 + 26 * playerDList.Count];
                    short[] headerBuffer = { 0, -1};

                    Buffer.BlockCopy(headerBuffer, 0, transBuffer, 0, 2);

                    int ind = 0;
                    foreach (Player player in playerDList.Values)
                    {
                        headerBuffer[1] = player.playerID;
                        Buffer.BlockCopy(headerBuffer, 2, transBuffer, ind * 26 + 2, 2);
                        Buffer.BlockCopy(player.playerPosition, 0, transBuffer, ind * 26 + 2 + 2, 12);
                        Buffer.BlockCopy(player.playerRotation, 0, transBuffer, ind * 26 + 2 + 2 + 12, 12);
                        ind++;
                    }

                    serverUDP.Send(transBuffer, transBuffer.Length, clientEP);

                    break;

                default:
                    break;
            }
        }
        catch (Exception ex)
        {

            UnityMainThreadDispatcher.Instance().Enqueue(()
                => ConPrint(ex.ToString()));
            throw;
        }
            
        udpReceive();                                                                                                           // udp recursive
    }

    void tcpPlayerDListSend()
    {
        if(playerDList.Count > 0)
        {

            foreach (Player player in playerDList.Values)
            {
                Debug.Log(player.playerID + ": " + player.playerName + " KID: " + player.playerKartID);
            }
            /*
            short[] backHeader = new short[1];
            backHeader[0] = 1;
            string dlistToSend = string.Empty;

            foreach (Player player in playerDList.Values)
            {
                dlistToSend += player.playerID.ToString() + "," + player.playerName + "," + player.playerKartID + "#";
            }
            dlistToSend = dlistToSend.Substring(0, dlistToSend.Length - 1);
            Debug.Log(dlistToSend);

            byte[] byDlist = new byte[2 + dlistToSend.Length];

            Buffer.BlockCopy(backHeader, 0, byDlist, 0, 2);
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(dlistToSend), 0, byDlist, 2, byDlist.Length - 2);

            foreach (Player player in playerDList.Values)
            {
                tcpSend(player, byDlist);
            }
            */
        }
    }

    public static void ConPrint(string cont)
    {
        _CONSOLE.text += "\n" + cont;

        _CONSCROLL.value = 1f;
    }

    private void OnApplicationQuit()
    {
        cts.Cancel();
        if(serverTCPSocket != null) serverTCPSocket.Close();
        if(serverUDPSocket != null) serverUDPSocket.Close();
        if (serverUDP != null) serverUDP.Close();
    }

}
