using System.Net.Sockets;
using System.IO;
using System;

[Flags]
public enum LogLevel
{
    None = 0,
    Info = 1,
    Error = 2
}

class Client
{
    public static LogLevel CurrentLogLevel = LogLevel.Info;

    public static int counter = 0;
    public static int soma = 0;

    public static void Log(string message, LogLevel level)
    {
        if ((level == LogLevel.Error) || (level == LogLevel.Info))
        {
            Console.WriteLine(message);
        }
    }

    static void Main(string[] args)
    {
        if (args.Length < 1 || !int.TryParse(args[0], out int nClients))
        {
            Log("Uso: Client <nClients>", LogLevel.Error);
            return;
        }

        Thread[] clients = new Thread[nClients];
        Random random = new Random();

        for (int i = 0; i < nClients; i++)
        {
            int pos = random.Next(0, 1000);
            clients[i] = new Thread(() => StartClient(pos));
            clients[i].Start();
        }

        foreach (var clientThread in clients)
        {
            clientThread.Join();
        }

        Log($"Contador do servidor: {counter}", LogLevel.Info);
        Log($"Soma de todos os valores do vetor: {soma}", LogLevel.Info);
    }


    static void StartClient(int pos)
    {
        string serverIp = "127.0.0.1";
        int port = 12345;
        int numberRequests = 5;

        try
        {
            using (TcpClient client = new TcpClient(serverIp, port))
            using (StreamReader inStream = new StreamReader(client.GetStream()))
            using (StreamWriter outStream = new StreamWriter(client.GetStream()) { AutoFlush = true })
            {
                outStream.WriteLine(numberRequests);

                for (int i = 0; i < numberRequests; i++)
                {
                    outStream.WriteLine(pos); 
                    string? response = inStream.ReadLine();
                    Log($"Resposta do servidor: {response}", LogLevel.None);
                }

                string? responseCounter = inStream.ReadLine();
                counter = responseCounter == null ? 0 : int.Parse(responseCounter);
                string? responseSoma = inStream.ReadLine();
                soma = responseSoma == null ? 0 : int.Parse(responseSoma); 
            }
        }
        catch (Exception e)
        {
            Log($"Erro no cliente: {e.Message}", LogLevel.Error);
        }
    }
}