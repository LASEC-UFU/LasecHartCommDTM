using System;
using System.Collections.Generic;
using System.Linq;

namespace HartEngine
{
    /// <summary>Tipo de quadro (bits 0–2 do delimitador).</summary>
    public enum HartFrameType : byte
    {
        /// <summary>Burst (escravo → mestre, não solicitado).</summary>
        Back = 1,
        /// <summary>Mestre → escravo.</summary>
        Stx = 2,
        /// <summary>Escravo → mestre (resposta).</summary>
        Ack = 6,
    }

    /// <summary>
    /// Endereço HART de um instrumento. Curto (endereço de polling, só para o comando 0) ou longo (endereço único
    /// de 5 bytes, usado em todos os demais comandos).
    ///   longo: byte0 = fabricante (HART 5) ou byte alto do tipo expandido (HART 6/7), 6 bits; byte1 = tipo;
    ///          bytes 2–4 = identificação do instrumento.
    /// </summary>
    public sealed class HartAddress : IEquatable<HartAddress>
    {
        private HartAddress(bool isLong, byte polling, byte[] unique)
        {
            IsLong = isLong;
            PollingAddress = polling;
            Unique = unique;
        }

        public bool IsLong { get; }
        public byte PollingAddress { get; }
        /// <summary>5 bytes, sem os bits de mestre e burst (null em endereço curto).</summary>
        public byte[] Unique { get; }

        public static HartAddress Short(byte pollingAddress)
        {
            if (pollingAddress > 63) throw new ArgumentOutOfRangeException(nameof(pollingAddress), "Endereço de polling HART vai de 0 a 63.");
            return new HartAddress(false, pollingAddress, null);
        }

        public static HartAddress Long(byte b0, byte deviceType, uint deviceId) =>
            new HartAddress(true, 0, new[] { (byte)(b0 & 0x3F), deviceType, (byte)(deviceId >> 16), (byte)(deviceId >> 8), (byte)deviceId });

        public static HartAddress Long(byte[] unique)
        {
            if (unique == null || unique.Length != 5) throw new ArgumentException("Endereço longo HART tem 5 bytes.", nameof(unique));
            var u = (byte[])unique.Clone();
            u[0] &= 0x3F;
            return new HartAddress(true, 0, u);
        }

        public byte ManufacturerOrTypeHigh => IsLong ? Unique[0] : (byte)0;
        public byte DeviceType => IsLong ? Unique[1] : (byte)0;
        public uint DeviceId => IsLong ? (uint)((Unique[2] << 16) | (Unique[3] << 8) | Unique[4]) : 0;

        /// <summary>Bytes de endereço no quadro, com o bit de mestre primário (bit 7) e de burst (bit 6).</summary>
        public byte[] Encode(bool primaryMaster, bool burst = false)
        {
            byte flags = (byte)((primaryMaster ? 0x80 : 0) | (burst ? 0x40 : 0));
            if (!IsLong) return new[] { (byte)(flags | PollingAddress) };
            var b = (byte[])Unique.Clone();
            b[0] = (byte)(flags | (b[0] & 0x3F));
            return b;
        }

        /// <summary>Lê o endereço dos bytes de um quadro (ignora mestre e burst).</summary>
        public static HartAddress Decode(byte[] raw)
        {
            if (raw.Length == 1) return Short((byte)(raw[0] & 0x3F));
            return Long(raw);
        }

        public bool Equals(HartAddress other) =>
            other != null && IsLong == other.IsLong &&
            (IsLong ? Unique.SequenceEqual(other.Unique) : PollingAddress == other.PollingAddress);

        public override bool Equals(object obj) => Equals(obj as HartAddress);
        public override int GetHashCode() => IsLong ? (int)DeviceId ^ (Unique[0] << 24) ^ (Unique[1] << 16) : PollingAddress;
        public override string ToString() => IsLong ? "long " + Hex.Of(Unique) : "short " + PollingAddress;
    }

    /// <summary>
    /// Quadro HART da camada de enlace (token-passing), sem os preâmbulos:
    ///   delimitador | endereço (1 ou 5) | [bytes de expansão] | comando | contagem | dados | checksum (XOR).
    /// Nas respostas (ACK/BACK), os dois primeiros bytes de dados são o código de resposta e o status do instrumento.
    /// </summary>
    public sealed class HartFrame
    {
        public HartFrameType Type { get; set; }
        public bool LongAddress { get; set; }
        /// <summary>Bytes de endereço como estão no quadro (com bits de mestre e burst).</summary>
        public byte[] AddressBytes { get; set; }
        public byte[] Expansion { get; set; } = new byte[0];
        public byte Command { get; set; }
        /// <summary>Todos os bytes após a contagem (nas respostas, inclui código de resposta e status).</summary>
        public byte[] Data { get; set; } = new byte[0];

        public bool PrimaryMaster => (AddressBytes[0] & 0x80) != 0;
        public bool BurstBit => (AddressBytes[0] & 0x40) != 0;
        public HartAddress Address => HartAddress.Decode(AddressBytes);

        /// <summary>Primeiro byte de status (resposta). Bit 7 = erro de comunicação detectado pelo escravo.</summary>
        public byte ResponseCode => Data.Length > 0 ? Data[0] : (byte)0;
        public byte DeviceStatus => Data.Length > 1 ? Data[1] : (byte)0;
        public bool IsCommunicationError => Data.Length > 0 && (Data[0] & 0x80) != 0;
        /// <summary>Dados do comando, sem os dois bytes de status.</summary>
        public byte[] Payload => Data.Length > 2 ? Data.Skip(2).ToArray() : new byte[0];

