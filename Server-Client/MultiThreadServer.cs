using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading.Tasks;
using System;

[Flags]
public enum LogLevel
{
    None = 0,
    Info = 1,
    Error = 2
}

public class MultiThreadedServer
{
    public static int[] vetor = new int[1000];
    
    public static LogLevel CurrentLogLevel = LogLevel.Info | LogLevel.Error;

    public static void Log(string message, LogLevel level)
    {
        if ((CurrentLogLevel & level) == level)
        {
            Console.WriteLine(message);
        }
    }

    private static readonly object lockObject = new object();

    public static void Main(string[] args)
    {
        int port = 12345;

        TcpListener? server = null;

        try
        {
            server = new TcpListener(IPAddress.Any, port);
            server.Start();
            Log("Servidor iniciado na porta " + port, LogLevel.Info);

            while (true)
            {
                TcpClient clientSocket = server.AcceptTcpClient();
                Log("Cliente conectado: " + ((IPEndPoint)clientSocket.Client.RemoteEndPoint!)?.Address, LogLevel.Info);
                Task.Run(() => HandleClient(clientSocket));
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
                int numberOfRequests = int.Parse(inStream.ReadLine()!);

                for (int i = 0; i < numberOfRequests; i++)
                {
                    string mensagem = inStream.ReadLine();
                    if (mensagem == null) return;

                    string[] parameters = mensagem.Split(',');

                    if (parameters.Length != 2 || 
                        !int.TryParse(parameters[0], out int pos) || 
                        !int.TryParse(parameters[1], out int value))
                    {
                        outStream.WriteLine("Erro: parâmetros inválidos.");
                        continue;
                    }
                    
                    {
                        lock (lockObject)
                        if (pos >= 0 && pos < vetor.Length)
                        {
                            vetor[pos] = value; 
                            outStream.WriteLine($"Posição {pos} atualizada com o valor {vetor[pos]}");
                        }
                        else
                        {
                            outStream.WriteLine("Erro: posição fora do limite.");
                        }
                    }
                }
            }
        }
        catch (IOException e)
        {
            Log("Erro na comunicação com o cliente: " + e.Message, LogLevel.Error);
        }
        finally
        {
            clientSocket.Close();
        }
    }
}
