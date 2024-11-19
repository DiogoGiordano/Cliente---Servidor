using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Collections.Generic;
using Server_Client;

public class SocketSelectorServer
{
    private static int[] _vetor;
    private static int _tamanhoVetor;
    private static int _port;
    private static int _counter;
    private static int _soma;
    private static object[] _lockObjects;
    private static bool _useLock;
    private static Teste _teste;
    private static Socket _serverSocket;

    public static void Main(string[] args)
    {
        if (args.Length < 4 ||
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

        try
        {
            _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _serverSocket.Bind(new IPEndPoint(IPAddress.Any, _port));
            _serverSocket.Listen(10);

            Teste.log($"Servidor iniciado na porta {_port}", LogLevel.Basic);

            while (true)
            {
                Socket clientSocket = _serverSocket.Accept();
                Teste.log("Novo cliente conectado.", LogLevel.Info);
                ThreadPool.QueueUserWorkItem(HandleClient, clientSocket);
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("Erro no servidor: " + e.Message);
        }
        finally
        {
            _serverSocket?.Close();
        }
    }

    private static void HandleClient(object? clientObj)
    {
        if (clientObj is not Socket clientSocket)
            return;

        try
        {
            using (NetworkStream networkStream = new NetworkStream(clientSocket))
            using (StreamReader reader = new StreamReader(networkStream))
            using (StreamWriter writer = new StreamWriter(networkStream) { AutoFlush = true })
            {
                writer.WriteLine(_tamanhoVetor); // Envia o tamanho do vetor

                string? requestCountStr = reader.ReadLine();
                if (!int.TryParse(requestCountStr, out int requestCount))
                {
                    Teste.log("Número de requisições inválido.", LogLevel.Basic);
                    return;
                }

                for (int i = 0; i < requestCount; i++)
                {
                    string? request = reader.ReadLine();
                    if (string.IsNullOrEmpty(request))
                    {
                        Teste.log("Requisição inválida recebida.", LogLevel.Basic);
                        return;
                    }

                    string[] parts = request.Split(' ');
                    if (parts.Length < 2 || !int.TryParse(parts[1], out int pos))
                    {
                        writer.WriteLine("Erro: formato da requisição inválido.");
                        continue;
                    }

                    string operation = parts[0];

                    if (_useLock)
                    {
                        ProcessRequestWithLock(writer, operation, pos);
                    }
                    else
                    {
                        ProcessRequestWithoutLock(writer, operation, pos);
                    }
                }

                // Após processar todas as requisições, envia o contador e a soma
                lock (_lockObjects)
                {
                    writer.WriteLine(_counter.ToString());
                    writer.WriteLine(_soma.ToString());
                }
            }
        }
        catch (Exception e)
        {
            Teste.log($"Erro na comunicação com o cliente: {e.Message}", LogLevel.Basic);
        }
        finally
        {
            clientSocket.Close();
        }
    }

    private static void ProcessRequestWithLock(StreamWriter writer, string operation, int pos)
    {
        if (pos >= 0 && pos < _vetor.Length)
        {
            lock (_lockObjects[pos])
            {
                ExecuteOperation(writer, operation, pos);
            }
        }
        else
        {
            writer.WriteLine("Erro: posição fora do limite.");
        }
    }

    private static void ProcessRequestWithoutLock(StreamWriter writer, string operation, int pos)
    {
        if (pos >= 0 && pos < _vetor.Length)
        {
            ExecuteOperation(writer, operation, pos);
        }
        else
        {
            writer.WriteLine("Erro: posição fora do limite.");
        }
    }

    private static void ExecuteOperation(StreamWriter writer, string operation, int pos)
    {
        if (operation.Equals("READ", StringComparison.OrdinalIgnoreCase))
        {
            int value = _vetor[pos];
            writer.WriteLine($"READ {pos}: {value}");
        }
        else if (operation.Equals("WRITE", StringComparison.OrdinalIgnoreCase))
        {
            _vetor[pos] += 1;
            _counter++;
            _soma = _vetor.Sum();
            writer.WriteLine($"WRITE {pos}: {_vetor[pos]}");
        }
    }
}
