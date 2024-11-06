using System.Net;
using System.Net.Sockets;


[Flags]
public enum LogLevel
{
    None = 0,
    Info = 1,
    Error = 2
}

public class MultiThreadedServer
{
    public static LogLevel CurrentLogLevel = LogLevel.Info | LogLevel.Error;

    public static void Log(string message, LogLevel level)
    {
        // Não exibe mensagens com o nível "None"
        if (level == LogLevel.None)
        {
            return;
        }

        // Exibe as mensagens dos outros níveis de log
        if ((CurrentLogLevel & level) == level)
        {
            Console.WriteLine(message);
        }
    }

    private static int counter = 0; // Contador de requisições
    private static readonly object lockObject = new object(); // Objeto para sincronizaçãows

    public static void Main(string[] args)
    {
        int port = 12345;

        TcpListener server = null;

        try
        {
            server = new TcpListener(IPAddress.Any, port);
            server.Start();
            Log("Servidor iniciado na porta " + port, LogLevel.Info);

            while (true)
            {
                TcpClient clientSocket = server.AcceptTcpClient();
                Log("Cliente conectado: " + ((IPEndPoint)clientSocket.Client.RemoteEndPoint).Address, LogLevel.Info);
                Task.Run(() => HandleClient(clientSocket)); // Lida com o cliente em uma nova tarefa
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

    private static void HandleClient(TcpClient clientSocket)
    {
        try
        {
            using (StreamReader inStream = new StreamReader(clientSocket.GetStream()))
            using (StreamWriter outStream = new StreamWriter(clientSocket.GetStream()) { AutoFlush = true })
            {
                int numberOfRequests = int.Parse(inStream.ReadLine());

                for (int i = 0; i < numberOfRequests; i++)
                {
                    int currentValue;

                    lock (lockObject) // Sincroniza o acesso ao contador
                    {
                        currentValue = ++counter; // Incrementa o contador
                    }

                    outStream.WriteLine(currentValue); // Envia o valor atual ao cliente
                    inStream.ReadLine(); // Aguarda a leitura do cliente
                }
            }
        }
        catch (IOException e)
        {
            Console.Error.WriteLine("Erro na comunicação com o cliente: " + e.Message);
        }
        finally
        {
            clientSocket.Close(); // Fecha o socket do cliente
        }
    }
}