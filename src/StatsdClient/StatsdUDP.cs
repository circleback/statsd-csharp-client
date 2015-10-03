using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace StatsdClient
{
    public class StatsdUDP : MetricsSenderBase, IStatsdUDP, IDisposable
    {
        private readonly Socket socket;
        private readonly UdpClient udp;
        private bool _disposed;
        public StatsdUDP(string host, int port, int maxPacketSize = MetricsConfig.DefaultMaxPacketSize)
            : base(host, port, maxPacketSize)
        {
            //socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            udp = new UdpClient(); // should we specifically use the port provided? depends on firewall/AWS SG
        }
        protected override async Task SendCommand(byte[] command)
        {
            await udp.SendAsync(command, command.Length, IPEndpoint);
            //socket.SendTo(command, command.Length, SocketFlags.None, IPEndpoint);
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
                if (socket != null)
                {
                    try
                    {
                        socket.Close();
                    }
                    catch (Exception)
                    {
                        //Swallow since we are not using a logger, should we add LibLog and start logging??
                    }
                    
                }
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