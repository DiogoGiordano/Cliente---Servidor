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
    public static int soma = 0;
    public static LogLevel CurrentLogLevel = LogLevel.Info | LogLevel.Error;
    
    public static bool useLock = false;

    public static void Log(string message, LogLevel level)
    {
        if (level == LogLevel.Error || level == LogLevel.Info || level == LogLevel.None)
        {
            Console.WriteLine(message);
        }
    }

    private static int counter = 0; 

    private static readonly object[] lockObjects = new object[vetor.Length]; 

    static MultiThreadedServer()
    {
        for (int i = 0; i < vetor.Length; i++)
        {
            lockObjects[i] = new object();
        }
    }

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
                
                if (useLock)
                {
                    for (int i = 0; i < numberOfRequests; i++)
                    {
                        string mensagem = inStream.ReadLine();
                        if (mensagem == null) return;

                        int pos = int.Parse(mensagem);
                        
                        if (pos >= 0 && pos < vetor.Length)
                        {
                            lock (lockObjects[pos])
                            {
                                vetor[pos] = vetor[pos] + 1;
                                counter++;
                                outStream.WriteLine($"Posição {pos} atualizada com o valor {vetor[pos]}");
                            }
                        }
                        else
                        {
                            outStream.WriteLine("Erro: posição fora do limite.");
                        }
                    }
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
            Log("Erro na comunicação com o cliente: " + e.Message, LogLevel.Error);
        }
        finally
        {
            clientSocket.Close();
        }
    }

    private static void ProcessRequests(StreamReader inStream, StreamWriter outStream, int numberOfRequests)
    {
        for (int i = 0; i < numberOfRequests; i++)
        {
            string mensagem = inStream.ReadLine();
            if (mensagem == null) return;

            int pos = int.Parse(mensagem);

            if (pos >= 0 && pos < vetor.Length)
            {
                vetor[pos] = vetor[pos] + 1;
                counter++;
                outStream.WriteLine($"Posição {pos} atualizada com o valor {vetor[pos]}");
            }
            else
            {
                outStream.WriteLine("Erro: posição fora do limite.");
            }
        }
    }
}
