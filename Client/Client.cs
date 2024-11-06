using System.Net.Sockets;

[Flags]
public enum LogLevel
{
    None = 0,
    Info = 1,
    Error = 2
}

class Client
{
    public static LogLevel CurrentLogLevel = LogLevel.Info;
    
    public static void Log(string message, LogLevel level)
    {
        
        // Exibe as mensagens dos outros níveis de log
        if ((CurrentLogLevel & level) == LogLevel.Info || level == LogLevel.Error)
        {
            Console.WriteLine(message);
        }
    }


    static void Main(string[] args)
    {

        // Verifica se um argumento foi passado
        if (args.Length < 1 || !int.TryParse(args[0], out int numberOfClients))
        {
            Log("Uso: Client <numero_de_clientes>", LogLevel.Error);
            return;
        }

        // Cria um array de threads
        Thread[] clients = new Thread[numberOfClients];

        for (int i = 0; i < numberOfClients; i++)
        {
            clients[i] = new Thread(() => StartClient()); // Inicia uma nova thread para cada cliente
            clients[i].Start();
        }

        // Aguarda todas as threads terminarem
        foreach (var clientThread in clients)
        {
            clientThread.Join();
        }

        Log("Todos os clientes emulados terminaram.", LogLevel.Info);
    }

    static void StartClient()
    {
        CurrentLogLevel &= ~LogLevel.None; // Desativa mensagens informativas
        string serverIp = "127.0.0.1"; // IP do servidor
        int port = 12345; // Porta do servidor

        try
        {
            using (TcpClient client = new TcpClient(serverIp, port))
            using (StreamReader inStream = new StreamReader(client.GetStream()))
            using (StreamWriter outStream = new StreamWriter(client.GetStream()) { AutoFlush = true })
            {
                // Envia o número de requisições que o cliente faz
                int numberOfRequests = 1;
                outStream.WriteLine(numberOfRequests); // Envia o número de requisições

                for (int i = 0; i < numberOfRequests; i++)
                {
                    string? response = inStream.ReadLine();
                    Log($"Cliente {Thread.CurrentThread.ManagedThreadId}: Resposta do servidor: {response}",
                        LogLevel.None);
                    outStream.WriteLine(""); // Envia uma linha em branco para continuar o ciclo
                }
            }
        }
        catch (Exception e)
        {
            Log($"Erro no cliente {Thread.CurrentThread.ManagedThreadId}: {e.Message}", LogLevel.Error);
        }
    }
}