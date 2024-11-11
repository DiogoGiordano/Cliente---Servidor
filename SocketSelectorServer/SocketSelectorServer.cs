using System.Net;
using System.Net.Sockets;
using System.Text;

[Flags]
public enum LogLevel
{
    None = 0,
    Info = 1,
    Error = 2
}

public class SocketSelectorServer
{
    public static LogLevel CurrentLogLevel = LogLevel.Info | LogLevel.Error;

    public static void Log(string message, LogLevel level)
    {
        if (level == LogLevel.None)
        {
            return;
        }

        if ((CurrentLogLevel & level) == level)
        {
            Console.WriteLine(message);
        }
    }

    private static int counter = 0;
    private static readonly object lockObject = new object();

    public static void Main(string[] args)
    {
        int port = 12345;

        TcpListener server = new TcpListener(IPAddress.Any, port);
        server.Start();
        Log("Servidor iniciado na porta " + port, LogLevel.Info);

        List<Socket> clientSockets = new List<Socket>();

        try
        {
            while (true)
            {
                // Aceita novas conexões de clientes
                if (server.Pending())
                {
                    Socket clientSocket = server.AcceptSocket();
                    clientSockets.Add(clientSocket);
                    Log("Novo cliente conectado: " + ((IPEndPoint)clientSocket.RemoteEndPoint).Address, LogLevel.Info);
                }

                // Apenas executa a seleção se houver clientes conectados
                if (clientSockets.Count > 0)
                {
                    List<Socket> readSockets = new List<Socket>(clientSockets);
                    List<Socket> writeSockets = new List<Socket>(clientSockets);

                    // Seleciona os sockets prontos
                    Socket.Select(readSockets, writeSockets, null, 1000);

                    foreach (var socket in readSockets)
                    {
                        if (!IsSocketConnected(socket))
                        {
                            Log("Cliente desconectado: " + ((IPEndPoint)socket.RemoteEndPoint).Address, LogLevel.Info);
                            socket.Close();
                            clientSockets.Remove(socket); // Remove o socket desconectado
                            continue;
                        }

                        HandleClient(socket);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Log("Erro no servidor: " + e.Message, LogLevel.Error);
        }
        finally
        {
            foreach (var socket in clientSockets)
            {
                socket.Close();
            }
            server.Stop();
        }
    }

    private static void HandleClient(Socket clientSocket)
    {
        try
        {
            using (NetworkStream stream = new NetworkStream(clientSocket))
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
            {
                string requestLine = reader.ReadLine();
                if (int.TryParse(requestLine, out int numberOfRequests))
                {
                    for (int i = 0; i < numberOfRequests; i++)
                    {
                        int currentValue;
                        lock (lockObject)
                        {
                            currentValue = ++counter;
                        }

                        writer.WriteLine(currentValue); // Envia o valor ao cliente
                        reader.ReadLine(); // Aguarda confirmação do cliente
                    }
                }
            }
        }
        catch (IOException e)
        {
            Log("Erro na comunicação com o cliente: " + e.Message, LogLevel.Error);
            clientSocket.Close(); // Fecha o socket do cliente em caso de erro
        }
    }

    private static bool IsSocketConnected(Socket socket)
    {
        // Verifica se o socket ainda está conectado
        return !(socket.Poll(1, SelectMode.SelectRead) && socket.Available == 0);
    }
}
