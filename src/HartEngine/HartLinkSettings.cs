using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HartEngine
{
    /// <summary>Meio físico até os instrumentos.</summary>
    public enum HartInterface
    {
        /// <summary>Modem HART em porta COM: RS-232, USB (porta virtual) ou Bluetooth (perfil SPP).</summary>
        Serial,
        /// <summary>HART-IP sobre UDP (porta 5094): gateways WirelessHART, multiplexadores e instrumentos HART-IP.</summary>
        HartIpUdp,
        /// <summary>HART-IP sobre TCP (porta 5094).</summary>
        HartIpTcp,
        /// <summary>Ponte UDP própria: o quadro da linha (com preâmbulos) vai inteiro num datagrama.</summary>
        UdpBridge,
    }

    /// <summary>Controle da linha RTS do modem serial.</summary>
    public enum RtsControl
    {
        /// <summary>RTS ligado só durante a transmissão, controlado pelo DTM (funciona com qualquer driver).</summary>
        Toggle,
        /// <summary>RTS_CONTROL_TOGGLE do driver serial (nem todo driver USB suporta).</summary>
        DriverToggle,
        /// <summary>RTS sempre ligado.</summary>
        On,
        /// <summary>RTS sempre desligado (modems USB/Bluetooth que controlam a portadora sozinhos).</summary>
        Off,
    }

    public enum SerialParity { None = 0, Odd = 1, Even = 2 }

    /// <summary>
    /// Configuração completa do canal HART. É o data set persistente do Comm DTM (IPersistPropertyBag /
    /// IPersistStreamInit): cada propriedade vira uma entrada nome=valor.
    /// </summary>
    public sealed class HartLinkSettings
    {
        public HartInterface Interface { get; set; } = HartInterface.Serial;

        // ---- serial (modem RS-232 / USB / Bluetooth)
        public string Port { get; set; } = "COM1";
        public int Baudrate { get; set; } = 1200;
        public SerialParity Parity { get; set; } = SerialParity.Odd;
        public int StopBits { get; set; } = 1;
        public RtsControl RtsControl { get; set; } = RtsControl.Toggle;
        public bool DtrEnabled { get; set; } = true;
        /// <summary>Atraso entre ligar RTS e transmitir (portadora do modem).</summary>
        public int RtsDelayMs { get; set; } = 5;

        // ---- rede (HART-IP / ponte UDP)
        public string HostAddress { get; set; } = "192.168.1.10";
        public int HostPort { get; set; } = 5094;
        /// <summary>HART-IP: tempo de inatividade pedido ao servidor para fechar a sessão.</summary>
        public int InactivityTimeoutMs { get; set; } = 60000;

        // ---- mestre HART
        public bool PrimaryMaster { get; set; } = true;
        public int PreambleCount { get; set; } = 5;
        public int RetryCount { get; set; } = 3;
        /// <summary>Tempo máximo até o início da resposta. 0 = automático (1 s serial, 5 s rede).</summary>
        public int ResponseTimeoutMs { get; set; }
        /// <summary>Tempo máximo para concluir uma resposta atrasada (códigos 33/34, típica de WirelessHART).</summary>
        public int DelayedResponseTimeoutMs { get; set; } = 60000;

        // ---- varredura (topology scan)
        public int ScanStart { get; set; }
        public int ScanStop { get; set; } = 15;
        /// <summary>Na varredura, listar os sub-dispositivos de gateways/multiplexadores (comandos 74 e 84).</summary>
        public bool ScanSubDevices { get; set; } = true;

        public bool IsNetwork => Interface != HartInterface.Serial;

        public int EffectiveResponseTimeoutMs => ResponseTimeoutMs > 0 ? ResponseTimeoutMs : IsNetwork ? 5000 : 1000;

        public HartLinkSettings Clone() => (HartLinkSettings)MemberwiseClone();

        /// <summary>Texto curto para log e UI (ex.: "COM3 1200 8O1" ou "HART-IP/UDP 192.168.1.10:5094").</summary>
        public string Describe()
        {
            switch (Interface)
            {
                case HartInterface.Serial:
                    return $"{Port} {Baudrate} 8{Parity.ToString()[0]}{StopBits} RTS={RtsControl}";
                case HartInterface.HartIpUdp: return $"HART-IP/UDP {HostAddress}:{HostPort}";
                case HartInterface.HartIpTcp: return $"HART-IP/TCP {HostAddress}:{HostPort}";
                default: return $"Ponte UDP {HostAddress}:{HostPort}";
            }
        }

        /// <summary>Validação antes de aplicar. Retorna null se está tudo certo.</summary>
        public string Validate()
        {
            if (Interface == HartInterface.Serial)
            {
                if (string.IsNullOrWhiteSpace(Port)) return "Informe a porta serial.";
                if (Baudrate < 300 || Baudrate > 115200) return "Velocidade fora da faixa (300 a 115200).";
                if (StopBits != 1 && StopBits != 2) return "Stop bits deve ser 1 ou 2.";
            }
            else
            {
                if (string.IsNullOrWhiteSpace(HostAddress)) return "Informe o endereço do servidor.";
                if (HostPort < 1 || HostPort > 65535) return "Porta de rede fora da faixa.";
            }
            if (PreambleCount < 2 || PreambleCount > 20) return "Preâmbulos: de 2 a 20.";
            if (RetryCount < 0 || RetryCount > 10) return "Repetições: de 0 a 10.";
            if (ScanStart < 0 || ScanStop > 63 || ScanStart > ScanStop) return "Faixa de varredura inválida (0 a 63).";
            return null;
        }

        // ------------------------------------------------------------ persistência nome=valor

        public static readonly string[] PropertyNames =
        {
            nameof(Interface), nameof(Port), nameof(Baudrate), nameof(Parity), nameof(StopBits), nameof(RtsControl),
            nameof(DtrEnabled), nameof(RtsDelayMs), nameof(HostAddress), nameof(HostPort), nameof(InactivityTimeoutMs),
            nameof(PrimaryMaster), nameof(PreambleCount), nameof(RetryCount), nameof(ResponseTimeoutMs),
            nameof(DelayedResponseTimeoutMs), nameof(ScanStart), nameof(ScanStop), nameof(ScanSubDevices),
        };

        /// <summary>Valores tipados (string, int ou bool) para gravar num property bag.</summary>
        public IEnumerable<KeyValuePair<string, object>> ToProperties()
        {
            foreach (var n in PropertyNames)
            {
                var v = GetType().GetProperty(n).GetValue(this);
                yield return new KeyValuePair<string, object>(n, v is Enum ? v.ToString() : v);
            }
        }

        /// <summary>Aplica um valor lido (qualquer tipo VARIANT). Propriedades desconhecidas são ignoradas.</summary>
        public bool TrySet(string name, object value)
        {
            var p = GetType().GetProperty(name ?? "", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
            if (p == null || !p.CanWrite || !PropertyNames.Contains(p.Name) || value == null) return false;
            string s = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
            try
            {
                object v;
                if (p.PropertyType == typeof(string)) v = s;
                else if (p.PropertyType == typeof(int)) v = int.Parse(s, CultureInfo.InvariantCulture);
                else if (p.PropertyType == typeof(bool)) v = s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "-1";
                else if (p.PropertyType.IsEnum) v = Enum.Parse(p.PropertyType, s, true);
                else return false;
                p.SetValue(this, v);
                return true;
            }
            catch { return false; }
        }

        /// <summary>Texto "Nome=Valor" por linha (IPersistStreamInit).</summary>
        public string ToText() =>
            string.Join("\n", ToProperties().Select(kv => kv.Key + "=" + Convert.ToString(kv.Value, CultureInfo.InvariantCulture)));

        public static HartLinkSettings FromText(string text)
        {
            var s = new HartLinkSettings();
            foreach (var line in (text ?? "").Split('\n'))
            {
                int i = line.IndexOf('=');
                if (i > 0) s.TrySet(line.Substring(0, i).Trim(), line.Substring(i + 1).TrimEnd('\r'));
            }
            return s;
        }
    }
}
