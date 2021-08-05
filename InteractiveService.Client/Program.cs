using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace InteractiveService.Client
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var host = new Argument<string>("HOST", () => "localhost", "IP address or domain name of the interative service host.");

            var port = new Option<int>(
                    new[] { "--port", "-p" },
                    getDefaultValue: () => 21331,
                    "Specify the TCP port to publish remote console for client process.");

            port.AddValidator(result => result.GetValueOrDefault() is < 1 or > 65535 ? "Port must be in the range [1,65535]." : null);

            var rootCommand = new RootCommand("Interative Service Client - " +
                "Interact with remote interative console applications hosted by Interative Service over TCP/IP.")
            {
               port,
               host
            };

            rootCommand.Handler = CommandHandler.Create<string, int>(
                async (host, port) =>
                {
                    using var cc = new ConsoleClient(host, port);
                    cc.Start();
                    await Task.WhenAll(ReceiveAsync(cc), SendAsync(cc));
                });


            await rootCommand.InvokeAsync(args);
        }

        private static async Task ReceiveAsync(ConsoleClient cc)
        {
            while (true)
            {
                var l = await cc.ReadLineAsync(default);
                if (l != null)
                {
                    await Console.Out.WriteLineAsync(l);
                }
            }
        }

        private static async Task SendAsync(ConsoleClient cc)
        {
            while (true)
            {
                await cc.ConnectSignal;
                var l = await Console.In.ReadLineAsync();
                await cc.WriteLineAsync(l, default);
            }
        }
    }
}
