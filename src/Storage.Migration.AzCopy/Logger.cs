using Storage.Migration.Service.Interface;

namespace Storage.Migration.AzCopy
{
    public class Logger : ILogger
    {
        public void WriteLine(string message)
        {
            Console.WriteLine(message);
        }
    }
}
