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

public class NetServer : MonoBehaviour
{
    public static System.Random rand = new System.Random();

    public TMP_InputField _IP;
    public TMP_InputField _PORT;
    public static TMP_InputField _CONSOLE;
    public static Scrollbar _CONSCROLL;

    static Socket serverTCPSocket;
    static Socket serverUDPSocket;

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
        
    }

    public void StartServer()
    {
        IPAddress ip = IPAddress.Parse("127.0.0.1");
        IPEndPoint serverEP = new IPEndPoint(ip, 8888);
        serverTCPSocket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        serverUDPSocket = new Socket(ip.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

        try
        {
            // TCP
            serverTCPSocket.Bind(serverEP);
            serverTCPSocket.Listen(50);


            Task.Run(() => { tcpAccept(); });

            ConPrint("[SYSTEM] TCP Server Started");
            
        }
        catch (Exception ex)
        {
            ConPrint(ex.ToString());
        }
    }

    public static void tcpAccept()
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
            
            Task.Run(() => { tcpReceive(playerDList[playerid]); });

            Task.Run(() => { tcpAccept(); });
        }
        catch (Exception ex)
        {
            Debug.Log(ex.ToString());
        }
        
    }

    public static void tcpReceive(Player player)
    {
        try
        {
            Array.Clear(player.buffer, 0, player.buffer.Length);
            int recv = player.playerSocket.Receive(player.buffer);

            short[] header = new short[1];
            Buffer.BlockCopy(player.buffer, 0, header, 0, 2);
            byte[] byContent = new byte[player.buffer.Length - 2];
            Buffer.BlockCopy(player.buffer, 2, byContent, 0, byContent.Length);

            switch (header[0])
            {
                // First Login
                case 0:
                    string name = Encoding.ASCII.GetString(player.buffer, 2, recv - 2);
                    player.playerName = name;
                    UnityMainThreadDispatcher.Instance().Enqueue(()
                        => ConPrint("Get name: " + name));

                    byte[] msg = new byte[header.Length * 2 * 2];
                    header[0] = 0;
                    Buffer.BlockCopy(header, 0, msg, 0, 2);
                    header[0] = player.playerID;
                    Buffer.BlockCopy(header, 0, msg, 2, 2);
                    tcpSend(player, msg);

                    UnityMainThreadDispatcher.Instance().Enqueue(()
                        => ConPrint("ID Sent: " + header[0]));
                    break;

                default:
                    break;
            }

            Task.Run(() => { tcpReceive(player); });
        }
        catch (Exception ex)
        {
            Debug.Log(ex.ToString());
        }
        
    }

    public static void tcpSend(Player player, byte[] msg)
    {
        player.playerSocket.Send(msg);
    }

    public static void ConPrint(string cont)
    {
        _CONSOLE.text += "\n" + cont;

        _CONSCROLL.value = 1f;
    }

}
