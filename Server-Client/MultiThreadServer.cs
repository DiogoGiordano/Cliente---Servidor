using System.Net;
using System.Net.Sockets;

public class MultiThreadedServer
{
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
            Console.WriteLine("Servidor iniciado na porta " + port);

            while (true)
            {
                TcpClient clientSocket = server.AcceptTcpClient();
                Console.WriteLine("Cliente conectado: " + ((IPEndPoint)clientSocket.Client.RemoteEndPoint).Address + "Thread id: " + Thread.CurrentThread.ManagedThreadId);
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
