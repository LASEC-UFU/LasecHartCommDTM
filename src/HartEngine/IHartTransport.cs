using System;
using System.Threading;

namespace HartEngine
{
    /// <summary>
    /// Meio de transporte de quadros HART (modem serial, HART-IP, ponte UDP). Uma transação por vez: quem chama
    /// (<see cref="HartMaster"/>) serializa o acesso.
    /// </summary>
    public interface IHartTransport : IDisposable
    {
        string Description { get; }
        bool IsOpen { get; }

        /// <summary>Abre o meio (porta serial, sessão HART-IP). Lança <see cref="HartTransportException"/> em falha.</summary>
        void Open();
        void Close();

        /// <summary>
        /// Envia o pedido e devolve o primeiro quadro de resposta (ACK) que satisfaça <paramref name="accept"/>, ou
        /// null se nada chegou no tempo. Eco do próprio pedido e quadros de burst são descartados.
        /// </summary>
        HartFrame Transact(HartFrame request, int preambles, int responseTimeoutMs, Func<HartFrame, bool> accept, CancellationToken ct);
    }

    public sealed class HartTransportException : Exception
    {
        public HartTransportException(string message, Exception inner = null) : base(message, inner) { }
    }

    public static class HartTransports
    {
        public static IHartTransport Create(HartLinkSettings s, HartCommLog log)
        {
            switch (s.Interface)
            {
                case HartInterface.Serial: return new SerialHartTransport(s, log);
                case HartInterface.HartIpUdp: return new HartIpTransport(s, log, useTcp: false);
                case HartInterface.HartIpTcp: return new HartIpTransport(s, log, useTcp: true);
                case HartInterface.UdpBridge: return new UdpBridgeTransport(s, log);
                default: throw new ArgumentOutOfRangeException(nameof(s));
            }
        }
    }
}
