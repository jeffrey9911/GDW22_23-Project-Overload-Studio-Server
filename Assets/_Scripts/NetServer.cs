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
            playerID = consID;
            playerName = string.Empty;
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
    }

    public void StartServer()
    {
        IPAddress ip = IPAddress.Parse("127.0.0.1");
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

            Task.Run(() => { tcpAccept(); }, cts.Token);

            ConPrint("[SYSTEM] TCP Server Started");
            
        }
        catch (Exception ex)
        {
            ConPrint(ex.ToString());
            throw;
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
            
            Task.Run(() => { tcpReceive(playerDList[playerid]); }, cts.Token);

            Task.Run(() => { tcpAccept(); }, cts.Token);

            //Task.Run(() => { udpSend(); }, cts.Token);
        }
        catch (Exception ex)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(()
                => ConPrint(ex.ToString()));
            throw;
        }
        
    }

    public static void tcpReceive(Player player)
    {
        try
        {
            Array.Clear(player.tcpBuffer, 0, player.tcpBuffer.Length);
            int recv = player.playerTCPSocket.Receive(player.tcpBuffer);

            short[] header = new short[1];
            Buffer.BlockCopy(player.tcpBuffer, 0, header, 0, 2);
            byte[] byContent = new byte[player.tcpBuffer.Length - 2];
            Buffer.BlockCopy(player.tcpBuffer, 2, byContent, 0, byContent.Length);

            switch (header[0])
            {
                // First Login
                case 0:
                    string name = Encoding.ASCII.GetString(player.tcpBuffer, 2, recv - 2);
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

            Task.Run(() => { tcpReceive(player); }, cts.Token);
            Task.Run(() => { udpReceive(); }, cts.Token);
        }
        catch (Exception ex)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(()
                => ConPrint(ex.ToString()));
            throw;
        }
        
    }

    public static void tcpSend(Player player, byte[] msg)
    {
        player.playerTCPSocket.Send(msg);
    }

    public static void udpReceive()
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
                
                if(clientEP is IPEndPoint ipEP)
                {
                    //playerDList[shortBuffer[1]].playerEP.Address = ipEP.Address;
                    //playerDList[shortBuffer[1]].playerEP.Port = ipEP.Port;
                    //playerDList[shortBuffer[1]].SetPlayerUDPEndpoint(ipEP);
                }

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

                    /*
                    byte[] tempBuffer = new byte[512];
                    tempBuffer = Encoding.ASCII.GetBytes("UDP SEND TEST!");
                    serverUDP.Send(tempBuffer, tempBuffer.Length, clientEP);
                    */

                    
                    byte[] transBuffer = new byte[26];
                    short[] headerBuffer = new short[1];
                    foreach (Player selectedPlayer in playerDList.Values)
                    {
                        
                    }

                    foreach (Player player in playerDList.Values)
                    {
                        if (player.playerID != playerDList[shortBuffer[1]].playerID)
                        {
                            headerBuffer[0] = player.playerID;
                            Buffer.BlockCopy(headerBuffer, 0, transBuffer, 0, 2);
                            Buffer.BlockCopy(player.playerPosition, 0, transBuffer, 2, 12);
                            Buffer.BlockCopy(player.playerRotation, 0, transBuffer, 14, 12);
                            //serverUDP.Send(transBuffer, transBuffer.Length, selectedPlayer.playerEP);
                            //EndPoint ep = selectedPlayer.playerEP;
                            //serverUDPSocket.SendTo(transBuffer, ep);
                            serverUDP.Send(transBuffer, transBuffer.Length, clientEP);
                            Debug.Log("Send to: " + clientEP.Address +  "\nInfo: " + playerDList[shortBuffer[1]].playerID + " " + clientEP.Address + " " + clientEP.Port + " \n"
                                + player.playerPosition[0] + " " + player.playerPosition[1] + " " + player.playerPosition[2]
                                + "\n" + player.playerRotation);

                        }
                    }


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

        udpReceive();
    }

    public static void udpSend()
    {
        if(udpSendTimer >= udpSendTimeInterval)
        {
            try
            {
                byte[] tempBuffer = new byte[512];
                tempBuffer = Encoding.ASCII.GetBytes("UDP SEND TEST!");

                

                foreach (Player player in playerDList.Values)
                {
                    if(player.udpIsSetup) serverUDP.Send(tempBuffer, tempBuffer.Length, player.playerEP);


                }    

                /*
                byte[] transBuffer = new byte[512];
                transBuffer = Encoding.ASCII.GetBytes("UDP SEND TEST!");

                foreach (Player player in playerDList.Values)
                {
                    //serverUDPSocket.SendTo(transBuffer, player.playerEP);
                    serverUDP.Send(transBuffer, transBuffer.Length, player.playerEP);
                }

                
                foreach (Player selectedPlayer in playerDList.Values)
                {
                    foreach (Player player in playerDList.Values)
                    {
                        if(player.playerID != selectedPlayer.playerID)
                        {
                            short[] headerBuffer = {player.playerID};
                            Buffer.BlockCopy(headerBuffer, 0, transBuffer, 0, 2);
                            Buffer.BlockCopy(player.playerPosition, 0, transBuffer, 2, 12);
                            Buffer.BlockCopy(player.playerRotation, 0, transBuffer, 14, 12);
                            //serverUDP.Send(transBuffer, transBuffer.Length, selectedPlayer.playerEP);
                            EndPoint ep = selectedPlayer.playerEP;
                            serverUDPSocket.SendTo(transBuffer, ep);
                            Debug.Log("Send to: " + selectedPlayer.playerID + " " + selectedPlayer.playerEP.Address + " " + selectedPlayer.playerEP.Port);
                        }
                    }
                }
                */
            }
            catch (Exception ex)
            {

                Debug.Log(ex.ToString());
                throw;
            }

            udpSendTimer -= udpSendTimeInterval;
        }
        

        udpSend();
    }

    public static void ConPrint(string cont)
    {
        _CONSOLE.text += "\n" + cont;

        _CONSCROLL.value = 1f;
    }

    private void OnApplicationQuit()
    {
        cts.Cancel();
        serverTCPSocket.Close();
        serverUDPSocket.Close();
        serverUDP.Close();
    }

}
