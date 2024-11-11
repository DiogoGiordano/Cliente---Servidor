using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Linq;

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
    private static bool useLock; // Controle de uso do lock (concorrência)
    
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
    private static readonly object lockObject = new object(); // Objeto para sincronização
    private static int[] database; // Banco de dados simplificado
    private static bool isServerRunning = true; // Flag para monitorar o status do servidor

    public static void Main(string[] args)
    {
        // Pergunta ao usuário se deseja ativar o controle de concorrência
        Console.WriteLine("Deseja ativar o controle de concorrência (lock)? (S/N)");
        string input = Console.ReadLine().ToUpper();

        if (input == "S")
        {
            useLock = true;
            Log("Controle de concorrência (lock) ativado.", LogLevel.Info);
        }
        else if (input == "N")
        {
            useLock = false;
            Log("Controle de concorrência (lock) desativado.", LogLevel.Info);
        }
        else
        {
            Console.WriteLine("Opção inválida, o controle de concorrência será desativado.");
            useLock = false;
        }

        int port = 12345;
        int vectorSize = 100; // Tamanho do vetor (pode ser alterado conforme necessário)

        // Inicializa o banco de dados com números pseudo-aleatórios
        Random random = new Random();
        database = new int[vectorSize];
        for (int i = 0; i < vectorSize; i++)
        {
            database[i] = random.Next(0, 1000); // Preenche com números aleatórios entre 0 e 100
        }

        TcpListener server = null;

        try
        {
            server = new TcpListener(IPAddress.Any, port);
            server.Start();
            Log("Servidor iniciado na porta " + port, LogLevel.Info);

            // Thread para monitorar o pressionamento de Enter
            Thread closeServerThread = new Thread(() =>
            {
                Console.WriteLine("Pressione Enter para fechar o servidor..."); // Mensagem informando para pressionar Enter
                Console.ReadLine(); // Aguarda o pressionamento da tecla Enter
                Log("Fechando o servidor...", LogLevel.Info);
                isServerRunning = false; // Define a flag para parar o servidor
                server.Stop(); // Encerra o servidor
            });
            closeServerThread.Start();

            while (isServerRunning) // Verifica se o servidor deve continuar executando
            {
                if (server.Pending()) // Verifica se há uma conexão pendente
                {
                    TcpClient clientSocket = server.AcceptTcpClient();
                    Log("Cliente conectado: " + ((IPEndPoint)clientSocket.Client.RemoteEndPoint).Address, LogLevel.Info);
                    Task.Run(() => HandleClient(clientSocket)); // Lida com o cliente em uma nova tarefa
                }
            }
        }
        catch (IOException e)
        {
            Console.Error.WriteLine("Erro no servidor: " + e.Message);
        }
        finally
        {
            // Imprime o somatório do banco de dados simplificado
            int sum = database.Sum();
            Log("Somatório do banco de dados: " + sum, LogLevel.Info);
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

                    if (useLock) // Sincroniza o acesso ao contador apenas se o lock for habilitado
                    {
                        lock (lockObject) 
                        {
                            currentValue = ++counter; // Incrementa o contador
                        }
                    }
                    else
                    {
                        currentValue = ++counter; // Incrementa o contador sem usar o lock
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
