using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace StatsdClient
{
    public abstract class MetricsSenderBase : IMetricsSender
    {
        protected int MaxPacketSize { get; private set; } // Set to zero for no limit.
        public IPEndPoint IPEndpoint { get; private set; }
        protected string Host { get; private set;  }
        protected int Port { get; private set; }

        public MetricsSenderBase(string host, int port, int maxPacketSize = MetricsConfig.DefaultMaxPacketSize)
        {
            Host = host;
            Port = port;
            MaxPacketSize = maxPacketSize;
            var ipAddress = GetIpv4Address(Host);
            IPEndpoint = new IPEndPoint(ipAddress, Port);
        }
        protected abstract void SendCommand(byte[] command);

        public void Send(string command)
        {
            Send(Encoding.ASCII.GetBytes(command));
        }

        private void Send(byte[] encodedCommand)
        {
            if (MaxPacketSize > 0 && encodedCommand.Length > MaxPacketSize)
            {
                // If the command is too big to send, linear search backwards from the maximum
                // packet size to see if we can find a newline delimiting two stats. If we can,
                // split the message across the newline and try sending both componenets individually
                var newline = Encoding.ASCII.GetBytes("\n")[0];
                for (var i = MaxPacketSize; i > 0; i--)
                {
                    if (encodedCommand[i] != newline)
                    {
                        continue;
                    }

                    var encodedCommandFirst = new byte[i];
                    Array.Copy(encodedCommand, encodedCommandFirst, encodedCommandFirst.Length); // encodedCommand[0..i-1]
                    Send(encodedCommandFirst);

                    var remainingCharacters = encodedCommand.Length - i - 1;
                    if (remainingCharacters > 0)
                    {
                        var encodedCommandSecond = new byte[remainingCharacters];
                        Array.Copy(encodedCommand, i + 1, encodedCommandSecond, 0, encodedCommandSecond.Length); // encodedCommand[i+1..end]
                        Send(encodedCommandSecond);
                    }

                    return; // We're done here if we were able to split the message.
                    // At this point we found an oversized message but we weren't able to find a 
                    // newline to split upon. We'll still send it to the UDP socket, which upon sending an oversized message 
                    // will fail silently if the user is running in release mode or report a SocketException if the user is 
                    // running in debug mode.
                    // Since we're conservative with our MAX_UDP_PACKET_SIZE, the oversized message might even
                    // be sent without issue.
                }
            }
            SendCommand(encodedCommand);
        }

        private IPAddress GetIpv4Address(string name)
        {
            IPAddress ipAddress;
            var isValidIpAddress = IPAddress.TryParse(name, out ipAddress);

            if (!isValidIpAddress)
            {
                var addressList = Dns.GetHostEntry(Host).AddressList;

                var positionForIpv4 = addressList.Length - 1;

                ipAddress = addressList[positionForIpv4];
            }
            return ipAddress;
        }
    }
}
