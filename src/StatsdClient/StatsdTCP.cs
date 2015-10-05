using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace StatsdClient
{
    public class StatsdTCP : MetricsSenderBase, IDisposable
    {
        #region Private Fields

        private readonly bool _reconnectEnabled;
        private readonly int _retryAttempts;
        private readonly AsyncLock clientLock;
        private readonly IPAddress localIp;
        private readonly int[] localPorts;
        private bool _disposed = false;
        private NetworkStream _stream;
        private TcpClient _tcpClient = null;
        private IPEndPoint localEndpoint;

        #endregion Private Fields

        #region Public Constructors

        public StatsdTCP(string host, int port, bool reconnectEnabled = true, int retryAttempts = 3)
            : base(host, port, 0) // 0 means unlimited packet size (we don't want to split up our TCP stream)
        {
            _reconnectEnabled = reconnectEnabled;
            _retryAttempts = retryAttempts;
            localIp = Dns.GetHostEntry(Dns.GetHostName()).AddressList
                .FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork);
            clientLock = new AsyncLock();
        }

        public StatsdTCP(string host, int remotePort, int[] localPorts, bool reconnectEnabled = true, int retryAttempts = 3)
            : this(host, remotePort, reconnectEnabled, retryAttempts)
        {
            this.localPorts = localPorts;
        }

        #endregion Public Constructors

        #region Protected Methods
        protected override async Task SendCommand(byte[] command)
        {
            await SendWithRetryAsync(command, _reconnectEnabled ? _retryAttempts - 1 : 0).ConfigureAwait(false);
        }

        #endregion Protected Methods

        #region Private Methods

        private static int GetAvailablePort(int[] ports)
        {
            // Scan the current TCP connections for the first available Port
            var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            var tcpConnInfoArray = ipGlobalProperties.GetActiveTcpConnections();
            foreach (int port in ports)
            {
                if (IsPortAvailable(port, tcpConnInfoArray))
                    return port;
            }
            throw new InvalidOperationException("None of the Ports provided are available for connection using TCP.");
        }

        private static bool IsPortAvailable(int port, TcpConnectionInformation[] tcpConnInfoArray)
        {
            bool isAvailable = true;
            foreach (TcpConnectionInformation tcpi in tcpConnInfoArray)
            {
                if (port > 0 && tcpi.LocalEndPoint.Port == port)
                {
                    isAvailable = false;
                    break;
                }
            }
            return isAvailable;
        }

        private async Task Connect()
        {
            var localPort = localPorts == null ? RemotePort : GetAvailablePort(localPorts);
            localEndpoint = new IPEndPoint(localIp, localPort);
            _tcpClient = new TcpClient(localEndpoint); // if the connection faulted, need to create a new one
            await _tcpClient.ConnectAsync(Host, RemotePort);
            _stream = _tcpClient.GetStream();
        }

        private async Task DoInLockAsync(Func<bool> check, Func<Task> action)
        {
            if (check())
            {
                using (await clientLock.LockAsync())
                {
                    if (check())
                    {
                        await action();
                    }
                }
            }
        }

        private async Task InitializeConnectionAsync()
        {
            await DoInLockAsync(() => _tcpClient == null, async () => await Connect());
        }

        private async Task RestoreConnectionAsync()
        {
            await DoInLockAsync(() => !_tcpClient.Connected, async () =>
            {
                _tcpClient.Close();
                await Connect();
            });
        }

        private async Task SendWithRetryAsync(byte[] bytesToSend, int attemptsLeft)
        {
            string errorMessage = null;
            Exception excp = null;
            try
            {
                if (_tcpClient == null)
                {
                    await InitializeConnectionAsync();
                }
                else if (!_tcpClient.Connected)
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
                    Trace.TraceError(string.Format("Failed to write to Metrics server. Error: {0}", errorMessage));
                    var metric = Encoding.ASCII.GetString(bytesToSend);
                    // Throw and let caller decide what to do
                    throw new MessageNotDeliveredException(metric, string.Format("Failed to send Metrics after {0} " + 
                        "retries. See Inner for more details.", _retryAttempts), excp);
                }
            }
        }

        #endregion Private Methods

        #region Dispose
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
        #endregion Dispose
    }
}