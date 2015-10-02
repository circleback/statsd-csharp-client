using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Linq;
using System.Text;

namespace StatsdClient
{
    public class StatsdTCP : MetricsSenderBase, IDisposable
    {
        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private readonly AsyncLock clientLock;
        private readonly bool _reconnectEnabled;
        private readonly int _retryAttempts;
        private bool _disposed = false;
        private readonly IPEndPoint localEndpoint;
        public StatsdTCP(string host, int port, bool reconnectEnabled = true, int retryAttempts = 3)
            : base(host, port, 0) // 0 means unlimited packet size (we don't want to split up our TCP stream)
        {
            _reconnectEnabled = reconnectEnabled;
            _retryAttempts = retryAttempts;
            var localIp = Dns.GetHostEntry(Dns.GetHostName()).AddressList
                .FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork);
            localEndpoint = new IPEndPoint(localIp, Port);
            _tcpClient = new TcpClient(new IPEndPoint(localIp, Port)); // match local port to the remote port
            clientLock = new AsyncLock();
        }

        protected override void SendCommand(byte[] command)
        {
            Exception e = null;
            SendAsync(command).ContinueWith(t => { if (t.IsFaulted) e = t.Exception.InnerException; })
                .Wait(); // synchronously wait (TODO: propagate async all the way)
            if (e != null) throw e;
        }
        private async Task SendAsync(byte[] line)
        {
            await SendWithRetryAsync(line, _reconnectEnabled ? _retryAttempts - 1 : 0).ConfigureAwait(false);
        }

        private async Task SendWithRetryAsync(byte[] bytesToSend, int attemptsLeft)
        {
            string errorMessage = null;
            Exception excp = null;
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
                excp = ex;
            }
            catch (SocketException ex)
            {
                errorMessage = string.Format("Sending metrics via TCP failed with a SocketException: {0}, code: {1}", 
                    ex.Message, ex.SocketErrorCode);
                excp = ex;
            }
            catch (Exception ex)
            {
                errorMessage = string.Format("Sending metrics via TCP failed with unknown Exception: {0}",
                    ex.Message);
                excp = ex;
            }
            if (excp != null)
            {
                if (attemptsLeft > 0)
                {
                    Debug.WriteLine("{0}. {1} more attempts left; retrying . . .", errorMessage, attemptsLeft);
                    await SendWithRetryAsync(bytesToSend, --attemptsLeft);
                }
                else
                {
                    // No more attempts left, so log it and continue
                    var metric = Encoding.ASCII.GetString(bytesToSend);
                    Trace.TraceError(string.Format("Failed to write to Metrics server. Metric: '{0}'. Error: {1}",
                        metric, errorMessage));
                    // Throw and let caller decide what to do
                    throw new TimeoutException(string.Format("Failed to send Metrics after {0} retries. " +
                        "Metric was: '{1}'. See Inner for more details.", _retryAttempts, metric), excp);
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
                        _tcpClient.Close();
                        _tcpClient = new TcpClient(localEndpoint);
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