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
        if (args.Length < 2 || !int.TryParse(args[0], out int pos) || !int.TryParse(args[1], out int value))
        {
            Log("Uso: Client <posição> <valor>", LogLevel.Error);
            return;
        }

        StartClient(pos, value);
    }

    static void StartClient(int pos, int value)
    {
        string parametros = $"{pos},{value}";
        string serverIp = "127.0.0.1";
        int port = 12345;

        try
        {
            using (TcpClient client = new TcpClient(serverIp, port))
            using (StreamReader inStream = new StreamReader(client.GetStream()))
            using (StreamWriter outStream = new StreamWriter(client.GetStream()) { AutoFlush = true })
            {
                outStream.WriteLine(parametros); 

                string? response = inStream.ReadLine();
                Log($"Resposta do servidor: {response}", LogLevel.Info);
            }
        }
        catch (Exception e)
        {
            Log($"Erro no cliente: {e.Message}", LogLevel.Error);
        }
    }
}