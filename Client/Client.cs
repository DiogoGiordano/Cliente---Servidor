using System.Net.Sockets;
using System.IO;
using System;
using System.Net;
using Server_Client;

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
    private static Teste _teste;
    private static int _port;
    private static IPAddress? _ipAddress;
    private static int _nRequisitos;
    private static int _counter = 0;
    private static int _soma = 0;

    public static void Log(string message, LogLevel level)
    {
        if ((_currentLogLevel & level) == level)
        {
            Console.WriteLine(message);
        }
    }

//dotnet run 100 127.0.0.1 12345 100 2

    static void Main(string[] args)
    {
        //verificar a ordem dos argumentos
        if (args.Length < 5 ||
            !int.TryParse(args[0], out int nClients) || 
            !IPAddress.TryParse(args[1], out IPAddress? ipAddress) ||
            !int.TryParse(args[2], out int port) || !Validacao.PortaValida(port) ||
            !int.TryParse(args[3], out int nRequisicoes) ||
            !Enum.TryParse(args[4], out LogLevel currentLogLevel)) 
        {
            Console.WriteLine("Uso: Client <Numero de Clientes> <Server Ip> <Port> <Numero de Requisicoes> <Log Level>");
            return;
        }

        _nClientes = nClients;
        _ipAddress = ipAddress;
        _port = port;
        _nRequisitos = nRequisicoes;
        _currentLogLevel = currentLogLevel;


        Thread[] clients = new Thread[_nClientes];
        Random random = new Random();
        
        for (int i = 0; i < _nClientes; i++)
        {
            int pos = random.Next(0, 100);
            clients[i] = new Thread(() => StartClient(pos));
            clients[i].Start();
        }

        foreach (var clientThread in clients)
        {
            clientThread.Join();
        }
        
        Log($"Contador do servidor: {_counter}", LogLevel.Basic);
        Log($"Soma de todos os valores do vetor: {_soma}", LogLevel.Basic);
        Log($"Contador do servidor: {_counter}", LogLevel.Info);
        Log($"Soma de todos os valores do vetor: {_soma}", LogLevel.Info);
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
                    outStream.WriteLine(_nRequisitos);

                    for (int i = 0; i < _nRequisitos; i++)
                    {
                        outStream.WriteLine(pos);
                        string? response = inStream.ReadLine();
                        Log($"Resposta do servidor: {response}", LogLevel.Info);
                    }
                    
                    string? responseCounter = inStream.ReadLine();
                    _counter = responseCounter == null ? 0 : int.Parse(responseCounter);
                    string? responseSoma = inStream.ReadLine();
                    _soma = responseSoma == null ? 0 : int.Parse(responseSoma);
                }
            }
        }
        catch (Exception e)
        {
            Log($"Erro no cliente: {e.Message}", LogLevel.Basic);
        }
    }
}