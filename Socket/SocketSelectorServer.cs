using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Linq;
using System.Collections.Generic;

class SocketServer
{
    private static int[] _vetor;
    private static int _tamanhoVetor;
    private static int _port;
    private static int _counter;
    private static int _soma;
    private static object[] _lockObjects;
    private static bool _useLock;
    private static Socket _serverSocket;
    private static List<Socket> _clientSockets = new List<Socket>(); // Lista de sockets conectados
    private static List<Socket> _readSockets = new List<Socket>(); // Lista de sockets prontos para leitura

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

            Log("Servidor iniciado na porta " + _port, LogLevel.Basic);

            while (true)
            {
                // Preparando as listas para o Socket.Select
                _readSockets.Clear();
                _readSockets.Add(_serverSocket); // Servidor esperando por novas conexões
                _readSockets.AddRange(_clientSockets); // Clientes existentes

                // Seleciona os sockets que estão prontos para leitura (conexões novas ou dados recebidos)
                Socket.Select(_readSockets, null, null, TimeSpan.FromMilliseconds(100));

                foreach (Socket socket in _readSockets)
                {
                    if (socket == _serverSocket) // Aceitar nova conexão
                    {
                        Socket clientSocket = _serverSocket.Accept();
                        _clientSockets.Add(clientSocket);
                        Log("Novo cliente conectado", LogLevel.Info);
                    }
                    else // Processar dados de clientes existentes
                    {
                        bool clientStillConnected = HandleClient(socket);
                        if (!clientStillConnected)
                        {
                            _clientSockets.Remove(socket);
                            socket.Close();
                            Log("Cliente desconectado", LogLevel.Info);
                        }
                    }
                }
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

    private static bool HandleClient(Socket clientSocket)
    {
        try
        {
            using (NetworkStream networkStream = new NetworkStream(clientSocket))
            using (StreamReader reader = new StreamReader(networkStream))
            using (StreamWriter writer = new StreamWriter(networkStream) { AutoFlush = true })
            {
                // Enviar o tamanho do vetor ao cliente
                writer.WriteLine(_vetor.Length);

                // Ler o número de requisições do cliente
                string? requestCountStr = reader.ReadLine();
                if (!int.TryParse(requestCountStr, out int requestCount))
                {
                    Log("Número de requisições inválido.", LogLevel.Basic);
                    return false;
                }

                // Processar cada requisição enviada pelo cliente
                for (int i = 0; i < requestCount; i++)
                {
                    string? request = reader.ReadLine();
                    if (string.IsNullOrEmpty(request))
                    {
                        Log("Requisição inválida recebida.", LogLevel.Basic);
                        return false;
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

                // Após processar todas as requisições, enviar contador e soma
                writer.WriteLine(_counter.ToString());
                writer.WriteLine(_soma.ToString());
            }
        }
        catch (IOException ioEx)
        {
            // Cliente fechou a conexão ou houve erro de rede
            Log($"Erro de IO: {ioEx.Message}", LogLevel.Basic);
            return false;
        }
        catch (SocketException socketEx)
        {
            Log($"Erro no socket: {socketEx.Message}", LogLevel.Basic);
            return false;
        }
        catch (Exception ex)
        {
            Log($"Erro inesperado no cliente: {ex.Message}", LogLevel.Basic);
            return false;
        }
        return true;
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

    private static void Log(string message, LogLevel level)
    {
        Console.WriteLine($"[{level}] {message}");
    }
}

enum LogLevel
{
    Basic = 1,
    Info = 2,
    Error = 4
}
