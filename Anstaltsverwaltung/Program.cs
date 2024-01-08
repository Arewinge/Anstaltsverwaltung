namespace Anstaltsverwaltung
{
    public class Program
    {
        static void Main(string[] args)
        {
            Bot bot = new Bot();
            bot.RunBot().GetAwaiter().GetResult();
        }
    }
}