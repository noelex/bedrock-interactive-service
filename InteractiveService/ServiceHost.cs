using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InteractiveService
{
    class ServiceHost : IDisposable
    {
        private CancellationTokenSource stopRequestSignal, stopDoneSignal;

        public async Task RunAsync(CommandLineArgs args, CancellationToken cancellationToken = default)
        {
            using var gracefulQuitSignal = new ManualResetEventSlim(false);
            using var exitSignal = new CancellationTokenSource();

            var psi = new ProcessStartInfo(args.ClientExecutable, string.Join(' ', args.Arguments))
            {
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WorkingDirectory = args.WorkingDirectory
            };

            var proc = Process.Start(psi);

            proc.EnableRaisingEvents = true;
            proc.Exited += (s, e) =>
                exitSignal.Cancel();

            ChildProcessTracker.AddProcess(proc);

            using var cs = new ConsoleServer(args.Bind, args.Port);

            using var registration = cancellationToken.Register(() => exitSignal.Cancel());

            try
            {
                _ = ProcessToPipeAsync(proc, cs, args, gracefulQuitSignal, exitSignal.Token);
                await PipeToProcessAsync(proc, cs, exitSignal.Token);
            }
            finally
            {
                if (!proc.HasExited)
                {
                    await LogBoth(cs, "Shutting down client process...");

                    if (args.StopCommand != null)
                    {
                        await proc.StandardInput.WriteLineAsync(args.StopCommand);
                        if (!gracefulQuitSignal.Wait(args.StopTimeout))
                        {
                            if (!proc.HasExited)
                            {
                                await LogBoth(cs, "Timed out when waiting for client process to shutdown cleanly, killing client process...");
                            }
                            else
                            {
                                await LogBoth(cs, "Client process stopped by service host.");
                            }
                        }
                        else
                        {
                            await LogBoth(cs, "Client process stopped by service host with stop message.");
                        }
                    }

                    if (!proc.HasExited)
                    {
                        proc.Kill();
                        await LogBoth(cs, "Client process killed.");
                    }
                }
                else
                {
                    await LogBoth(cs, "Client process exited.");
                }


                stopRequestSignal.Dispose();
                stopRequestSignal = null;

                stopDoneSignal.Cancel();
                stopDoneSignal.Dispose();
                stopDoneSignal = null;
            }
        }

        private void LogServer(string message)
        {
            Trace.WriteLine("[ISS] " + message);
            Console.WriteLine("[ISS] " + message);
        }

        private async Task LogBoth(ConsoleServer cs, string message)
        {
            message = "[ISS] " + message;

            Trace.WriteLine(message);
            Console.WriteLine(message);
            await cs.TryWriteLineAsync(message, default);
        }

        public void Stop()
        {
            if (stopRequestSignal is not null)
            {
                stopRequestSignal.Cancel();
            }
        }

        public void Start(CommandLineArgs args)
        {
            if (stopRequestSignal is not null)
            {
                throw new InvalidOperationException("Service host is already started.");
            }

            stopRequestSignal = new();
            stopDoneSignal = new();
            _ = RunAsync(args, stopRequestSignal.Token);
        }

        public async Task WaitForExitAsync()
        {
            if (stopDoneSignal is null)
            {
                throw new InvalidOperationException("Service host is not running yet.");
            }

            try
            {
                await Task.Delay(-1, stopDoneSignal.Token);
            }
            catch
            {

            }
        }

        public void WaitForExit() => WaitForExitAsync().Wait();


        private async Task ProcessToPipeAsync(Process process, ConsoleServer cs, CommandLineArgs args, ManualResetEventSlim quitSignal, CancellationToken cancellationToken)
        {
            while (!process.HasExited)
            {
                var line = await process.StandardOutput.ReadLineAsync();
                if (line is null)
                {
                    LogServer("Received EOF from client process.");
                    break;
                }

                LogServer("<< " + line);
                await cs.TryWriteLineAsync(line, default);

                if (cancellationToken.IsCancellationRequested && args.StopMessage != null && line == args.StopMessage)
                {
                    quitSignal.Set();
                }
            }
        }

        private async Task PipeToProcessAsync(Process process, ConsoleServer cs, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await cs.TryReadLineAsync(cancellationToken);

                if (line is null)
                {
                    LogServer("Received EOF from TCP client.");
                }
                else
                {
                    LogServer(">> " + line);
                    await process.StandardInput.WriteLineAsync(line.AsMemory(), cancellationToken);
                }
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }

    static class Extensions
    {
        public static Task WithCancellation(this Task task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource();
            var reg = cancellationToken.Register(() => tcs.TrySetCanceled());
            return Task.WhenAny(task, tcs.Task).ContinueWith(_ => reg.Dispose());
        }

        public static Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<T>();
            var reg = cancellationToken.Register(() => tcs.TrySetCanceled());
            return Task.WhenAny(task, tcs.Task).ContinueWith(t => { reg.Dispose(); return t.Result.Result; });
        }
    }
}
