using System.Net;
using System.Net.Sockets;
using System.Diagnostics;

[Flags]
public enum LogLevel
{
    None = 0,
    Info = 1,
    Error = 2
}

public class MultiProcessServer
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

                // Inicia um novo processo para lidar com o cliente
                Process clientProcess = new Process();
                clientProcess.StartInfo.FileName = "dotnet"; // Comando dotnet para executar o cliente
                clientProcess.StartInfo.Arguments = "run --project caminho/do/arquivo/Client.csproj"; // Altere para o caminho do cliente
                clientProcess.StartInfo.UseShellExecute = false;
                clientProcess.StartInfo.RedirectStandardInput = true;
                clientProcess.StartInfo.RedirectStandardOutput = true;

                clientProcess.Start();

                // Encaminha o cliente para o processo recém-criado
                HandleClient(clientSocket, clientProcess);
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

    private static void HandleClient(TcpClient clientSocket, Process clientProcess)
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

                    lock (lockObject)
                    {
                        currentValue = ++counter;
                    }

                    outStream.WriteLine(currentValue);
                    inStream.ReadLine();
                }
            }
        }
        catch (IOException e)
        {
            Console.Error.WriteLine("Erro na comunicação com o cliente: " + e.Message);
        }
        finally
        {
            clientSocket.Close();
            clientProcess.Kill(); // Encerra o processo do cliente
        }
    }
}
