namespace Server_Client;

public class Validacao
{
    public static bool PortaValida(int port)
    {
        return port >= 1024 && port <= 65535;
    }
}