using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace StatsdClient
{
    public class StatsdTCP : MetricsSenderBase, IDisposable
    {
        private readonly TcpClient _tcpClient;
        private NetworkStream _stream;
        private readonly AsyncLock clientLock;
        private readonly bool _reconnectEnabled;
        private readonly int _retryAttempts;
        private bool _disposed = false;
        public StatsdTCP(string host, int port, bool reconnectEnabled = true, int retryAttempts = 3)
            : base(host, port, 0) // 0 means unlimited packet size (we don't want to split up our TCP stream)
        {
            _reconnectEnabled = reconnectEnabled;
            _retryAttempts = retryAttempts;
            _tcpClient = new TcpClient();
            clientLock = new AsyncLock();
        }

        protected override void SendCommand(byte[] command)
        {
            SendAsync(command).Wait(); // synchronously wait (TODO: propagate async all the way)
        }
        private async Task SendAsync(byte[] line)
        {
            await SendWithRetryAsync(line, _reconnectEnabled ? _retryAttempts - 1 : 0).ConfigureAwait(false);
        }

        private async Task SendWithRetryAsync(byte[] bytesToSend, int attemptsLeft)
        {
            string errorMessage = null;
            try
            {
                if (!_tcpClient.Connected)
                {
                    await RestoreConnectionAsync();
                }
                await _stream.WriteAsync(bytesToSend, 0, bytesToSend.Length);
            }
            catch (IOException ex)
            {
                errorMessage = string.Format("Sending metrics via TCP failed with an IOException: {0}", ex.Message);
            }
            catch (SocketException ex)
            {
                // No more attempts left, so log it and continue
                errorMessage = string.Format("Sending metrics via TCP failed with a SocketException: {0}, code: {1}", 
                    ex.Message, ex.SocketErrorCode);
            }

            if (errorMessage != null)
            {
                if (attemptsLeft > 0)
                {
                    await SendWithRetryAsync(bytesToSend, --attemptsLeft);
                }
                else
                {
                    // No more attempts left, so log it and continue
                    Trace.TraceWarning(errorMessage);
                }
            }
        }

        private async Task RestoreConnectionAsync()
        {
            if (!_tcpClient.Connected)
            {
                using (await clientLock.LockAsync())
                {
                    if (!_tcpClient.Connected)
                    {
                        await _tcpClient.ConnectAsync(Host, Port);
                        _stream = _tcpClient.GetStream();
                    }
                }
            }
        }


        //reference : https://lostechies.com/chrispatterson/2012/11/29/idisposable-done-right/
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        ~StatsdTCP() 
        {
            Dispose(false);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                if (_tcpClient != null)
                {
                    try
                    {
                        _tcpClient.Close();
                    }
                    catch (Exception)
                    {
                        //Swallow since we are not using a logger, should we add LibLog and start logging??
                    }
                }
                if (_stream != null)
                {
                    try
                    {
                        _stream.Dispose();
                    }
                    catch (Exception)
                    {
                        //Swallow since we are not using a logger, should we add LibLog and start logging??
                    }
                }
            }
            _disposed = true;
        }

    }
}