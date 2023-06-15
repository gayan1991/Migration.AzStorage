using System.Diagnostics;

namespace Storage.Migration.Service.Util
{
    internal class Command
    {
        internal static async Task Execute(string command, bool writeOutPut = false)
        {
            var process = Process.Start(GetProcessStartInfoForCommand(command));

            if (process is null)
            {
                return;
            }

            if (writeOutPut)
            {
                process.BeginOutputReadLine();
                process.OutputDataReceived += (s, e) => Console.WriteLine(e.Data);
            }

            await process.WaitForExitAsync();
        }

        internal static async Task<string> ExecuteWithOutput(string command)
        {
            var process = Process.Start(GetProcessStartInfoForCommand(command));

            if (process is null)
            {
                return null!;
            }

            var output = string.Empty;

            process!.BeginOutputReadLine();
            process.OutputDataReceived += (s, e) => output += e.Data;
            await process.WaitForExitAsync();

            return output;
        }

        private static ProcessStartInfo GetProcessStartInfoForCommand(string command)
        {
            return new ProcessStartInfo
            {
                FileName = @"cmd",
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                Arguments = @"/c " + command
            };
        }
    }
}
