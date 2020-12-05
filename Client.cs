using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using CryptoUtilsLib;
using System.Threading.Tasks;
using System.Numerics;
using System.Net;

namespace client
{
    class Program
    {
        static volatile TcpClient client = null;
        static volatile NetworkStream stream = null;
        static volatile string[] alluser;
        static volatile string interlocutorV;
        static volatile string interlocutorUserName;

        static void ProoveConn(BigInteger N, BigInteger S)
        {
            TcpClient proover = new TcpClient("127.0.0.1", 9559);
            NetworkStream prooverStream = proover.GetStream();
            Console.WriteLine("Connected");
            byte[] bytes = new byte[1024];
            int length;
            BigInteger R, X, Y, e;

            for(int i=0; i< 15; i++)
            {
                R = Fiat_Shamir.get_R(N);
                X = BigInteger.ModPow(R, 2, N);

                bytes = new byte[1024];
                bytes = Encoding.ASCII.GetBytes(X.ToString());
                prooverStream.Write(bytes, 0, bytes.Length);
                prooverStream.Flush();

                while (!prooverStream.DataAvailable) ;
                bytes = new byte[1024];
                length = prooverStream.Read(bytes, 0, bytes.Length);
                e = BigInteger.Parse(Encoding.ASCII.GetString(bytes, 0, length));

                Y = (R * (BigInteger.Pow(S, (int)e))) % N;
                bytes = new byte[1024];
                bytes = Encoding.ASCII.GetBytes(Y.ToString());
                prooverStream.Write(bytes, 0, bytes.Length);
                prooverStream.Flush();
                Console.WriteLine("Round {0} done.", i+1);
            }
            while (!prooverStream.DataAvailable) ;
            byte[] response = new byte[1];
            prooverStream.Read(response, 0, 1);
            Console.WriteLine(response[0] == 1 ? "Authentication success" : "Authentication failed\nConnection closed");

            byte[] response1 = new byte[128];
            response1 = Encoding.ASCII.GetBytes(response[0] == 1 ? "AuthOK": "!AuthOK");
            stream.Write(response1, 0, response1.Length);
            stream.Flush();
        }

        static void ServerMode(BigInteger N)
        {
            TcpListener serverExchange = new TcpListener(IPAddress.Parse("127.0.0.1"), 9559);
            serverExchange.Start();
            Console.WriteLine(interlocutorUserName + "'s V = " + interlocutorV);
            Console.WriteLine("Wait connection...");
            TcpClient verifier = serverExchange.AcceptTcpClient();
            NetworkStream verifierStream = verifier.GetStream();
            Console.WriteLine("Connected");

            int success = 0;
            byte[] bytes = new byte[1024];
            int length;
            BigInteger e, X, Y, V = BigInteger.Parse(interlocutorV);

            for (int i = 0; i < 15; i++)
            {
                while (!verifierStream.DataAvailable) ;
                bytes = new byte[1024];
                length = verifierStream.Read(bytes, 0, bytes.Length);
                X = BigInteger.Parse(Encoding.ASCII.GetString(bytes, 0, length));

                e = Fiat_Shamir.get_e();
                bytes = new byte[1024];
                bytes = Encoding.ASCII.GetBytes(e.ToString());
                verifierStream.Write(bytes, 0, bytes.Length);
                verifierStream.Flush();

                while (!verifierStream.DataAvailable) ;
                bytes = new byte[1024];
                length = verifierStream.Read(bytes, 0, bytes.Length);
                Y = BigInteger.Parse(Encoding.ASCII.GetString(bytes, 0, length));

                if (BigInteger.ModPow(Y, 2, N) == (X * BigInteger.Pow(V, (int)e)) % N) {
                    success++;
                    Console.WriteLine("Round {0} successful!", i+1);
                }
                else
                    Console.WriteLine("Round {0} not successful!", i + 1);
            }
            byte s = 0;
            if(success == 15) {
                Console.WriteLine("100% succes");
                s = 1;
            }
            else
                Console.WriteLine("Authentication failed\nConnection closed");
            verifierStream.Write(new byte[] { s }, 0, 1);
            verifierStream.Flush();
        }

        static void ClientInterface(BigInteger N)
        {
            while (true) {
                byte[] bytes = new byte[256];
                Console.WriteLine("Enter command:");
                string cmd = Console.ReadLine();
                if (cmd.ToLower() == "auth")
                {
                    bytes = Encoding.ASCII.GetBytes(cmd.ToLower());
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush();

                    int number = int.Parse(Console.ReadLine()) - 1;
                    string SelectedUser = "StartWith$" + alluser[number];
                    Console.WriteLine("You select " + alluser[number]);
                    bytes = new byte[256];
                    bytes = Encoding.ASCII.GetBytes(SelectedUser);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush();
                }
                else if (cmd.ToLower() == "y" || cmd.ToLower() == "n")
                {
                    if (cmd.ToLower() == "y")
                    {
                        bytes = new byte[256];
                        bytes = Encoding.ASCII.GetBytes("AnswerYes$" + interlocutorUserName);
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush();
                        Task ServerOprion = Task.Run(() => ServerMode(N));
                    }
                    else
                    {
                        bytes = new byte[256];
                        bytes = Encoding.ASCII.GetBytes("AnswerNo$"+interlocutorUserName);
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush();
                    }
                }
                else
                    Console.WriteLine("Unknown command");
            }
        }

        static void Main(string[] args)
        {
            byte[] bytes = new byte[1024];
            Console.Write("Enter your Name: ");
            string name = Console.ReadLine();
            client = new TcpClient("127.0.0.1", 9478);
            stream = client.GetStream();

            int length = stream.Read(bytes, 0 , bytes.Length); 
            BigInteger N = BigInteger.Parse(Encoding.ASCII.GetString(bytes, 0, length));
            BigInteger S = Fiat_Shamir.get_S(N);
            BigInteger V = BigInteger.ModPow(S, 2, N);

            bytes = Encoding.ASCII.GetBytes(name+"$"+V.ToString());
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();

            bytes = new byte[512];
            while (!stream.CanRead);
            length = stream.Read(bytes, 0, bytes.Length);
            Console.WriteLine(Encoding.ASCII.GetString(bytes, 0, length));
            Console.WriteLine("N = "+N);
            Console.WriteLine("S = "+S);
            Console.WriteLine("V = "+V);

            Task Exchange = Task.Run(() => ClientInterface(N));

            while (true)
            {
                while (!stream.CanRead) ;
                bytes = new byte[512];
                length = stream.Read(bytes, 0, bytes.Length);
                string response = Encoding.ASCII.GetString(bytes, 0, length);

                if (response.Contains("has connected"))
                    Console.WriteLine(response);
                else if(response.Contains("New Request"))
                {
                    string[] newrequest = response.Split('$');
                    interlocutorUserName = newrequest[1];
                    interlocutorV = newrequest[2];
                    Console.WriteLine("You have "+newrequest[0].ToLower()+" "+ interlocutorUserName);
                    Console.WriteLine("Accept request [y/n]?");
                }
                else if (response.Contains("deny"))
                {
                    Console.WriteLine(response);
                }
                else if(response == "ready")
                {
                    Console.WriteLine("Trying connect...");
                    Task newConn = Task.Run(() => ProoveConn(N, S));
                }
                else //there are all users
                {
                    alluser = response.Split('$');
                    Console.WriteLine("Select user");
                    int i = 1;
                    foreach (var user in alluser) {
                        if(user.ToString() != string.Empty)
                            Console.WriteLine(i++ + ") " + user);
                    }
                }
            }
            Console.ReadKey();
            client.Close();
        }
    }
}
