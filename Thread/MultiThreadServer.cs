
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

using Server_Client;

public class MultiThreadedServer
{
    private static int[] _vetor;
    private static int _tamanhoVetor;
    private static int _port;
    private static int _counter;
    private static int _soma;
    private static object[] _lockObjects;
    private static bool _useLock;
    private static LogClass _logClass;
    
    // Exemplo de uso: dotnet run -- 1000 12345 3 true
    

    public static void Main(string[] args)
    {
        
        if (args.Length < 4 || 
            !int.TryParse(args[0], out int tamanhoVetor) || 
            !int.TryParse(args[1], out int port) || 
            !Enum.TryParse(args[2], out LogLevel currentLogLevel) || 
            !bool.TryParse(args[3], out bool useLock))
        {
            LogClass.log("Uso: Server <Tamanho do Vetor> <Port> <Log Level> <Usar Lock>", LogLevel.Basic);
            return;
        }

        _tamanhoVetor = tamanhoVetor;
        _port = port;
        _logClass = new LogClass(currentLogLevel);
        _useLock = useLock;
        _vetor = new int[_tamanhoVetor];
        _lockObjects = new object[_tamanhoVetor];

        for (int i = 0; i < _vetor.Length; i++)
        {
            _lockObjects[i] = new object();
        }

        TcpListener server = null;

        try
        {
            server = new TcpListener(IPAddress.Any, _port);
            server.Start();
            LogClass.log($"Servidor iniciado na porta {_port}", LogLevel.Basic);

            while (true)
            {
                Socket clientSocket = server.AcceptSocket();
                _counter++;
                LogClass.log("Cliente conectado: " + ((IPEndPoint)clientSocket.RemoteEndPoint!).Address, LogLevel.Info);
                
                Thread clientThread = new Thread(() => HandleClient(clientSocket));
                clientThread.Start();
            }
        }
        catch (IOException e)
        {
            Console.Error.WriteLine("Erro no servidor: " + e.Message);
        }
        finally
        {
            server?.Stop();
        }
        
    }

    private static void HandleClient(Socket clientSocket)
    {
        try
        {
            using (NetworkStream stream = new NetworkStream(clientSocket))
            using (StreamReader inStream = new StreamReader(stream))
            using (StreamWriter outStream = new StreamWriter(stream) { AutoFlush = true })
            {
                outStream.WriteLine(_tamanhoVetor);

                int numberOfRequests = int.Parse(inStream.ReadLine()!);

                for (int i = 0; i < numberOfRequests; i++)
                {
                    string mensagem = inStream.ReadLine();
                    if (mensagem == null) return;

                    string[] parts = mensagem.Split(' ');
                    string operation = parts[0];
                    int pos = int.Parse(parts[1]);

                    if (_useLock)
                    {
                        ProcessRequestWithLock(outStream, operation, pos);
                    }
                    else
                    {
                        ProcessRequestWithoutLock(outStream, operation, pos);
                    }
                }

                lock (_lockObjects)
                {
                    outStream.WriteLine(_counter.ToString());
                    outStream.WriteLine(_soma.ToString());
                }
            }
        }
        catch (IOException e)
        {
            LogClass.log($"Erro na comunicação com o cliente: {e.Message}", LogLevel.Basic);
        }
        finally
        {
            clientSocket.Close();
        }
    }


    private static void ProcessRequestWithLock(StreamWriter outStream, string operation, int pos)
    {
        if (pos >= 0 && pos < _vetor.Length)
        {
            lock (_lockObjects[pos])
            {
                if (operation.Equals("READ", StringComparison.OrdinalIgnoreCase))
                {
                    int value = _vetor[pos];
                    outStream.WriteLine($"READ {pos}: {value}");
                }
                else if (operation.Equals("WRITE", StringComparison.OrdinalIgnoreCase))
                {
                    _vetor[pos] += 1;
                    _soma = _vetor.Sum();
                    outStream.WriteLine($"WRITE {pos}: {_vetor[pos]}");
                }
            }
        }
        else
        {
            outStream.WriteLine("Erro: posição fora do limite.");
        }
    }

    private static void ProcessRequestWithoutLock(StreamWriter outStream, string operation, int pos)
    {
        if (pos >= 0 && pos < _vetor.Length)
        {
            if (operation.Equals("READ", StringComparison.OrdinalIgnoreCase))
            {
                int value = _vetor[pos];
                outStream.WriteLine($"READ {pos}: {value}");
            }
            else if (operation.Equals("WRITE", StringComparison.OrdinalIgnoreCase))
            {
                _vetor[pos] += 1;
                _soma = _vetor.Sum();
                outStream.WriteLine($"WRITE {pos}: {_vetor[pos]}");
            }
        }
        else
        {
            outStream.WriteLine("Erro: posição fora do limite.");
        }
    }
}
