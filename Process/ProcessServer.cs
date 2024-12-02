using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Collections.Generic;
using System.IO;

[Flags]
public enum LogLevel
{
    None = 0,
    Info = 1,
    Error = 2
}

public enum FileSystemType
{
    Ext4,
    NTFS,
    ReiserFS,
    VFAT
}

public class ProcessServer
{
    private static MemoryMappedFile sharedMemory;
    private static MemoryMappedViewAccessor accessor;
    private static int counter = 0;
    private static bool useLock = true;
    private static int port;
    private static int vetorSize;
    private static LogLevel currentLogLevel;
    private static int completedClients = 0;  // Contador para verificar quando todos os clientes terminaram
    private static int nClients = 0; // Número de clientes esperado, será setado quando o cliente se conectar
    private static List<string> clientResults = new List<string>(); // Para armazenar resultados de cada cliente
    private static Dictionary<int, object> positionLocks = new Dictionary<int, object>(); // Dicionário de bloqueios por posição
    private static ReaderWriterLockSlim readerWriterLock = new ReaderWriterLockSlim(); // Instância global do ReaderWriterLockSlim
    private static Dictionary<int, int> cache = new Dictionary<int, int>(); // Cache para armazenar dados lidos
    private static Queue<int> cacheQueue = new Queue<int>(); // Fila para rastrear a ordem de inserção das posições
    private static int cacheLimit = 100; // Limite do tamanho do cache (FIFO)
    private static bool useDiskCache = false; // Se usar o cache de disco
    private static FileSystemType fileSystemType = FileSystemType.Ext4; // Default file system

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
        if (args.Length < 6 ||
            !int.TryParse(args[0], out vetorSize) || 
            !int.TryParse(args[1], out port) ||
            !Enum.TryParse(args[2], out LogLevel logLevel) ||
            !bool.TryParse(args[3], out useLock) ||
            !bool.TryParse(args[4], out useDiskCache) ||
            !Enum.TryParse(args[5], out fileSystemType))  // Tipo de sistema de arquivos
        {
            Console.WriteLine("Uso: Server <Tamanho do Vetor> <Porta> <LogLevel> <UsarLock(true/false)> <UsarCacheDeDisco(true/false)> <TipoDeSistemaDeArquivos>");
            return;
        }

        // Criar memória compartilhada
        sharedMemory = MemoryMappedFile.CreateNew("SharedVector", sizeof(int) * vetorSize);
        accessor = sharedMemory.CreateViewAccessor();

        currentLogLevel = logLevel;

        TcpListener? server = null;
        List<Process> processes = new List<Process>(); // Lista para armazenar processos

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

                // Criar um novo processo para o cliente
                Process process = new Process();
                process.StartInfo.FileName = "dotnet";  // Caminho do executável do servidor
                process.StartInfo.Arguments = "run";  
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardInput = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.Start();

                // Armazene o processo na lista para controle posterior
                processes.Add(process);

