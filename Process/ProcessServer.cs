using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Threading;

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

                // Criar um novo processo para lidar com cada cliente
                Process process = new Process();
                process.StartInfo.FileName = "dotnet";  // Caminho do executável do servidor
                process.StartInfo.Arguments = "client"; // Nome da aplicação cliente ou argumentos necessários
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardInput = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.Start();

                using (NetworkStream stream = clientSocket.GetStream())
                using (StreamReader inStream = new StreamReader(stream))
                using (StreamWriter outStream = new StreamWriter(stream) { AutoFlush = true })
                {
                    int numberOfRequests = int.Parse(inStream.ReadLine()!);
                    ProcessRequests(inStream, outStream, numberOfRequests);
                    int soma = GetVectorSum();
                    outStream.WriteLine(counter.ToString());
                    outStream.WriteLine(soma.ToString());
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

        // Após processar todas as requisições, incrementa o contador
        counter++;

        // Calcular a soma final e enviar apenas uma vez no final
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
        accessor.Read<int>(pos * sizeof(int), out int value);
        accessor.Write(pos * sizeof(int), value + 1);
    }

    private static int GetVectorSum()
    {
        int sum = 0;
        long length = accessor.Capacity / sizeof(int);
        for (long i = 0; i < length; i++)
        {
            accessor.Read<int>(i * sizeof(int), out int value);
            sum += value;
        }
        return sum;
    }
}