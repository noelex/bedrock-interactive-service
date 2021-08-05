using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InteractiveService.Client
{
    class ConsoleClient : IDisposable
    {
        private readonly string host;
        private readonly int port;

        private TcpClient client;

        private StreamReader reader;
        private StreamWriter writer;

        private readonly ManualResetEventAsync disconnectSignal = new(), connectSignal = new();
        private readonly CancellationTokenSource disposeSignal = new CancellationTokenSource();

        public ConsoleClient(string host, int port)
        {
            this.host = host;
            this.port = port;

            disconnectSignal.Set();
        }

        public void Start()
        {
            _ = CheckConnectionStateAsync(disposeSignal.Token);
            _ = RunAsync(disposeSignal.Token);
        }

        public Task ConnectSignal => connectSignal.WaitAsync();

        private void SignalConnection(bool isConnected)
        {
            if (isConnected)
            {
                connectSignal.Set();
                disconnectSignal.Reset();
            }
            else
            {
                connectSignal.Reset();
                disconnectSignal.Set();
            }
        }

        private async Task CheckConnectionStateAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await connectSignal.WaitAsync(cancellationToken);

                try
                {
                    IsConnected = !(client.Client.Poll(0, SelectMode.SelectRead) && client.Client.Available == 0);
                }
                catch
                {
                    IsConnected = false;
                }

                if (!IsConnected)
                {
                    SignalConnection(false);
                }
                else
                {
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine($"[ISC] Trying to connect interative service host @ {host}:{port}...");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    client = new TcpClient();
                    await client.ConnectAsync(host, port);

                    var stream = client.GetStream();
                    reader = new StreamReader(stream, leaveOpen: true);
                    writer = new StreamWriter(stream);
                    writer.AutoFlush = true;

                    Console.WriteLine($"[ISC] Connected to interative service host @ {host}:{port}.");

                    SignalConnection(true);
                    await disconnectSignal.WaitAsync(cancellationToken);

                    Console.WriteLine($"[ISC] Disconnected from interative service host @ {host}:{port}, reconnecting...");
                }
                catch
                {
                    SignalConnection(false);
                    await Task.Delay(1000, cancellationToken);
                }
                finally
                {
                    reader?.Dispose();
                    writer?.Dispose();
                    client?.Dispose();
                }
            }
        }

        public bool IsConnected { get; private set; }

        public void Stop()
        {
            disposeSignal.Cancel();
        }

        public async Task<string> ReadLineAsync(CancellationToken cancellationToken)
        {
            try
            {
                await connectSignal.WaitAsync(cancellationToken);
                return await reader.ReadLineAsync();
            }
            catch
            {
                return null;
            }
        }

        public async Task WriteLineAsync(string line, CancellationToken cancellationToken)
        {
            try
            {
                await connectSignal.WaitAsync(cancellationToken);
                await writer.WriteLineAsync(line);
            }
            catch
            {
            
            }
        }

        public void Dispose()
        {
            Stop();
            disposeSignal.Dispose();
            disconnectSignal.Dispose();
            connectSignal.Dispose();
        }
    }
}
