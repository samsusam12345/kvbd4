using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using CryptoUtilsLib;
using System.Threading.Tasks;
using System.Threading;

namespace ConsoleApp1
{
    class Connections
    {
        public TcpClient tcpClient { get; set; }
        public NetworkStream clientNetwork { get; set; }
        public string name { get; set; }
        public BigInteger V { get; set; }
    }

    class Program
    {
        static List<Connections> Clients = new List<Connections> { };
        static volatile string currentName = null;
        static volatile string NamefromThread = null;
        static volatile string VfromThread = null;
        static volatile string command = null;

        static void Linker(Connections client)
        {
            byte[] bytes = new byte[1024];
            while (true) {
                if (client.name == NamefromThread) {
                    if (command == "start")
                    {
                        bytes = Encoding.ASCII.GetBytes("New Request from$" + currentName + "$" + VfromThread);
                        client.clientNetwork.Write(bytes, 0, bytes.Length);
                        client.clientNetwork.Flush();
                        VfromThread = string.Empty;
                        NamefromThread = string.Empty;
                        command = string.Empty;
                        currentName = string.Empty;
                    }
                    else if (command == "No")
                    {
                        bytes = new byte[512];
                        bytes = Encoding.ASCII.GetBytes(currentName + " deny your request");
                        client.clientNetwork.Write(bytes, 0, bytes.Length);
                        client.clientNetwork.Flush();
                        NamefromThread = string.Empty;
                        command = string.Empty;
                        currentName = string.Empty;
                    }
                    else if(command == "Yes")
                    {
                        bytes = new byte[256];
                        bytes = Encoding.ASCII.GetBytes("ready");
                        client.clientNetwork.Write(bytes, 0, bytes.Length);
                        client.clientNetwork.Flush();
                        NamefromThread = string.Empty;
                        command = string.Empty;
                        currentName = string.Empty;
                    }
                }
            }
        }

        static void ResponseProccessing(Connections client)
        {
            Task holder = Task.Run(() => Linker(client));
            string selectedName = null; ;
            while (true)
            {
                if (client.clientNetwork.DataAvailable)
                {
                    byte[] bytes = new byte[256];
                    int length = client.clientNetwork.Read(bytes, 0, bytes.Length);
                    string response = Encoding.ASCII.GetString(bytes, 0, length);
                    if (response == "auth")
                    {
                        Console.WriteLine("[{0}] get auth from " + client.name, DateTime.Now.ToString());
                        string allusers = string.Empty;
                        foreach (var user in Clients)
                        {
                            if (user.name == client.name)
                                continue;
                            else
                                allusers += user.name + "$";
                        }
                        bytes = Encoding.ASCII.GetBytes(allusers);
                        client.clientNetwork.Write(bytes, 0, bytes.Length);
                        client.clientNetwork.Flush();
                    }
                    else if (response.Contains("StartWith"))
                    {
                        selectedName = response.Split('$')[1];
                        Console.WriteLine("[{0}] "+client.name + " selected " + selectedName, DateTime.Now.ToString());
                        command = "start";
                        currentName = client.name;
                        VfromThread = client.V.ToString();
                        NamefromThread = selectedName;
                    }
                    else if (response.Contains("AnswerNo"))
                    {
                        selectedName = response.Split('$')[1];
                        Console.WriteLine("[{0}] "+client.name + " deny request from " + selectedName, DateTime.Now.ToString());
                        command = "No";
                        currentName = client.name;
                        NamefromThread = selectedName;
                    }
                    else if (response.Contains("AnswerYes"))
                    {
                        selectedName = response.Split('$')[1];
                        Console.WriteLine("[{0}] "+client.name + " accept request from " + selectedName, DateTime.Now.ToString());
                        command = "Yes";
                        currentName = client.name;
                        NamefromThread = selectedName;
                    }
                    else if(response == "AuthOK")
                    {
                        Console.WriteLine("[{0}] "+client.name+" authentiacated", DateTime.Now.ToString());
                    }
                    else if (response == "!AuthOK")
                    {
                        Console.WriteLine("[{0}] " + client.name + " not authentiacated", DateTime.Now.ToString());
                    }
                }
            }
        }

        static void Main(string[] args)
        {
            TcpClient newclient = null;
            NetworkStream stream = null;
            string ClientName = string.Empty;
            byte[] bytes = new byte[1024];

            (BigInteger, BigInteger) PQ = Fiat_Shamir.get_PQ();
            BigInteger N = PQ.Item1 * PQ.Item2;

            TcpListener server = new TcpListener(IPAddress.Parse("127.0.0.1"), 9478);
            server.Start();
            Console.WriteLine("Server start...");
            Console.WriteLine("N = " + N);

            while (true)
            {
                newclient = server.AcceptTcpClient();
                stream = newclient.GetStream();
                bytes = Encoding.ASCII.GetBytes(N.ToString());
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();

                bytes = new byte[1024];
                while (!stream.CanRead) ;
                int length = stream.Read(bytes, 0, bytes.Length);
                string[] data = Encoding.ASCII.GetString(bytes, 0, length).Split('$');
                ClientName = data[0];
                BigInteger _V = BigInteger.Parse(data[1]);
                Console.WriteLine("[{0}] Client connected", DateTime.Now.ToString());

                Connections Currentclient = new Connections { tcpClient = newclient, clientNetwork = stream, name = ClientName, V = _V };

                bytes = new byte[1024];
                bytes = Encoding.ASCII.GetBytes("Welcome");
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();

                foreach (var client in Clients)
                {
                    stream = client.tcpClient.GetStream();
                    bytes = Encoding.ASCII.GetBytes(ClientName + " has connected");
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush();
                }
                Clients.Add(new Connections { tcpClient = newclient, clientNetwork = stream, name = ClientName, V = _V });
                Console.WriteLine("[{0}] count of clients = " + Clients.Count, DateTime.Now.ToString());
                int i = 1;
                foreach (var clent in Clients)
                    Console.WriteLine(i++ + ") " + clent.name);

                Task th = Task.Run(() => ResponseProccessing(Currentclient));
            }
            server.Stop();
        }
    }
}