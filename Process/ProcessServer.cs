using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;

[Flags]
public enum LogLevel
{
    None = 0,
    Info = 1,
    Error = 2
}

public class ProcessServer
{
    private static MemoryMappedFile sharedMemory;
    private static MemoryMappedViewAccessor accessor;
    private static int counter = 0;
    private static bool useLock = true;
    private static int port;
    private static LogLevel currentLogLevel;
    private static int completedClients = 0;  // Contador para verificar quando todos os clientes terminaram
    private static int nClients = 0; // Número de clientes esperado, será setado quando o cliente se conectar
    private static List<string> clientResults = new List<string>(); // Para armazenar resultados de cada cliente

    public static void Log(string message, LogLevel level)
    {
        if ((currentLogLevel & level) == level)
        {
            Console.WriteLine(message);
        }
    }

    public static void Main(string[] args)
    {
        // Verificar se os parâmetros necessários foram passados
        if (args.Length < 4 ||
            !int.TryParse(args[0], out int vetorSize) || 
            !int.TryParse(args[1], out port) ||
            !Enum.TryParse(args[2], out LogLevel logLevel) ||
            !bool.TryParse(args[3], out useLock))
        {
            Console.WriteLine("Uso: Server <Tamanho do Vetor> <Porta> <LogLevel> <UsarLock(true/false)>");
            return;
        }

        // Criar memória compartilhada
        sharedMemory = MemoryMappedFile.CreateNew("SharedVector", sizeof(int) * vetorSize);
        accessor = sharedMemory.CreateViewAccessor();

        currentLogLevel = logLevel;

        TcpListener? server = null;

        try
        {
            server = new TcpListener(IPAddress.Any, port);
            server.Start();
            Log("Servidor iniciado na porta " + port, LogLevel.Info);

            while (true)
            {
                TcpClient clientSocket = server.AcceptTcpClient();
                Log("Cliente conectado: " + ((IPEndPoint)clientSocket.Client.RemoteEndPoint!)?.Address, LogLevel.None);

                // Incrementa o contador apenas uma vez por cliente
                lock (accessor)
                {
                    counter++;
                }

                // Definindo o número de clientes baseado nas informações enviadas pelo primeiro cliente
                if (nClients == 0)
                {
                    nClients = 1;
                }

                // Criar um novo processo para lidar com cada cliente
                Process process = new Process();
                process.StartInfo.FileName = "dotnet";  // Caminho do executável do servidor
                process.StartInfo.Arguments = "run";   
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardInput = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.Start();

                using (NetworkStream stream = clientSocket.GetStream())
                using (StreamReader inStream = new StreamReader(stream))
                using (StreamWriter outStream = new StreamWriter(stream) { AutoFlush = true })
                {
                    int numberOfRequests = int.Parse(inStream.ReadLine()!);
                    ProcessRequests(inStream, outStream, numberOfRequests); // Processa requisições do cliente
                    int soma = GetVectorSum();

                    string clientResponse = $"{counter} {soma}";
                    lock (clientResults)
                    {
                        clientResults.Add(clientResponse);  // Adiciona resultado na lista
                    }
                }

                // Incrementa o contador de clientes processados
                lock (accessor)
                {
                    completedClients++;
                }

                // Espera todos os clientes terminarem para exibir o contador e soma final
                if (completedClients == nClients)  // Verifica se todos os clientes terminaram
                {
                    // Exibe a resposta final uma vez, após todos os clientes processados
                    foreach (var result in clientResults)
                    {
                        Log($"Resultado do cliente: {result}", LogLevel.Info);
                    }
                    Log($"Contador final do servidor: {counter}", LogLevel.Info);
                }

                process.WaitForExit();
                clientSocket.Close();
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

    private static void ProcessRequests(StreamReader inStream, StreamWriter outStream, int numberOfRequests)
    {
        // Processar todas as operações de leitura e escrita
        for (int i = 0; i < numberOfRequests; i++)
        {
            string mensagem = inStream.ReadLine();
            if (mensagem == null) return;

            // Dividir o comando em "operacao" e "posicao"
            string[] partes = mensagem.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length != 2)
            {
                outStream.WriteLine("Erro: Comando inválido.");
                continue;
            }

            string operacao = partes[0].ToUpper(); // Comando (READ ou WRITE)
            if (!int.TryParse(partes[1], out int pos))
            {
                outStream.WriteLine("Erro: Posição inválida.");
                continue;
            }

            if (operacao == "READ")
            {
                // Operação de leitura
                int valor;
                accessor.Read(pos * sizeof(int), out valor);
                outStream.WriteLine($"READ {pos} -> {valor}");
            }
            else if (operacao == "WRITE")
            {
                // Operação de escrita
                if (useLock)
                {
                    lock (accessor)
                    {
                        IncrementPosition(pos);
                    }
                }
                else
                {
                    IncrementPosition(pos);
                }

                outStream.WriteLine($"WRITE {pos} -> OK");
            }
            else
            {
                outStream.WriteLine("Erro: Operação desconhecida.");
            }
        }

        // Após processar todas as requisições, apenas enviar o contador e a soma final
        int soma = GetVectorSum();
        outStream.WriteLine(counter.ToString());  // Enviar o contador
        outStream.WriteLine(soma.ToString());    // Enviar a soma final
    }


    private static int ReadPosition(int pos)
    {
        accessor.Read<int>(pos * sizeof(int), out int value);
        return value;
    }

    private static void IncrementPosition(int pos)
    {
        // Lê o valor atual da posição
        accessor.Read<int>(pos * sizeof(int), out int valorAtual);

        // Tenta atualizar a posição (se não estiver usando lock, isso pode ser uma operação concorrente)
        try
        {
            // Simula o processamento
            int valorNovo = valorAtual + 1;
            accessor.Write(pos * sizeof(int), valorNovo);

            // Log de concorrência se não usar lock
            if (!useLock)
            {
                Log($"Concorrência detectada: Posicao {pos} foi alterada de {valorAtual} para {valorNovo} sem bloqueio.", LogLevel.Error);
            }
        }
        catch (Exception ex)
        {
            Log($"Erro de concorrência detectado na posição {pos}: {ex.Message}", LogLevel.Error);
        }
    }

    private static int GetVectorSum()
    {
        int sum = 0;
        long length = accessor.Capacity / sizeof(int);

        lock (accessor)  // Adicionando o lock para sincronizar o acesso ao vetor durante a soma
        {
            for (long i = 0; i < length; i++)
            {
                accessor.Read<int>(i * sizeof(int), out int value);
                sum += value;
            }
        }

        return sum;
    }
}
