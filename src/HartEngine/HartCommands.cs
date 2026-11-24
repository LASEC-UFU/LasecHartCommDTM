using System;
using System.Linq;
using System.Text;

namespace HartEngine
{
    /// <summary>Resposta do comando 0 (Read Unique Identifier). Layout da especificação HART (rev. 5 a 7).</summary>
    public sealed class HartIdentity
    {
        /// <summary>Byte 1: fabricante (HART 5) ou byte alto do tipo expandido (HART 6/7).</summary>
        public byte ManufacturerOrTypeHigh { get; set; }
        public byte DeviceType { get; set; }
        /// <summary>Número mínimo de preâmbulos mestre → escravo pedido pelo instrumento.</summary>
        public byte RequestPreambles { get; set; }
        public byte UniversalRevision { get; set; }
        public byte DeviceRevision { get; set; }
        public byte SoftwareRevision { get; set; }
        public byte HardwareRevision { get; set; }
        public byte Flags { get; set; }
        public uint DeviceId { get; set; }
        /// <summary>HART 7: código de fabricante de 16 bits (0 se não informado).</summary>
        public ushort ManufacturerId16 { get; set; }
        public byte PollingAddress { get; set; }
        public string Tag { get; set; } = "";

        public HartAddress UniqueAddress => HartAddress.Long(ManufacturerOrTypeHigh, DeviceType, DeviceId);

        /// <summary>Tipo expandido de 16 bits (HART 6/7).</summary>
        public ushort ExpandedDeviceType => (ushort)((ManufacturerOrTypeHigh << 8) | DeviceType);

        public static HartIdentity FromCommand0(byte[] payload)
        {
            if (payload == null || payload.Length < 12) return null;
            var id = new HartIdentity
            {
                ManufacturerOrTypeHigh = payload[1],
                DeviceType = payload[2],
                RequestPreambles = payload[3],
                UniversalRevision = payload[4],
                DeviceRevision = payload[5],
                SoftwareRevision = payload[6],
                HardwareRevision = (byte)(payload[7] >> 3),
                Flags = payload[8],
                DeviceId = (uint)((payload[9] << 16) | (payload[10] << 8) | payload[11]),
            };
            if (id.UniversalRevision >= 7 && payload.Length >= 19) id.ManufacturerId16 = (ushort)((payload[17] << 8) | payload[18]);
            return id;
        }

        public override string ToString() =>
            $"fabr/tipo {ManufacturerOrTypeHigh}/{DeviceType} id {DeviceId:X6} rev univ {UniversalRevision} disp {DeviceRevision}" +
            (Tag.Length > 0 ? $" tag '{Tag}'" : "");
    }

    /// <summary>Sub-dispositivo de um sistema de E/S (gateway WirelessHART, multiplexador) — comando 84.</summary>
    public sealed class HartSubDevice
    {
        public ushort Index { get; set; }
        public byte IoCard { get; set; }
        public byte Channel { get; set; }
        public ushort ManufacturerId { get; set; }
        public ushort ExpandedDeviceType { get; set; }
        public uint DeviceId { get; set; }
        public byte UniversalRevision { get; set; }
        public string LongTag { get; set; } = "";

        public HartAddress UniqueAddress => HartAddress.Long((byte)(ExpandedDeviceType >> 8), (byte)ExpandedDeviceType, DeviceId);

        /// <summary>
        /// Comando 84 (Read Sub-Device Identity Summary): índice (2), cartão de E/S (1), canal (1), fabricante (2),
        /// tipo expandido (2), identificação (3), revisão universal (1), tag longo (32, ISO Latin-1)…
        /// </summary>
        public static HartSubDevice FromCommand84(byte[] p)
        {
            if (p == null || p.Length < 12) return null;
            var d = new HartSubDevice
            {
                Index = (ushort)((p[0] << 8) | p[1]),
                IoCard = p[2],
                Channel = p[3],
                ManufacturerId = (ushort)((p[4] << 8) | p[5]),
                ExpandedDeviceType = (ushort)((p[6] << 8) | p[7]),
                DeviceId = (uint)((p[8] << 16) | (p[9] << 8) | p[10]),
                UniversalRevision = p[11],
            };
            if (p.Length >= 44) d.LongTag = HartText.Latin1(p, 12, 32);
            return d;
        }
    }

    public static class HartResponseCodes
    {
        public const byte Success = 0;
        public const byte NotImplemented = 64;
        public const byte Busy = 32;
        public const byte DelayedResponseInitiated = 33;
        public const byte DelayedResponseRunning = 34;
        public const byte DelayedResponseDead = 35;
        public const byte DelayedResponseConflict = 36;
    }

    public static class HartText
    {
        /// <summary>Packed ASCII HART: 4 caracteres em 3 bytes (6 bits cada).</summary>
        public static string UnpackAscii(byte[] b, int start, int count)
        {
            var sb = new StringBuilder();
            for (int i = start; i + 2 < start + count && i + 2 < b.Length; i += 3)
            {
                int v = (b[i] << 16) | (b[i + 1] << 8) | b[i + 2];
                for (int k = 3; k >= 0; k--)
                {
                    int c = (v >> (6 * k)) & 0x3F;
                    sb.Append((char)(c < 0x20 ? c + 0x40 : c));
                }
            }
            return sb.ToString().TrimEnd(' ', '\0');
        }

        public static byte[] PackAscii(string s, int chars)
        {
            s = (s ?? "").ToUpperInvariant().PadRight(chars).Substring(0, chars);
            var r = new byte[chars * 3 / 4];
            for (int i = 0, o = 0; i < chars; i += 4, o += 3)
            {
                int v = 0;
                for (int k = 0; k < 4; k++) v = (v << 6) | (s[i + k] & 0x3F);
                r[o] = (byte)(v >> 16); r[o + 1] = (byte)(v >> 8); r[o + 2] = (byte)v;
            }
            return r;
        }

        public static string Latin1(byte[] b, int start, int count) =>
            Encoding.GetEncoding(28591).GetString(b, start, Math.Min(count, b.Length - start)).TrimEnd(' ', '\0');
    }
}
