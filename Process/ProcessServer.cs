using System.Net;
using System.Net.Sockets;

public class ProcessServer
{
    

    
    public static int[] vetor = new int[1000];
    public static int soma = 0;
    public static LogLevel CurrentLogLevel = LogLevel.Info | LogLevel.Basic;

    public static bool useLock = false;
    public static SemaphoreSlim processPool; // Pool de processos
    private static readonly Mutex[] locks = new Mutex[vetor.Length]; // Mutex para cada posição do vetor

    private static int counter = 0;

    // Construtor estático para inicializar os Mutexes
    static ProcessServer()
    {
        // Inicializa os mutexes para cada posição
        for (int i = 0; i < vetor.Length; i++)
        {
            locks[i] = new Mutex();
        }
    }

    public static void Log(string message, LogLevel level)
    {
        if (level == LogLevel.Basic || level == LogLevel.Info || level == LogLevel.None)
        {
            Console.WriteLine(message);
        }
    }

    public static void Main(string[] args)
    {
        int port = 12345;
        int maxConcurrentProcesses = 5; // Número máximo de processos simultâneos

        Console.WriteLine("Deseja rodar o servidor com lock? (s/n): ");
        string userInput = Console.ReadLine()?.ToLower();
        useLock = userInput == "s" || userInput == "sim";

        processPool = new SemaphoreSlim(maxConcurrentProcesses); // Inicializa o pool de processos

        TcpListener? server = null;

        try
        {
            server = new TcpListener(IPAddress.Loopback, port); 
            server.Start();
            Log("Servidor iniciado na porta " + port, LogLevel.Info);
            Log("Lock " + (useLock ? "habilitado" : "desabilitado"), LogLevel.Info);

            while (true)
            {
                TcpClient clientSocket = server.AcceptTcpClient();
                Log("Cliente conectado: " + ((IPEndPoint)clientSocket.Client.RemoteEndPoint!)?.Address, LogLevel.Info);

                // Inicia o processo com controle de acesso ao pool
                StartClientProcess(clientSocket);
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

    private static void StartClientProcess(TcpClient clientSocket)
    {
        try
        {
            // Aguardar até que um processo esteja disponível
            processPool.Wait();

            // Processa o cliente diretamente sem usar um novo processo
            HandleClientWithProcess(clientSocket);
        }
        catch (Exception e)
        {
            Log($"Erro ao iniciar o processo para o cliente: {e.Message}", LogLevel.Basic);
        }
    }

    private static void HandleClientWithProcess(TcpClient clientSocket)
    {
        try
        {
            using (StreamReader inStream = new StreamReader(clientSocket.GetStream()))
            using (StreamWriter outStream = new StreamWriter(clientSocket.GetStream()) { AutoFlush = true })
            {
                int numberOfRequests = int.Parse(inStream.ReadLine()!);

                // Se usar lock, realiza a operação com exclusão mútua apenas nas seções críticas
                if (useLock)
                {
                    ProcessRequestsWithFineGrainedLock(inStream, outStream, numberOfRequests);
                }
                else
                {
                    ProcessRequests(inStream, outStream, numberOfRequests);
                }

                soma = vetor.Sum();
                outStream.WriteLine(counter.ToString());
                outStream.WriteLine(soma.ToString());
            }
        }
        catch (IOException e)
        {
            Log("Erro na comunicação com o cliente: " + e.Message, LogLevel.Basic);
        }
        finally
        {
            // Libera o processo para outro cliente
            processPool.Release();
            clientSocket.Close();
        }
    }

    private static void ProcessRequestsWithFineGrainedLock(StreamReader inStream, StreamWriter outStream, int numberOfRequests)
    {
        for (int i = 0; i < numberOfRequests; i++)
        {
            string mensagem = inStream.ReadLine();
            if (mensagem == null) return;

            int pos = int.Parse(mensagem);

            // Se a posição for válida, use o Mutex específico para aquela posição
            if (pos >= 0 && pos < vetor.Length)
            {
                locks[pos].WaitOne();  // Aguarda o lock para a posição
                try
                {
                    vetor[pos] = vetor[pos] + 1;
                    counter++;
                    outStream.WriteLine($"Posição {pos} atualizada com o valor {vetor[pos]}");
                }
                finally
                {
                    locks[pos].ReleaseMutex();  // Libera o lock da posição
                }
            }
            else
            {
                outStream.WriteLine("Erro: posição fora do limite.");
            }
        }
    }

    private static void ProcessRequests(StreamReader inStream, StreamWriter outStream, int numberOfRequests)
    {
        for (int i = 0; i < numberOfRequests; i++)
        {
            string mensagem = inStream.ReadLine();
            if (mensagem == null) return;

            int pos = int.Parse(mensagem);

            // Somente aqui as operações críticas são bloqueadas
            if (pos >= 0 && pos < vetor.Length)
            {
                lock (locks[pos]) // Lock com granularidade reduzida
                {
                    vetor[pos] = vetor[pos] + 1;
                    counter++;
                }
                outStream.WriteLine($"Posição {pos} atualizada com o valor {vetor[pos]}");
            }
            else
            {
                outStream.WriteLine("Erro: posição fora do limite.");
            }
        }
    }
}
