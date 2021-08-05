using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

namespace InteractiveService
{
    class Program
    {
        const int STD_INPUT_HANDLE = -10;

        [DllImport("kernel32", SetLastError = true)]
        static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32", SetLastError = true)]
        static extern bool CancelIoEx(IntPtr handle, IntPtr lpOverlapped);

        static void Main(string[] args)
        {
            if (!Environment.UserInteractive)
            {
                using var service = new Service(args);

#pragma warning disable CA1416 // This application is supposed to run on windows only.
                ServiceBase.Run(service);
#pragma warning restore CA1416
            }
            else
            {
                CommandLineArgs.Invoke(args, c =>
                {
                    try
                    {
                        Console.WriteLine("[ISS] Launching client process. Press ENTER to quit.");

                        using var serviceHost = new ServiceHost();
                        serviceHost.Start(c);

                        var read = false;

                        var t = serviceHost.WaitForExitAsync().ContinueWith(t =>
                          {
                              if (!read)
                              {
                                  var handle = GetStdHandle(STD_INPUT_HANDLE);
                                  CancelIoEx(handle, IntPtr.Zero);
                              }
                          });

                        try
                        {
                            Console.ReadLine();
                        }
                        catch
                        {

                        }

                        read = true;

                        serviceHost.Stop();
                        t.Wait();
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        Console.WriteLine(e);
                    }
                });
            }
        }

    }
}
