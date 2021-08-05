using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace InteractiveService
{
    sealed record CommandLineArgs(
        string ClientExecutable,
        string Arguments,
        string WorkingDirectory,
        IPAddress Bind,
        int Port,
        string StopCommand,
        string StopMessage,
        int StopTimeout
    )
    {
        public static int Invoke(string[] args, Action<CommandLineArgs> handler)
        {
            var clientExecutable = new Argument<FileInfo>("EXEC", "Path to the client executable to host.");

            var arguments = new Argument<string[]>("ARGS", "Arguments to use when lauching client executable.")
            {
                Arity = ArgumentArity.ZeroOrMore
            };

            var workingDirectory = new Option<DirectoryInfo>(
                    new[] { "--working-directory", "-d" },
                    "Specify working directory of the client process. Default to client executable's directory.")
                .ExistingOnly();

            var bind = new Option<string>(
                    new[] { "--bind", "-b" },
                    getDefaultValue: () => IPAddress.Loopback.ToString(),
                    description: "Specify the address to listen at.");

            var port = new Option<int>(
                    new[] { "--port", "-p" },
                    getDefaultValue: () => 21331,
                    "Specify the TCP port to publish remote console for client process.");

            var stopCommand = new Option<string>(
                    new[] { "--stop-command", "-s" },
                    "Specify a command to be sent to the client process when host is requested to stop. A newline is appended to the command automatically."
                    );

            var stopMessage = new Option<string>(
                    new[] { "--stop-message", "-m" },
                    "Requires --stop-command to be specified. Wait for the client to print specified message after sending stop command. If --stop-timeout is specified, " +
                    "host will stop waiting when timeout is reached. Otherwise host will wait indefinitely until stop message is received.");

            var stopTimeout = new Option<int>(
                    new[] { "--stop-timeout", "-t" },
                    getDefaultValue: () => 1000,
                    "Requires --stop-command to be specified. Time (milliseconds) to wait for the client process to stop gracefully. Host will kill the client process when timeout is reached.");

            bind.AddValidator(result => !IPAddress.TryParse(result.GetValueOrDefault<string>(), out _) ? "Format of the bind address is invalid." : null);
            port.AddValidator(result => result.GetValueOrDefault() is < 1 or > 65535 ? "Port must be in the range [1,65535]." : null);
            stopTimeout.AddValidator(result => result.GetValueOrDefault<int>() < 0 ? "Stop timeout must be equal or greater than 0." : null);

            var rootCommand = new RootCommand("Interative Service - Host a command-line interative application and forward its standard input/ouput via TCP/IP.")
            {
               workingDirectory,
               bind,port,stopCommand,stopMessage,stopTimeout,
               clientExecutable,
               arguments
            };

            rootCommand.AddValidator(result =>
            {
                var msg = result.FindResultFor(stopMessage);
                var timeout = result.FindResultFor(stopTimeout);
                var cmd = result.FindResultFor(stopCommand);
                if ((msg != null || (timeout != null && timeout.GetValueOrDefault<int>() != 1000)) && cmd is null)
                {
                    return "--stop-message and --stop-timeout can only be specified when --stop-command is specified.";
                }

                return null;
            });

            rootCommand.Handler = CommandHandler.Create<DirectoryInfo, string, int, string, string, int, FileInfo, string[], ParseResult>(
                (workingDirectory, bind, port, stopCommand, stopMessage, stopTimeout, exec, args, result) =>
              {
                  var argString = string.Join(' ', args);
                  var dir = workingDirectory is not null ? workingDirectory.FullName : exec.DirectoryName;
                  stopCommand = result.HasOption("--stop-command") ? stopCommand : null;
                  stopMessage = result.HasOption("--stop-message") ? stopMessage : null;

                  var opts = new CommandLineArgs(exec.FullName, argString, dir, IPAddress.Parse(bind), port, stopCommand, stopMessage, stopTimeout);
                  handler(opts);
              });

            return rootCommand.Invoke(args);
        }
    }
}
