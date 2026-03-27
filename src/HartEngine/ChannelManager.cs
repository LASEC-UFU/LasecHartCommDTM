using System;

namespace HartEngine
{
    public class ChannelManager : IDisposable
    {
        private IChannel _channel;
        private string _protocol;
        private string _ipAddress;
        private int _ipPort;
        private int _timeout;

        /// <summary>
        /// Configura o canal de comunicação IP (TCP ou UDP).
        /// </summary>
        public void Configure(string protocol, string ipAddress, int ipPort, int timeout)
        {
            _protocol = (protocol ?? "udp").ToLowerInvariant();
            _ipAddress = string.IsNullOrEmpty(ipAddress) ? "127.0.0.1" : ipAddress.Trim();
            _ipPort = ipPort <= 0 ? 5094 : ipPort;
            _timeout = timeout <= 0 ? 5000 : timeout;

            _channel?.Dispose();
            _channel = null;

            if (_protocol == "tcp")
            {
                _channel = new TcpChannel(_ipAddress, _ipPort);
            }
            else
            {
                _channel = new UdpChannel(_ipAddress, _ipPort);
            }

            _channel.Open();
        }

        /// <summary>
        /// Configura o canal de comunicação serial (porta COM).
        /// </summary>
        public void ConfigureSerial(string comPort, int baudRate)
        {
            _protocol = "serial";
            _channel?.Dispose();
            _channel = null;

            _channel = new SerialChannel(comPort, baudRate);
            _channel.Open();
        }

        public byte[] SendAndReceive(byte[] request, int timeoutMs)
        {
            if (_channel == null)
            {
                throw new InvalidOperationException("Channel not configured. Call Configure() first.");
            }
            return _channel.SendAndReceive(request, timeoutMs > 0 ? timeoutMs : _timeout);
        }

        public void Dispose()
        {
            _channel?.Dispose();
            _channel = null;
        }
    }
}
