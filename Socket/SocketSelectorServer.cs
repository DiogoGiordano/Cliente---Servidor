
using System.Net;
using System.Net.Sockets;
using System.Text;
using Server_Client;

public class SocketSelectorServer
{
    private static int[] _vetor;
    private static int _tamanhoVetor;
    private static int _port;
    private static int _counter = 0;
    private static int _soma = 0;
    private static Socket _serverSocket;
    private static List<Socket> _clientSockets = new List<Socket>();
    private static object _lock = new object();

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
        _vetor = new int[_tamanhoVetor];

        try
        {
            _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _serverSocket.Bind(new IPEndPoint(IPAddress.Any, _port));
            _serverSocket.Listen(10);

            Console.WriteLine($"Servidor iniciado na porta {_port}.");

            while (true)
            {
                List<Socket> readSockets = new List<Socket>(_clientSockets) { _serverSocket };
                List<Socket> errorSockets = new List<Socket>(_clientSockets);

                Socket.Select(readSockets, null, errorSockets, 1000);

                foreach (var socket in errorSockets)
                {
                    Console.WriteLine("Cliente desconectado devido a erro.");
                    _clientSockets.Remove(socket);
                    socket.Close();
                }

                foreach (var socket in readSockets)
                {
                    if (socket == _serverSocket)
                    {
                        var clientSocket = _serverSocket.Accept();
                        _clientSockets.Add(clientSocket);
                        Console.WriteLine("Novo cliente conectado.");
                    }
                    else
                    {
                        HandleClient(socket);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Erro no servidor: {e.Message}");
        }
        finally
        {
            _serverSocket?.Close();
        }
    }

    private static void HandleClient(Socket clientSocket)
    {
        try
        {
            byte[] buffer = new byte[1024];
            int bytesRead = clientSocket.Receive(buffer);

            if (bytesRead == 0)
            {
                _clientSockets.Remove(clientSocket);
                clientSocket.Close();
                return;
            }

            string request = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
            string response;

            if (request.Equals("INIT", StringComparison.OrdinalIgnoreCase))
            {
                response = _tamanhoVetor.ToString();
            }
            else if (request.StartsWith("REQCOUNT"))
            {
                _counter = int.Parse(request.Split(' ')[1]);
                response = "ACK";
            }
            else if (request.StartsWith("READ") || request.StartsWith("WRITE"))
            {
                string[] parts = request.Split(' ');
                if (parts.Length < 2 || !int.TryParse(parts[1], out int pos) || pos < 0 || pos >= _vetor.Length)
                {
                    response = "Erro: posição inválida.";
                }
                else
                {
                    lock (_lock)
                    {
                        if (request.StartsWith("READ"))
                        {
                            response = $"READ {pos}: {_vetor[pos]}";
                        }
                        else if (request.StartsWith("WRITE"))
                        {
                            _vetor[pos]++;
                            _soma = _vetor.Sum();
                            response = $"WRITE {pos}: {_vetor[pos]}";
                        }
                        else
                        {
                            response = "Erro: operação desconhecida.";
                        }
                    }
                }
            }
            else if (request.Equals("RESULT", StringComparison.OrdinalIgnoreCase))
            {
                lock (_lock)
                {
                    response = $"{_counter}\n{_soma}";
                }
            }
            else
            {
                response = "Erro: comando inválido.";
            }

            clientSocket.Send(Encoding.UTF8.GetBytes(response + "\n"));
        }
        catch (Exception e)
        {
            Console.WriteLine($"Erro ao processar cliente: {e.Message}");
            _clientSockets.Remove(clientSocket);
            clientSocket.Close();
        }
    }
}
