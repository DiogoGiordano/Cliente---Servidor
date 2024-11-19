using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Linq;
using Server_Client;

public class MultiThreadedServer
{
    private static int[] _vetor;
    private static int _tamanhoVetor;
    private static int _port;
    private static int _soma;
    private static int _counter;
    private static object[] _lockObjects;
    private static bool _useLock;
    private static LogLevel _currentLogLevel;
    private static Teste _teste;

    public static void Main(string[] args)
    {
        if (args.Length < 3 || 
            !int.TryParse(args[0], out int tamanhoVetor) || 
            !int.TryParse(args[1], out int port) || 
            !Enum.TryParse(args[2], out LogLevel currentLogLevel) || 
            !bool.TryParse(args[3], out bool useLock))
        {
            Console.WriteLine("Uso: Server <Tamanho do Vetor> <Port> <Log Level> <Usar Lock>");
            return;
        }

        _tamanhoVetor = tamanhoVetor;
        _port = port;
        _teste = new Teste(currentLogLevel);
        _useLock = useLock;
        _vetor = new int[_tamanhoVetor];
        _lockObjects = new object[_tamanhoVetor];

        for (int i = 0; i < _vetor.Length; i++)
        {
            _lockObjects[i] = new object();
        }

        TcpListener? server = null;

        try
        {
            server = new TcpListener(IPAddress.Any, _port);
            server.Start();
            Teste.log("Servidor iniciado na porta " + _port, LogLevel.Basic);
            Teste.log("Servidor iniciado na porta " + _port, LogLevel.Info);

            _counter++;
            while (true)
            {
                Socket clientSocket = server.AcceptSocket();
                Teste.log("Cliente conectado: " + ((IPEndPoint)clientSocket.RemoteEndPoint!)?.Address,
                    LogLevel.Info);
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

                // Envia os valores finais (_counter e _soma) para o cliente.
                outStream.WriteLine(_counter.ToString());
                outStream.WriteLine(_soma.ToString());
            }
        }
        catch (IOException e)
        {
            Teste.log("Erro na comunicação com o cliente: " + e.Message, LogLevel.Basic);
        }
        finally
        {
            clientSocket.Close();  // Fechamento do socket do cliente
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
                    // Incrementa o valor no vetor.
                    _vetor[pos] += 1;

                    // Calcula a soma total do vetor
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
                // Incrementa o valor no vetor.
                _vetor[pos] += 1;

                // Calcula a soma total do vetor
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