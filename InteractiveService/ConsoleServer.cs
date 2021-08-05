using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InteractiveService
{
    public sealed class ConsoleServer : IDisposable
    {
        private readonly TcpListener tcpListener;
        private readonly ManualResetEventAsync connectedSignal = new();
        private readonly ManualResetEventAsync disconnectSignal = new();

        private TcpClient client;
        private StreamWriter clientStreamWriter;
        private StreamReader clientStreamReader;

        private readonly CancellationTokenSource disposeSignal = new();

        public ConsoleServer(IPAddress bind, int port)
        {
            tcpListener = new TcpListener(bind, port);
            disconnectSignal.Set();

            _ = RunAsync(disposeSignal.Token);
            _ = CheckConnectionStateAsync(disposeSignal.Token);
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    tcpListener.Start(0);

                    client = await tcpListener.AcceptTcpClientAsync();
                    tcpListener.Stop();

                    SetKeepAlive(client.Client, 1000, 1000);
                    var stream = client.GetStream();

                    clientStreamReader = new StreamReader(stream, leaveOpen: true);
                    clientStreamWriter = new StreamWriter(stream)
                    {
                        AutoFlush = true
                    };

                    LogServer($"Client @ {client.Client.RemoteEndPoint} connected.");

                    SignalConnection(true);

                    await disconnectSignal.WaitAsync(cancellationToken);

                    LogServer($"Client @ {client.Client.RemoteEndPoint} disconnected.");
                }
                finally
                {
                    clientStreamReader?.Dispose();
                    clientStreamWriter?.Dispose();
                    client?.Dispose();
                }
            }
        }

        private void SignalConnection(bool isConnected)
        {
            if (isConnected)
            {
                connectedSignal.Set();
                disconnectSignal.Reset();
            }
            else
            {
                connectedSignal.Reset();
                disconnectSignal.Set();
            }
        }

        public bool IsConnected { get; private set; }

        private async Task CheckConnectionStateAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await connectedSignal.WaitAsync(cancellationToken);

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

        private void SetKeepAlive(Socket socket, int time, int interval)
        {
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            byte[] keepAlive = new byte[12];
            BitConverter.GetBytes(1).CopyTo(keepAlive, 0);
            BitConverter.GetBytes(time).CopyTo(keepAlive, 4);
            BitConverter.GetBytes(interval).CopyTo(keepAlive, 8);

#pragma warning disable CA1416 // This application is supposed to run on windows only.
            socket.IOControl(IOControlCode.KeepAliveValues, keepAlive, null);
#pragma warning restore CA1416
        }

        public async Task<bool> TryWriteLineAsync(string line, CancellationToken cancellationToken)
        {
            if (connectedSignal.IsSet)
            {
                try
                {
                    using var token = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, disposeSignal.Token);
                    await clientStreamWriter.WriteLineAsync(line.AsMemory(), token.Token);
                    return true;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception e) when (e is AggregateException ae && ae.InnerException is OperationCanceledException oc)
                {
                    throw oc;
                }
                catch
                {
                    SignalConnection(false);
                    LogServer("Client disconnected by TryWriteLineAsync");
                }
            }

            return false;
        }

        public async Task<string> TryReadLineAsync(CancellationToken cancellationToken)
        {
            using var token = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, disposeSignal.Token);

            await connectedSignal.WaitAsync(token.Token);

            try
            {
                return await clientStreamReader.ReadLineAsync().WithCancellation(token.Token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e) when (e is AggregateException ae && ae.InnerException is OperationCanceledException oc)
            {
                throw oc;
            }
            catch
            {
                SignalConnection(false);
            }

            return null;
        }

        private void LogServer(string message)
        {
            message = "[ISS] " + message;
            Trace.WriteLine(message);
            Console.WriteLine(message);
        }

        public void Dispose()
        {
            tcpListener.Stop();
            disposeSignal.Cancel();
            connectedSignal.Dispose();
            disconnectSignal.Dispose();
        }
    }
}
