using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;

class Client
{
    static void Main(string[] args)
    {
        // Verifica se um argumento foi passado
        if (args.Length < 1 || !int.TryParse(args[0], out int numberOfClients))
        {
            Console.WriteLine("Uso: MultiThreadedClient <numero_de_clientes>");
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

        Console.WriteLine("Todos os clientes emulados terminaram.");
    }

    static void StartClient()
    {
        string serverIp = "127.0.0.1"; // IP do servidor
        int port = 12345; // Porta do servidor

        try
        {
            Thread.Sleep(100);
            using (TcpClient client = new TcpClient(serverIp, port))
            using (StreamReader inStream = new StreamReader(client.GetStream()))
            using (StreamWriter outStream = new StreamWriter(client.GetStream()) { AutoFlush = true })
            {
                // Envia o número de requisições que este cliente fará
                int numberOfRequests = 18;
                outStream.WriteLine(numberOfRequests); // Envia o número de requisições

                for (int i = 0; i < numberOfRequests; i++)
                {
                    string response = inStream.ReadLine(); // Lê a resposta do servidor
                    Console.WriteLine($"Cliente {Thread.CurrentThread.ManagedThreadId}: Resposta do servidor: {response}");
                    outStream.WriteLine(""); // Envia uma linha em branco para continuar o ciclo
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Erro no cliente {Thread.CurrentThread.ManagedThreadId}: {e.Message}");
        }
    }
}
