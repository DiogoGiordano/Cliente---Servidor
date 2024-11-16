using System.Net;
using System.Net.Sockets;
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
        if (args.Length < 3 || !int.TryParse(args[0], out int tamanhoVetor) || !int.TryParse(args[1], out int port) || !Enum.TryParse(args[2], out Server_Client.LogLevel currentLogLevel) || !bool.TryParse(args[3], out bool useLock))
        {
            Console.WriteLine("Uso: Client <Tamanho do Vetor> <Port> <Log Level> <Usar Lock>");
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
            Teste.log("Servidor iniciado na porta " + _port, Server_Client.LogLevel.Basic);
            Teste.log("Servidor iniciado na porta " + _port, Server_Client.LogLevel.Info);

            _counter++;
            while (true)
            {
                TcpClient clientSocket = server.AcceptTcpClient();
                Teste.log("Cliente conectado: " + ((IPEndPoint)clientSocket.Client.RemoteEndPoint!)?.Address,
                    Server_Client.LogLevel.Info);
                Task.Run(() => HandleClient(clientSocket));
            }
        }
        catch (IOException e)
        {
            Console.Error.WriteLine("Erro no servidor: " + e.Message);
        }
        finally
        {
            Console.WriteLine("finally");
            server?.Stop();
        }
    }

    private static void HandleClient(TcpClient clientSocket)
    {
        try
        {
            using (NetworkStream stream = clientSocket.GetStream())
            using (StreamReader inStream = new StreamReader(stream))
            using (StreamWriter outStream = new StreamWriter(stream) { AutoFlush = true })
            {
                int numberOfRequests = int.Parse(inStream.ReadLine()!);

                if (_useLock)
                {
                    for (int i = 0; i < numberOfRequests; i++)
                    {
                        string mensagem = inStream.ReadLine();
                        if (mensagem == null) return;

                        int pos = int.Parse(mensagem);

                        if (pos >= 0 && pos < _vetor.Length)
                        {
                            lock (_lockObjects[pos])
                            {
                                _vetor[pos] = _vetor[pos] + 1;
                                outStream.WriteLine($"Posição {pos} atualizada com o valor {_vetor[pos]}");
                            }
                        }
                        else
                        {
                            outStream.WriteLine("Erro: posição fora do limite.");
                        }
                    }

                    _counter++;
                }
                else
                {
                    ProcessRequests(inStream, outStream, numberOfRequests);
                }

                _soma = _vetor.Sum();
                outStream.WriteLine(_counter.ToString());
                outStream.WriteLine(_soma.ToString());
            }
        }
        catch (IOException e)
        {
            Teste.log("Erro na comunicação com o cliente: " + e.Message, Server_Client.LogLevel.Basic);
        }
        finally
        {
            clientSocket.Close();
        }
    }

    private static void ProcessRequests(StreamReader inStream, StreamWriter outStream, int numberOfRequests)
    {
        for (int i = 0; i < numberOfRequests; i++)
        {
            string mensagem = inStream.ReadLine();
            if (mensagem == null) return;

            int pos = int.Parse(mensagem);

            if (pos >= 0 && pos < _vetor.Length)
            {
                _vetor[pos] = _vetor[pos] + 1;
                outStream.WriteLine($"Posição {pos} atualizada com o valor {_vetor[pos]}");
            }
            else
            {
                outStream.WriteLine("Erro: posição fora do limite.");
            }
        }

        _counter++;
    }
}