using System.Net.Sockets;
using System.IO;
using System;
using System.Net;
using System.Threading;
using Server_Client;

class Client
{
    private static int _nClientes;
    private static LogLevel _currentLogLevel;
    private static int _port;
    private static IPAddress? _ipAddress;
    private static int _nReads;
    private static int _nWrites;
    private static int _counter = 0;
    private static int _soma = 0;
    private static string _sequence;

    public static void Log(string message, LogLevel level)
    {
        if ((_currentLogLevel & level) == level)
        {
            Console.WriteLine($"[{level}] {message}");
        }
    }

    // Exemplo de uso: dotnet run -- 100 127.0.0.1 12345 100 100 2 RW
    static void Main(string[] args)
    {
        if (!ValidateArguments(args, out string? errorMessage))
        {
            Console.WriteLine(errorMessage);
            return;
        }

        Thread[] clients = new Thread[_nClientes];
        Random random = new Random();

        for (int i = 0; i < _nClientes; i++)
        {
            int pos = random.Next(0, 1000); // Gera posições aleatórias iniciais
            clients[i] = new Thread(() => StartClient(pos));
            clients[i].Start();
        }

        foreach (var clientThread in clients)
        {
            clientThread.Join(); // Aguarda todas as threads concluírem
        }

        Log($"Resultado final - Contador do servidor: {_counter}, Soma do vetor: {_soma}", LogLevel.Basic);
    }

    static bool ValidateArguments(string[] args, out string? errorMessage)
    {
        errorMessage = null;

        if (args.Length < 6 ||
            !int.TryParse(args[0], out _nClientes) || 
            !IPAddress.TryParse(args[1], out _ipAddress) ||
            !int.TryParse(args[2], out _port) ||
            !int.TryParse(args[3], out _nReads) ||
            !int.TryParse(args[4], out _nWrites) ||
            !Enum.TryParse(args[5], out _currentLogLevel))
        {
            errorMessage = "Uso: dotnet run -- <Numero de Clientes> <Server Ip> <Port> <Numero de Leituras> <Numero de Escritas> <Log Level> [RW|WR|Intercalado]";
            return false;
        }

        _sequence = args.Length >= 7 && ValidateSequence(args[6]) ? args[6] : "RW";
        return true;
    }

    static void StartClient(int pos)
    {
        try
        {
            using (TcpClient client = new TcpClient())
            {
                client.Connect(_ipAddress, _port);

                using (StreamReader inStream = new StreamReader(client.GetStream()))
                using (StreamWriter outStream = new StreamWriter(client.GetStream()) { AutoFlush = true })
                {
                    int totalRequests = _nReads + _nWrites;
                    outStream.WriteLine(totalRequests);

                    for (int i = 0; i < totalRequests; i++)
                    {
                        string operation = GetNextOperation(i);
                        outStream.WriteLine($"{operation} {pos}");

                        string? response = inStream.ReadLine();
                        Log($"Thread {Thread.CurrentThread.ManagedThreadId}: {operation} {pos} - Resposta do servidor: {response}", LogLevel.Info);

                        pos = (pos + 1) % 1000; // Incrementa a posição circularmente
                    }

                    string? responseCounter = inStream.ReadLine();
                    if (int.TryParse(responseCounter, out int parsedCounter))
                    {
                        _counter = parsedCounter; // Substitui o valor diretamente
                    }

                    string? responseSoma = inStream.ReadLine();
                    if (int.TryParse(responseSoma, out int parsedSoma))
                    {
                        _soma = parsedSoma; // Substitui o valor diretamente
                    }

                }
            }
        }
        catch (Exception e)
        {
            Log($"Erro na Thread {Thread.CurrentThread.ManagedThreadId}: {e.Message}", LogLevel.Basic);
        }
    }

    private static string GetNextOperation(int index)
    {
        return _sequence switch
        {
            "RW" => index < _nReads ? "READ" : "WRITE",
            "WR" => index < _nWrites ? "WRITE" : "READ",
            "Intercalado" => index % 2 == 0 ? "READ" : "WRITE",
            _ => "READ" // Padrão
        };
    }

    private static bool ValidateSequence(string sequence)
    {
        return sequence.Equals("RW", StringComparison.OrdinalIgnoreCase) ||
               sequence.Equals("WR", StringComparison.OrdinalIgnoreCase) ||
               sequence.Equals("Intercalado", StringComparison.OrdinalIgnoreCase);
    }
}