        public static HartFrame Request(HartAddress address, bool primaryMaster, byte command, byte[] data) => new HartFrame
        {
            Type = HartFrameType.Stx,
            LongAddress = address.IsLong,
            AddressBytes = address.Encode(primaryMaster),
            Command = command,
            Data = data ?? new byte[0],
        };

        public byte Delimiter => (byte)((LongAddress ? 0x80 : 0) | ((Expansion.Length & 0x03) << 5) | (byte)Type);

        /// <summary>Quadro sem preâmbulos (como vai num PDU HART-IP).</summary>
        public byte[] Encode()
        {
            if (Data.Length > 255) throw new InvalidOperationException("Dados HART limitados a 255 bytes.");
            var b = new List<byte>(8 + Data.Length) { Delimiter };
            b.AddRange(AddressBytes);
            b.AddRange(Expansion);
            b.Add(Command);
            b.Add((byte)Data.Length);
            b.AddRange(Data);
            b.Add(Checksum(b, 0, b.Count));
            return b.ToArray();
        }

        /// <summary>Quadro com preâmbulos 0xFF (linha física FSK/serial).</summary>
        public byte[] EncodeWithPreambles(int preambles)
        {
            var body = Encode();
            var b = new byte[preambles + body.Length];
            for (int i = 0; i < preambles; i++) b[i] = 0xFF;
            Buffer.BlockCopy(body, 0, b, preambles, body.Length);
            return b;
        }

        public static byte Checksum(IList<byte> b, int start, int count)
        {
            byte c = 0;
            for (int i = start; i < start + count; i++) c ^= b[i];
            return c;
        }

        /// <summary>
        /// Decodifica um quadro que começa no delimitador (sem preâmbulos). Retorna null se o tamanho ou o checksum
        /// não conferem.
        /// </summary>
        public static HartFrame Decode(IList<byte> b, int start, out int length)
        {
            length = 0;
            if (b.Count - start < 1) return null;
            byte delim = b[start];
            int type = delim & 0x07;
            if (type != 1 && type != 2 && type != 6) return null;
            bool lng = (delim & 0x80) != 0;
            int exp = (delim >> 5) & 0x03;
            int addrLen = lng ? 5 : 1;
            int hdr = 1 + addrLen + exp + 2;
            if (b.Count - start < hdr) return null;
            int bc = b[start + hdr - 1];
            int total = hdr + bc + 1;
            if (b.Count - start < total) return null;
            if (Checksum(b, start, total - 1) != b[start + total - 1]) { length = -1; return null; }
            length = total;
            var arr = new byte[total];
            for (int i = 0; i < total; i++) arr[i] = b[start + i];
            return new HartFrame
            {
                Type = (HartFrameType)type,
                LongAddress = lng,
                AddressBytes = arr.Skip(1).Take(addrLen).ToArray(),
                Expansion = arr.Skip(1 + addrLen).Take(exp).ToArray(),
                Command = arr[1 + addrLen + exp],
                Data = arr.Skip(hdr).Take(bc).ToArray(),
            };
        }

        public static HartFrame Decode(byte[] pdu)
        {
            var f = Decode(pdu, 0, out int len);
            return f != null && len == pdu.Length ? f : null;
        }

        public override string ToString() =>
            $"{Type} {(LongAddress ? "long" : "short")} {Hex.Of(AddressBytes)} cmd {Command} [{Data.Length}] {Hex.Of(Data)}";
    }

    /// <summary>
    /// Extrai quadros de um fluxo de bytes da linha (com preâmbulos), tolerando lixo antes dos preâmbulos,
    /// eco do próprio pedido e quadros de burst.
    /// </summary>
    public sealed class HartFrameParser
    {
        private readonly List<byte> _buf = new List<byte>();
        private readonly int _minPreambles;

        public HartFrameParser(int minPreambles = 2) { _minPreambles = minPreambles; }

        public int ChecksumErrors { get; private set; }
        public int Buffered => _buf.Count;
        /// <summary>Há bytes de um quadro em andamento (preâmbulo ou delimitador já recebidos).</summary>
        public bool InFrame => _buf.Count > 0;

        public void Clear() => _buf.Clear();

        public void Append(byte[] data, int count)
        {
            for (int i = 0; i < count; i++) _buf.Add(data[i]);
        }

        public HartFrame TryRead()
        {
            while (true)
            {
                int i = 0;
                while (i < _buf.Count && _buf[i] == 0xFF) i++;
                if (i >= _buf.Count)
                {
                    // Só preâmbulos (ou nada): mantém até 30 deles.
                    if (_buf.Count > 30) _buf.RemoveRange(0, _buf.Count - 30);
                    return null;
                }
                if (i < _minPreambles) { _buf.RemoveRange(0, i + 1); continue; } // lixo
                var f = HartFrame.Decode(_buf, i, out int len);
                if (f == null)
                {
                    if (len < 0) { ChecksumErrors++; _buf.RemoveRange(0, i + 1); continue; }
                    int type = _buf[i] & 0x07;
                    if (type != 1 && type != 2 && type != 6) { _buf.RemoveRange(0, i + 1); continue; }
                    return null; // incompleto
                }
                _buf.RemoveRange(0, i + len);
                return f;
            }
        }
    }

    public static class Hex
    {
        public static string Of(IEnumerable<byte> b) => b == null ? "" : string.Concat(b.Select(x => x.ToString("X2")));

        public static byte[] Parse(string h)
        {
            h = (h ?? "").Replace(" ", "").Replace("-", "");
            if (h.Length % 2 != 0) throw new FormatException("Hexadecimal com número ímpar de dígitos.");
            var r = new byte[h.Length / 2];
            for (int i = 0; i < r.Length; i++) r[i] = Convert.ToByte(h.Substring(i * 2, 2), 16);
            return r;
        }
    }
}
