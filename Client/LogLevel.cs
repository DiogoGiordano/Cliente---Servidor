namespace Server_Client;

[Flags]
public enum LogLevel
{
    None = 0,
    Basic = 1,
    Info = 2,
    
}

public class Teste
{
    private static LogLevel _currentLogLevel;
    
    public Teste(LogLevel level)
    {
        _currentLogLevel = level;
    }
    
    public static void log(String message, LogLevel level) {
        

        if ((_currentLogLevel & level) == level)
        {
            Console.WriteLine(message);
        }
    }

}