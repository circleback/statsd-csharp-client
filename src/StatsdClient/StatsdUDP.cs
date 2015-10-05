using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace StatsdClient
{
    public class StatsdUDP : MetricsSenderBase, IStatsdUDP, IDisposable
    {
        private readonly UdpClient udp;
        private bool _disposed;
        public StatsdUDP(string host, int port, int maxPacketSize = MetricsConfig.DefaultMaxPacketSize)
            : base(host, port, maxPacketSize)
        {
            udp = new UdpClient(); // no need to specify a Local Port since we don't need to receive responses
        }
        protected override async Task SendCommand(byte[] command)
        {
            await udp.SendAsync(command, command.Length, IPEndpoint);
        }

        //reference : https://lostechies.com/chrispatterson/2012/11/29/idisposable-done-right/
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        ~StatsdUDP() 
        {
            // Finalizer calls Dispose(false)
            Dispose(false);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                if (udp != null)
                {
                    try { udp.Close(); }
                    catch { }
                }
            }
            _disposed = true;
        }
    }
}