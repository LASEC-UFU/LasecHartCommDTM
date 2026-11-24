using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using HartEngine;
using Xunit;

namespace HartEngine.Tests
{
    public class HartFrameTests
    {
        [Fact]
        public void PedidoCurtoDoComando0_TemOsBytesDaLinha()
        {
            var f = HartFrame.Request(HartAddress.Short(0), primaryMaster: true, command: 0, data: null);
            // delimitador STX curto, endereço (mestre primário), comando 0, 0 bytes, checksum
            Assert.Equal("0280000082", Hex.Of(f.Encode()));
            Assert.Equal("FFFFFFFFFF0280000082", Hex.Of(f.EncodeWithPreambles(5)));
        }

        [Fact]
        public void PedidoLongo_UsaEnderecoUnicoComBitDeMestre()
        {
            var a = HartAddress.Long(0x3E, 0x02, 0x000001);
            var f = HartFrame.Request(a, primaryMaster: true, command: 13, data: null);
            Assert.Equal("82BE020000010D0032", Hex.Of(f.Encode()));
            var sec = HartFrame.Request(a, primaryMaster: false, command: 13, data: null);
            Assert.Equal(0x3E, sec.AddressBytes[0]);
        }

        [Fact]
        public void Parser_IgnoraLixoEcoEBurst_EEntregaAResposta()
        {
            var req = HartFrame.Request(HartAddress.Short(0), true, 0, null);
            var resp = new HartFrame { Type = HartFrameType.Ack, LongAddress = false, AddressBytes = new byte[] { 0x80 }, Command = 0, Data = new byte[] { 0, 0, 254, 62, 2, 5, 5, 1, 1, 8, 0, 0, 0, 1 } };
            var burst = new HartFrame { Type = HartFrameType.Back, LongAddress = false, AddressBytes = new byte[] { 0x40 }, Command = 3, Data = new byte[] { 0, 0 } };
            var stream = new List<byte> { 0x00, 0x13, 0xFF };                         // lixo
            stream.AddRange(req.EncodeWithPreambles(5));                              // eco do pedido
            stream.AddRange(burst.EncodeWithPreambles(5));
            stream.AddRange(resp.EncodeWithPreambles(7));
            var p = new HartFrameParser();
            // chega em pedaços, como de um adaptador USB/Bluetooth
            var got = new List<HartFrame>();
            foreach (var chunk in stream.Select((b, i) => new { b, i }).GroupBy(x => x.i / 3, x => x.b))
            {
                var arr = chunk.ToArray();
                p.Append(arr, arr.Length);
                HartFrame f;
                while ((f = p.TryRead()) != null) got.Add(f);
            }
            Assert.Equal(new[] { HartFrameType.Stx, HartFrameType.Back, HartFrameType.Ack }, got.Select(f => f.Type));
            var ack = got.Last();
            Assert.True(HartMaster.Matches(req, ack));
            var id = HartIdentity.FromCommand0(ack.Payload);
            Assert.Equal(62, id.ManufacturerOrTypeHigh);
            Assert.Equal(2, id.DeviceType);
            Assert.Equal(1u, id.DeviceId);
            Assert.Equal("3E02000001", Hex.Of(id.UniqueAddress.Unique));
        }

        [Fact]
        public void Parser_DescartaChecksumErrado()
        {
            var resp = new HartFrame { Type = HartFrameType.Ack, AddressBytes = new byte[] { 0x80 }, Command = 1, Data = new byte[] { 0, 0, 1 } }.EncodeWithPreambles(5);
            resp[resp.Length - 1] ^= 0xFF;
            var p = new HartFrameParser();
            p.Append(resp, resp.Length);
            Assert.Null(p.TryRead());
            Assert.Equal(1, p.ChecksumErrors);
        }

        [Fact]
        public void Resposta_NaoCasaComOutroEnderecoOuComando()
        {
            var req = HartFrame.Request(HartAddress.Long(0x3E, 2, 1), true, 13, null);
            var other = new HartFrame { Type = HartFrameType.Ack, LongAddress = true, AddressBytes = new byte[] { 0xBE, 2, 0, 0, 2 }, Command = 13, Data = new byte[] { 0, 0 } };
            var wrongCmd = new HartFrame { Type = HartFrameType.Ack, LongAddress = true, AddressBytes = new byte[] { 0xBE, 2, 0, 0, 1 }, Command = 12, Data = new byte[] { 0, 0 } };
            var secondary = new HartFrame { Type = HartFrameType.Ack, LongAddress = true, AddressBytes = new byte[] { 0x3E, 2, 0, 0, 1 }, Command = 13, Data = new byte[] { 0, 0 } };
            Assert.False(HartMaster.Matches(req, other));
            Assert.False(HartMaster.Matches(req, wrongCmd));
            Assert.False(HartMaster.Matches(req, secondary));
        }

        [Fact]
        public void PackedAscii_IdaEVolta()
        {
            var b = HartText.PackAscii("TT-201", 8);
            Assert.Equal(6, b.Length);
            Assert.Equal("TT-201", HartText.UnpackAscii(b, 0, 6));
        }

        [Fact]
        public void Comando84_DecodificaSubDispositivo()
        {
            var p = new List<byte> { 0, 1, 0, 2, 0x00, 0x26, 0x26, 0x10, 0x0A, 0x00, 0x01, 7 };
            p.AddRange(System.Text.Encoding.ASCII.GetBytes("TT-201".PadRight(32)));
            var d = HartSubDevice.FromCommand84(p.ToArray());
            Assert.Equal(1, d.Index);
            Assert.Equal(2, d.Channel);
            Assert.Equal(0x2610, d.ExpandedDeviceType);
            Assert.Equal(0x0A0001u, d.DeviceId);
            Assert.Equal("TT-201", d.LongTag);
            Assert.Equal("26100A0001", Hex.Of(d.UniqueAddress.Unique));
        }
    }

