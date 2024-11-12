using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Threading.Tasks;

[Flags]
public enum LogLevel
{
    None = 0,
    Info = 1,
    Error = 2
}

public class MultiThreadedServer
{
    public static int[] vetor = new int[1000];
    public static int soma = 0;
    private static int counter = 0;
    private static readonly object[] lockObjects = new object[vetor.Length];
    public static bool useLock = true;

    public static void Log(string message, LogLevel level)
    {
        if (level == LogLevel.Error || level == LogLevel.Info)
        {
            Console.WriteLine(message);
        }
    }

    static MultiThreadedServer()
    {
        for (int i = 0; i < vetor.Length; i++)
        {
            lockObjects[i] = new object();
        }
    }

    public static void Main(string[] args)
    {
        int port = 12345;
        TcpListener server = new TcpListener(IPAddress.Any, port);
        server.Start();
        Log("Servidor iniciado na porta " + port, LogLevel.Info);

        try
        {
            while (true)
            {
                TcpClient clientSocket = server.AcceptTcpClient();
                Log("Cliente conectado: " + ((IPEndPoint)clientSocket.Client.RemoteEndPoint).Address, LogLevel.None);
                Task.Run(() => HandleClient(clientSocket));
            }
        }
        catch (Exception e)
        {
            Log("Erro no servidor: " + e.Message, LogLevel.Error);
        }
        finally
        {
            server.Stop();
        }
    }

    private static void HandleClient(TcpClient clientSocket)
    {
        try
        {
            using (NetworkStream stream = clientSocket.GetStream())
            using (StreamReader reader = new StreamReader(stream))
            using (StreamWriter writer = new StreamWriter(stream) { AutoFlush = true })
            {
                int numberOfRequests = int.Parse(reader.ReadLine());
                if (useLock)
                {
                    for (int i = 0; i < numberOfRequests; i++)
                    {
                        string mensagem = reader.ReadLine();
                        if (mensagem == null) return;

                        int pos = int.Parse(mensagem);

                        if (pos >= 0 && pos < vetor.Length)
                        {
                            lock (lockObjects[pos])
                            {
                                vetor[pos]++;
                                writer.WriteLine(counter);
                            }
                        }
                        else
                        {
                            writer.WriteLine("Erro: posição fora do limite.");
                        }
                    }
                    counter++;
                }
                else
                {
                    ProcessRequests(reader, writer, numberOfRequests);
                }

                soma = vetor.Sum();
                writer.WriteLine(counter);
                writer.WriteLine(soma);
            }
        }
        catch (IOException e)
        {
            Log("Erro na comunicação com o cliente: ", LogLevel.Error);
        }
    }

    private static void ProcessRequests(StreamReader reader, StreamWriter writer, int numberOfRequests)
    {
        for (int i = 0; i < numberOfRequests; i++)
        {
            string mensagem = reader.ReadLine();
            if (mensagem == null) return;

            int pos = int.Parse(mensagem);

            if (pos >= 0 && pos < vetor.Length)
            {
                {
                    vetor[pos]++;
                    writer.WriteLine(counter);
                }
            }
            else
            {
                writer.WriteLine("Erro: posição fora do limite.");
            }
        }

        counter++;
    }
}