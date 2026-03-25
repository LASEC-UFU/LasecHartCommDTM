using System;
using System.Net.Sockets;

namespace HartEngine
{
    internal class TcpChannel : IChannel
    {
        private readonly string _host;
        private readonly int _port;
        private TcpClient _client;
        private NetworkStream _stream;

        public TcpChannel(string host, int port)
        {
            _host = string.IsNullOrEmpty(host) || host.Trim().Length == 0 ? "127.0.0.1" : host;
            _port = port <= 0 ? 5094 : port;
        }

        public void Open()
        {
            Close();
            _client = new TcpClient();
            _client.Connect(_host, _port);
            _stream = _client.GetStream();
        }

        public byte[] SendAndReceive(byte[] request, int timeoutMs)
        {
            if (_stream == null)
                throw new InvalidOperationException("TCP channel not open");

            _stream.WriteTimeout = timeoutMs;
            _stream.ReadTimeout = timeoutMs;

            // Envia request
            _stream.Write(request, 0, request.Length);
            _stream.Flush();

            // Lê resposta
            var buffer = new byte[4096];
            int totalRead = 0;

            try
            {
                // Lê primeiro bloco (bloqueante com timeout)
                int bytesRead = _stream.Read(buffer, 0, buffer.Length);
                totalRead = bytesRead;

                // Continua lendo enquanto houver dados disponíveis
                while (bytesRead > 0 && _stream.DataAvailable)
                {
                    if (totalRead == buffer.Length)
                        Array.Resize(ref buffer, buffer.Length * 2);

                    bytesRead = _stream.Read(buffer, totalRead, buffer.Length - totalRead);
                    totalRead += bytesRead;
                }
            }
            catch (System.IO.IOException)
            {
                // Timeout na leitura
            }

            var result = new byte[totalRead];
            Array.Copy(buffer, result, totalRead);
            return result;
        }

        public void Dispose()
        {
            Close();
        }

        private void Close()
        {
            if (_stream != null)
            {
                try { _stream.Close(); } catch { }
                _stream = null;
            }
            if (_client != null)
            {
                try { _client.Close(); } catch { }
                _client = null;
            }
        }
    }
}