                // Criar e processar o cliente em um novo processo
                Task.Run(() => ProcessClient(clientSocket, process)); // Cria o cliente em paralelo
            }

            // Esperar todos os processos terminarem
            foreach (var proc in processes)
            {
                proc.WaitForExit();  // Espera todos os processos finais
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

    private static void ProcessClient(TcpClient clientSocket, Process process)
    {
        using (NetworkStream stream = clientSocket.GetStream())
        using (StreamReader inStream = new StreamReader(stream))
        using (StreamWriter outStream = new StreamWriter(stream) { AutoFlush = true })
        {
            // Enviar o tamanho do vetor para o cliente
            outStream.WriteLine(vetorSize);

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

        // Finaliza o processo do cliente
        process.WaitForExit();
        clientSocket.Close();
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
                int valor = ReadFromCache(pos);
                outStream.WriteLine($"READ {pos} -> {valor}");
            }
            else if (operacao == "WRITE")
            {
                // Operação de escrita
                if (useLock)
                {
                    lock (GetPositionLock(pos))
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

    private static object GetPositionLock(int pos)
    {
        lock (positionLocks)
        {
            if (!positionLocks.ContainsKey(pos))
            {
                positionLocks[pos] = new object(); // Criar um bloqueio se não existir
            }
        }
        return positionLocks[pos];
    }

    private static void IncrementPosition(int pos)
    {
        readerWriterLock.EnterWriteLock();  // Bloqueio exclusivo para escrita
        try
        {
            // Lê o valor atual da posição
            accessor.Read<int>(pos * sizeof(int), out int valorAtual);

            // Tenta atualizar a posição
            int valorNovo = valorAtual + 1;
            accessor.Write(pos * sizeof(int), valorNovo);

            // Atualiza o cache se o cache de disco estiver ativado
            if (useDiskCache)
            {
                // Verifica se o cache atingiu o limite
                if (cache.Count >= cacheLimit)
                {
                    int oldestPos = cacheQueue.Dequeue(); // Remove a posição mais antiga
                    cache.Remove(oldestPos); // Remove do cache
                }

                cache[pos] = valorNovo;  // Atualiza o cache com o novo valor
                cacheQueue.Enqueue(pos); // Adiciona a posição à fila (FIFO)
            }
        }
        finally
        {
            readerWriterLock.ExitWriteLock();  // Libera o bloqueio de escrita
        }
    }

    private static int GetVectorSum()
    {
        readerWriterLock.EnterReadLock();
        try
        {
            int soma = 0;
            for (int i = 0; i < vetorSize; i++)
            {
                accessor.Read<int>(i * sizeof(int), out int valor);
                soma += valor;
            }
            return soma;
        }
        finally
        {
            readerWriterLock.ExitReadLock();
        }
    }

    // Função de leitura com cache FIFO e sistemas de arquivos
    private static int ReadFromCache(int pos)
    {
        if (useDiskCache && cache.ContainsKey(pos))
        {
            // Se estiver usando o cache de disco, tenta obter o valor do cache
            return cache[pos];
        }
        else
        {
            // Caso contrário, lê diretamente do sistema de arquivos simulado
            int valor;
            if (fileSystemType == FileSystemType.Ext4)
            {
                // Simulação de leitura no sistema de arquivos ext4
                valor = ReadFromExt4(pos);
            }
            else if (fileSystemType == FileSystemType.NTFS)
            {
                // Simulação de leitura no sistema de arquivos NTFS
                valor = ReadFromNTFS(pos);
            }
            else if (fileSystemType == FileSystemType.ReiserFS)
            {
                // Simulação de leitura no sistema de arquivos ReiserFS
                valor = ReadFromReiserFS(pos);
            }
            else
            {
                // Simulação de leitura no sistema de arquivos VFAT
                valor = ReadFromVFAT(pos);
            }

            if (useDiskCache)
            {
                // Se o cache de disco estiver habilitado, armazena o valor no cache
                // Primeiro, verificamos se precisamos remover o item mais antigo do cache
                if (cache.Count >= cacheLimit)
                {
                    int oldestPos = cacheQueue.Dequeue(); // Remove a posição mais antiga da fila
                    cache.Remove(oldestPos);  // Remove a posição mais antiga do cache
                }

                // Agora, adiciona o novo valor no cache
                cache[pos] = valor;
                cacheQueue.Enqueue(pos);  // Adiciona a posição na fila (FIFO)
            }
            return valor;
        }
    }

    // Métodos simulados para cada tipo de sistema de arquivos
    private static int ReadFromExt4(int pos)
    {
        // Simulação de leitura no sistema de arquivos Ext4
        return pos * 10;  // Apenas um valor fictício
    }

    private static int ReadFromNTFS(int pos)
    {
        // Simulação de leitura no sistema de arquivos NTFS
        return pos * 20;  // Apenas um valor fictício
    }

    private static int ReadFromReiserFS(int pos)
    {
        // Simulação de leitura no sistema de arquivos ReiserFS
        return pos * 30;  // Apenas um valor fictício
    }

    private static int ReadFromVFAT(int pos)
    {
        // Simulação de leitura no sistema de arquivos VFAT
        return pos * 40;  // Apenas um valor fictício
    }
}
