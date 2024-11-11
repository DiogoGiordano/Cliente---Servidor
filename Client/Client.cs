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

    public static void Log(string message, LogLevel level)
    {
        if ((CurrentLogLevel & level) == level)
        {
            Console.WriteLine(message);
        }
    }

    static void Main(string[] args)
    {
        if (args.Length < 1 || !int.TryParse(args[0], out int nClients))
        {
            Log("Uso: Client <posição> <valor> <nClients>", LogLevel.Error);
            return;
        }
        
        Thread[] clients = new Thread[nClients];

        for (int i = 0; i < nClients; i++)
        {
            Random random = new Random();
            
            int pos = random.Next(0, 1000);
            int value = random.Next(0, 10000);
            
            
            clients[i] = new Thread(() => StartClient(pos, value)); 
            clients[i].Start();
        }

        foreach (var clientThread in clients)
        {
            clientThread.Join();
        }
    }

    static void StartClient(int pos, int value)
    {
        string parametros = $"{pos},{value}";
        string serverIp = "127.0.0.1";
        int port = 12345;
        int numberRequests = 2;

        try
        {
            using (TcpClient client = new TcpClient(serverIp, port))
            using (StreamReader inStream = new StreamReader(client.GetStream()))
            using (StreamWriter outStream = new StreamWriter(client.GetStream()) { AutoFlush = true })
            {
                outStream.WriteLine(numberRequests); // Envia o número de requisições

                for (int i = 0; i < numberRequests; i++)
                {
                    outStream.WriteLine(parametros); // Envia os parâmetros da requisição
                    string? response = inStream.ReadLine();
                    Log($"Resposta do servidor: {response}", LogLevel.Info);
                }
            }
        }
        catch (Exception e)
        {
            Log($"Erro no cliente: {e.Message}", LogLevel.Error);
        }
    }
}