    public class HartLinkSettingsTests
    {
        [Fact]
        public void Propriedades_IdaEVolta_ComTiposDoPropertyBag()
        {
            var s = new HartLinkSettings { Interface = HartInterface.HartIpUdp, Port = "COM7", HostAddress = "10.0.0.5", HostPort = 5094, PrimaryMaster = false, RtsControl = RtsControl.Off };
            var back = new HartLinkSettings();
            foreach (var kv in s.ToProperties()) Assert.True(back.TrySet(kv.Key, kv.Value), kv.Key);
            Assert.Equal(s.ToText(), back.ToText());
            Assert.Equal(s.ToText(), HartLinkSettings.FromText(s.ToText()).ToText());
            // VARIANTs de outros tipos (ex.: Int16, VARIANT_BOOL -1) também são aceitos
            Assert.True(back.TrySet("Baudrate", (short)2400));
            Assert.True(back.TrySet("DtrEnabled", -1));
            Assert.Equal(2400, back.Baudrate);
            Assert.True(back.DtrEnabled);
            Assert.False(back.TrySet("Desconhecida", 1));
        }

        [Fact]
        public void Validacao_RecusaConfiguracaoInvalida()
        {
            Assert.Null(new HartLinkSettings().Validate());
            Assert.NotNull(new HartLinkSettings { Port = "" }.Validate());
            Assert.NotNull(new HartLinkSettings { Interface = HartInterface.HartIpTcp, HostAddress = "" }.Validate());
            Assert.NotNull(new HartLinkSettings { ScanStart = 5, ScanStop = 2 }.Validate());
        }
    }

    /// <summary>HART-IP contra um servidor mínimo local (127.0.0.1) escrito no próprio teste.</summary>
    public class HartIpTests
    {
        private static byte[] Reply(byte[] req, byte[] body)
        {
            int len = 8 + body.Length;
            var r = new byte[len];
            r[0] = 1; r[1] = 1; r[2] = req[2]; r[3] = 0; r[4] = req[4]; r[5] = req[5]; r[6] = (byte)(len >> 8); r[7] = (byte)len;
            Buffer.BlockCopy(body, 0, r, 8, body.Length);
            return r;
        }

        [Fact]
        public void Udp_SessaoNovaPorta_PduComando0_RespostaAtrasada()
        {
            var main = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            int port = ((IPEndPoint)main.Client.LocalEndPoint).Port;
            var seen = new List<string>();
            int pdus = 0;
            var server = new Thread(() =>
            {
                var from = new IPEndPoint(IPAddress.Any, 0);
                var init = main.Receive(ref from);
                lock (seen) seen.Add($"init id={init[2]} master={init[8]} timer={(init[9] << 24) | (init[10] << 16) | (init[11] << 8) | init[12]}");
                using (var session = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))  // sessão continua noutra porta
                {
                    session.Send(Reply(init, init.Skip(8).ToArray()), 13, from);
                    session.Client.ReceiveTimeout = 5000;
                    while (true)
                    {
                        byte[] m;
                        try { m = session.Receive(ref from); } catch { return; }
                        if (m[2] == 1) { session.Send(Reply(m, new byte[0]), 8, from); return; }
                        if (m[2] != 3) continue;
                        pdus++;
                        // 1º pedido: resposta atrasada iniciada (33); 2º: resposta do comando 0
                        var data = pdus == 1 ? new byte[] { 33, 0 } : new byte[] { 0, 0, 254, 0x26, 0x10, 5, 7, 1, 1, 8, 0, 0x0A, 0, 1 };
                        var pdu = new List<byte> { 0x86, 0xA6, 0x10, 0x0A, 0x00, 0x01, 0, (byte)data.Length };
                        pdu.AddRange(data);
                        pdu.Add(pdu.Aggregate((byte)0, (a, b) => (byte)(a ^ b)));
                        session.Send(Reply(m, pdu.ToArray()), 8 + pdu.Count, from);
                    }
                }
            }) { IsBackground = true };
            server.Start();

            var log = new HartCommLog();
            var s = new HartLinkSettings { Interface = HartInterface.HartIpUdp, HostAddress = "127.0.0.1", HostPort = port, ResponseTimeoutMs = 2000, InactivityTimeoutMs = 30000 };
            using (var master = new HartMaster(HartTransports.Create(s, log), s, log))
            {
                master.Open();
                var id = master.Identify(HartAddress.Long(0x26, 0x10, 0x0A0001), CancellationToken.None, out string err);
                Assert.True(id != null, err + " | " + string.Join(" | ", log.Snapshot()));
                Assert.Equal(0x0A0001u, id.DeviceId);
                Assert.Equal(7, id.UniversalRevision);
                Assert.Equal(2, pdus); // reenviado após o código 33
            }
            Assert.True(server.Join(5000));
            Assert.Equal("init id=0 master=1 timer=30000", seen.Single());
            main.Close();
        }

        [Fact]
        public void ServidorAusente_FalhaComMensagemClara()
        {
            var s = new HartLinkSettings { Interface = HartInterface.HartIpTcp, HostAddress = "127.0.0.1", HostPort = 1, ResponseTimeoutMs = 1000 };
            var t = HartTransports.Create(s, new HartCommLog());
            var ex = Assert.Throws<HartTransportException>(() => t.Open());
            Assert.Contains("127.0.0.1:1", ex.Message);
        }
    }
}
