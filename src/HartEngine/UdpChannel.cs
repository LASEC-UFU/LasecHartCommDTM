using System;
using System.Net;
using System.Net.Sockets;

namespace HartEngine
{
    internal class UdpChannel : IChannel
    {
        private readonly string _host;
        private readonly int _port;
        private UdpClient _client;
        private IPEndPoint _remoteEndPoint;

        public UdpChannel(string host, int port)
        {
            _host = string.IsNullOrEmpty(host) || host.Trim().Length == 0 ? "127.0.0.1" : host;
            _port = port <= 0 ? 20000 : port;
        }

        public void Open()
        {
            if (_client != null) { _client.Close(); _client = null; }
            _remoteEndPoint = new IPEndPoint(IPAddress.Parse(_host), _port);
            _client = new UdpClient();
        }

        public byte[] SendAndReceive(byte[] request, int timeoutMs)
        {
            _client.Client.ReceiveTimeout = timeoutMs;
            _client.Send(request, request.Length, _remoteEndPoint);

            try
            {
                var remote = new IPEndPoint(IPAddress.Any, 0);
                var response = _client.Receive(ref remote);
                return response;
            }
            catch (SocketException)
            {
                return new byte[0];
            }
        }

        public void Dispose()
        {
            if (_client != null) { _client.Close(); _client = null; }
        }
    }
}
