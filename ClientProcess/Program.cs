using System.Net.Sockets;
using System.IO;
using System;
using System.Diagnostics;
using System.Net;
using System.Threading;

[Flags]
public enum LogLevel
{
    None = 0,
    Basic = 1,
    Info = 2,
}

class Client
{
    private static int _nClientes;
    private static LogLevel _currentLogLevel;
    private static int _port;
    private static IPAddress? _ipAddress;
    private static int _nReads;
    private static int _nWrites;
    private static string _sequence;
    private static int _counter = 0;

    public static void Log(string message, LogLevel level)
    {
        if ((_currentLogLevel & level) == level)
        {
            Console.WriteLine(message);
        }
    }
    
    
    
    // Exemplo de uso: dotnet run 10 127.0.0.1 12345 5 5 Info RW
    static void Main(string[] args)
    {
        
        if (args.Length < 6 ||
            !int.TryParse(args[0], out int nClients) ||
            !IPAddress.TryParse(args[1], out IPAddress? ipAddress) ||
            !int.TryParse(args[2], out int port) || !Validacao.PortaValida(port) ||
            !int.TryParse(args[3], out int nReads) ||
            !int.TryParse(args[4], out int nWrites) ||
            !Enum.TryParse(args[5], out LogLevel currentLogLevel) ||
            (args.Length >= 7 && !ValidateSequence(args[6])))
        {
            Console.WriteLine("Uso: Client <Numero de Clientes> <Server Ip> <Porta> <Numero de Reads> <Numero de Writes> <Log Level> <RW/WR/Intercalado>");
            return;
        }

        _nClientes = nClients;
        _ipAddress = ipAddress;
        _port = port;
        _nReads = nReads;
        _nWrites = nWrites;
        _currentLogLevel = currentLogLevel;
        _sequence = args.Length >= 7 ? args[6] : "RW";

        Thread[] clients = new Thread[_nClientes];
        Random random = new Random();

        // Variável para armazenar a última resposta de cada cliente
        string lastResponse = string.Empty;

        for (int i = 0; i < _nClientes; i++)
        {
            int pos = random.Next(0, 1000); // Escolher uma posição aleatória
            clients[i] = new Thread(() => StartClient(pos, ref lastResponse));
            clients[i].Start();
        }

        foreach (var clientThread in clients)
        {
            clientThread.Join();
        }

        // Exibir a última resposta de contador e soma
        Log(lastResponse, LogLevel.Basic);
    }

    static void StartClient(int pos, ref string lastResponse)
    {
        try
        {
            using (TcpClient client = new TcpClient())
            {
                client.Connect(_ipAddress, _port);

                using (StreamReader inStream = new StreamReader(client.GetStream()))
                using (StreamWriter outStream = new StreamWriter(client.GetStream()) { AutoFlush = true })
                {
                    int totalOperations = _nReads + _nWrites;
                    outStream.WriteLine(totalOperations); // Enviar número total de operações

                    for (int i = 0; i < totalOperations; i++)
                    {
                        string operation = GetNextOperation(i);
                        outStream.WriteLine($"{operation} {pos}");

                        string? response = inStream.ReadLine(); // Receber resposta do servidor
                        Log($"Resposta do servidor: {response}", LogLevel.Info);

                        pos = (pos + 1) % 1000; // Incrementa posição para próximas operações
                    }

                    // No final das operações, receber o contador e soma final
                    string? responseCounter = inStream.ReadLine();  // Receber contador
                    string? responseSoma = inStream.ReadLine();     // Receber soma final

                    // Sobrescrever a resposta anterior com a última resposta
                    lastResponse = $"Contador do servidor (final): {responseCounter}\nSoma final do vetor: {responseSoma}";
                }
            }
        }
        catch (Exception e)
        {
            Log($"Erro no cliente: {e.Message}", LogLevel.Basic);
        }
    }

    private static string GetNextOperation(int index)
    {
        if (_sequence.Equals("RW", StringComparison.OrdinalIgnoreCase))
        {
            return index < _nReads ? "READ" : "WRITE";
        }
        else if (_sequence.Equals("WR", StringComparison.OrdinalIgnoreCase))
        {
            return index < _nWrites ? "WRITE" : "READ";
        }
        else
        {
            return index % 2 == 0 ? "READ" : "WRITE";
        }
    }

    private static bool ValidateSequence(string sequence)
    {
        return sequence.Equals("RW", StringComparison.OrdinalIgnoreCase) ||
               sequence.Equals("WR", StringComparison.OrdinalIgnoreCase) ||
               sequence.Equals("Intercalado", StringComparison.OrdinalIgnoreCase);
    }
}

public static class Validacao
{
    public static bool PortaValida(int porta)
    {
        return porta > 1024 && porta <= 65535;
    }
}
