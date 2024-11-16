using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Threading.Tasks;
using Server_Client;

public class SocketSelectorServer
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
        
        TcpListener server = new TcpListener(IPAddress.Any, port);
        server.Start();
        Teste.log("Servidor iniciado na porta " + port, Server_Client.LogLevel.Basic);

        try
        {
            while (true)
            {
                TcpClient clientSocket = server.AcceptTcpClient();
                Teste.log("Cliente conectado: " + ((IPEndPoint)clientSocket.Client.RemoteEndPoint).Address, Server_Client.LogLevel.Info);
                Task.Run(() => HandleClient(clientSocket));
            }
        }
        catch (Exception e)
        {
            Teste.log("Erro no servidor: " + e.Message, Server_Client.LogLevel.Basic);
        }
        finally
        {
            server.Stop();
        }
    }

    private static void HandleClient(TcpClient clientSocket)
    {
        try
        {
            using (NetworkStream stream = clientSocket.GetStream())
            using (StreamReader reader = new StreamReader(stream))
            using (StreamWriter writer = new StreamWriter(stream) { AutoFlush = true })
            {
                int numberOfRequests = int.Parse(reader.ReadLine());
                if (_useLock)
                {
                    for (int i = 0; i < numberOfRequests; i++)
                    {
                        string mensagem = reader.ReadLine();
                        if (mensagem == null) return;

                        int pos = int.Parse(mensagem);

                        if (pos >= 0 && pos < _vetor.Length)
                        {
                            lock (_lockObjects[pos])
                            {
                                _vetor[pos]++;
                                writer.WriteLine(_counter);
                            }
                        }
                        else
                        {
                            writer.WriteLine("Erro: posição fora do limite.");
                        }
                    }
                    _counter++;
                }
                else
                {
                    ProcessRequests(reader, writer, numberOfRequests);
                }

                _soma = _vetor.Sum();
                writer.WriteLine(_counter);
                writer.WriteLine(_soma);
            }
        }
        catch (IOException e)
        {
            Teste.log("Erro na comunicação com o cliente: ", Server_Client.LogLevel.Basic);
        }
    }

    private static void ProcessRequests(StreamReader reader, StreamWriter writer, int numberOfRequests)
    {
        for (int i = 0; i < numberOfRequests; i++)
        {
            string mensagem = reader.ReadLine();
            if (mensagem == null) return;

            int pos = int.Parse(mensagem);

            if (pos >= 0 && pos < _vetor.Length)
            {
                {
                    _vetor[pos]++;
                    writer.WriteLine(_counter);
                }
            }
            else
            {
                writer.WriteLine("Erro: posição fora do limite.");
            }
        }
        _counter++;
    }
}