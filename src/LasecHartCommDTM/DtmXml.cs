using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Text;
using System.Xml;
using HartEngine;

namespace LasecHartCommDTM
{
    /// <summary>
    /// Documentos XML trocados com o Frame e com os DTMs de instrumento (esquemas XDR do FDT 1.2:
    /// DTMInformationSchema, DTMFunctionsSchema, DTMParameterSchema, FDTBasicChannelParameterSchema,
    /// DTMProtocolsSchema, FDTHARTCommunicationSchema, DTMTopologyScanSchema).
    /// </summary>
    internal static class DtmXml
    {
        public const string HartProtocolId = "036D1498-387B-11D4-86E1-00E0987270B9"; // CATID_FDT_HART / BusCategory HART
        public const string FdtNs = "x-schema:FDTDataTypesSchema.xml";

        public const string DtmName = "Lasec HART Communication";
        public const string Vendor = "JosueLab";
        public const string Version = "2.0.0";
        public const string Date = "2026-11-24";

        public const int FunctionConfiguration = 1;
        public const int FunctionLog = 10;
        public const int FunctionScan = 20;
        public const int FunctionAbout = 100;

        public static bool IsPortuguese(int lcid) => (lcid & 0x3FF) == 0x16; // pt-BR 1046, pt-PT 2070

        public static string E(string s) => SecurityElement.Escape(s ?? "");

        // ------------------------------------------------------------ informação / parâmetros

        public static string DtmDeviceType(int readAccess = 1) =>
            $"<fdt:DtmDeviceType readAccess=\"{readAccess}\" writeAccess=\"0\" deviceTypeInformation=\"HART Communication FDT 1.2 DTM (modem serial/USB/Bluetooth, HART-IP, WirelessHART)\">" +
            $"<fdt:VersionInformation name=\"{DtmName}\" vendor=\"{Vendor}\" version=\"{Version}\" date=\"{Date}\"/>" +
            "<fdt:SupportedLanguages><fdt:LanguageId languageId=\"1033\"/><fdt:LanguageId languageId=\"1046\"/></fdt:SupportedLanguages>" +
            $"<fdt:BusCategories><fdt:BusCategory busCategory=\"{HartProtocolId}\" busCategoryName=\"HART\"><fdt:CommunicationTypeEntry communicationType=\"supported\"/></fdt:BusCategory></fdt:BusCategories>" +
            "</fdt:DtmDeviceType>";

        public static string Information() =>
            "<?xml version=\"1.0\"?>" +
            $"<FDT xmlns=\"x-schema:DTMInformationSchema.xml\" xmlns:fdt=\"{FdtNs}\"><DtmInfo>" +
            "<FDTVersion major=\"1\" minor=\"2\"/>" +
            $"<fdt:VersionInformation name=\"{DtmName}\" vendor=\"{Vendor}\" version=\"{Version}\" date=\"{Date}\"/>" +
            $"<DtmDeviceTypes>{DtmDeviceType()}</DtmDeviceTypes></DtmInfo></FDT>";

        public static string Parameters(string systemTag) =>
            "<?xml version=\"1.0\"?>" +
            $"<FDT xmlns=\"x-schema:DTMParameterSchema.xml\" xmlns:fdt=\"{FdtNs}\" fdt:storageState=\"persistent\" fdt:dataSetState=\"allDataLoaded\">" +
            DtmDeviceType(0) +
            $"<DtmDevice fdt:tag=\"{E(systemTag)}\"/></FDT>";

        // ------------------------------------------------------------ funções

        public static string Functions(int lcid)
        {
            bool pt = IsPortuguese(lcid);
            string Fn(int id, string name, string label, string help) =>
                $"<Function label=\"{E(label)}\" fdt:name=\"{name}\" help=\"{E(help)}\" hasGUI=\"1\" functionId=\"{id}\">" +
                "<Status toggle=\"0\" checked=\"0\" enabled=\"1\" hidden=\"0\" separator=\"0\"/></Function>";
            return "<?xml version=\"1.0\"?>" +
                   $"<FDT xmlns=\"x-schema:DTMFunctionsSchema.xml\" xmlns:fdt=\"{FdtNs}\" xmlns:appId=\"x-schema:FDTApplicationIdSchema.xml\">" +
                   $"<Functions label=\"{(pt ? "Funções" : "Functions")}\" fdt:name=\"Functions\" help=\"{(pt ? "Funções do DTM de comunicação HART" : "HART communication DTM functions")}\">" +
                   $"<StandardFunction fdt:name=\"Configuration\" help=\"{(pt ? "Interface, porta e parâmetros do mestre HART" : "Interface, port and HART master settings")}\" functionId=\"{FunctionConfiguration}\">" +
                   "<appId:ApplicationId applicationId=\"fdtConfiguration\"/></StandardFunction>" +
                   Fn(FunctionScan, "Scan", pt ? "Instrumentos na rede" : "Devices on the network", pt ? "Procura instrumentos (somente leitura)" : "Searches for devices (read only)") +
                   Fn(FunctionLog, "Log", pt ? "Log de comunicação" : "Communication log", pt ? "Quadros HART enviados e recebidos" : "HART frames sent and received") +
                   Fn(FunctionAbout, "About", pt ? "Sobre" : "About", pt ? "Versão do DTM" : "DTM version") +
                   "</Functions></FDT>";
        }

        /// <summary>functionId de um functionCall (DTMFunctionCallSchema); applicationId padrão → função equivalente.</summary>
        public static int FunctionIdOf(string functionCall)
        {
            try
            {
                var doc = Load(functionCall);
                var call = Elements(doc, "FDTFunctionCall").FirstOrDefault();
                if (call == null) return -1;
                var idAttr = call.Attributes.Cast<XmlAttribute>().FirstOrDefault(a => a.LocalName == "functionId");
                if (idAttr != null && int.TryParse(idAttr.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)) return id;
                var app = Elements(call, "ApplicationId").FirstOrDefault()?.GetAttribute("applicationId");
                return app == "fdtConfiguration" ? FunctionConfiguration : -1;
            }
            catch { return -1; }
        }

        // ------------------------------------------------------------ canal

        public static string ChannelParameters(string channelTag) =>
            "<?xml version=\"1.0\"?>" +
            $"<FDT xmlns=\"x-schema:FDTBasicChannelParameterSchema.xml\" xmlns:fdt=\"{FdtNs}\">" +
            $"<FDTChannelType gatewayBusCategory=\"{HartProtocolId}\"><fdt:VersionInformation name=\"{DtmName}\" vendor=\"{Vendor}\" version=\"{Version}\"/></FDTChannelType>" +
            $"<FDTBasicChannel fdt:tag=\"{E(channelTag)}\" fdt:id=\"{E(channelTag)}\"/></FDT>";

        public static string SupportedProtocols() =>
            "<?xml version=\"1.0\"?>" +
            $"<FDT xmlns=\"x-schema:DTMProtocolsSchema.xml\" xmlns:fdt=\"{FdtNs}\">" +
            $"<fdt:BusCategories><fdt:BusCategory busCategory=\"{HartProtocolId}\" busCategoryName=\"HART\"/></fdt:BusCategories></FDT>";

        // ------------------------------------------------------------ comunicação HART (FDTHARTCommunicationSchema)

        private static string Hart(string inner) =>
            $"<?xml version=\"1.0\"?><FDT xmlns=\"x-schema:FDTHARTCommunicationSchema.xml\" xmlns:fdt=\"{FdtNs}\">{inner}</FDT>";

        public static string ConnectResponse(string tag, int preambles, bool primaryMaster, string commRef, HartAddress unique, byte pollingAddress) =>
            Hart($"<ConnectResponse fdt:tag=\"{E(tag)}\" preambleCount=\"{preambles}\" primaryMaster=\"{(primaryMaster ? 1 : 0)}\" communicationReference=\"{commRef}\">" +
                 LongAddress(unique) +
                 $"<ShortAddress shortAddress=\"{pollingAddress}\"/></ConnectResponse>");

        public static string LongAddress(HartAddress a) =>
            $"<LongAddress manufacturerId=\"{a.ManufacturerOrTypeHigh}\" deviceTypeId=\"{a.DeviceType}\" address1=\"{a.Unique[2]}\" address2=\"{a.Unique[3]}\" address3=\"{a.Unique[4]}\"/>";

        public static string DisconnectResponse(string commRef) => Hart($"<DisconnectResponse communicationReference=\"{commRef}\"/>");

        public static string DataExchangeResponse(string commRef, int command, HartFrame resp)
        {
            var sb = new StringBuilder();
            sb.Append($"<DataExchangeResponse commandNumber=\"{command}\" communicationReference=\"{commRef}\">");
            var payload = resp.Payload;
            if (payload.Length > 0) sb.Append($"<fdt:CommunicationData byteArray=\"{Hex.Of(payload)}\"/>");
            sb.Append($"<Status deviceStatus=\"{resp.DeviceStatus}\">");
            sb.Append(resp.IsCommunicationError
                ? $"<CommunicationStatus value=\"{resp.ResponseCode}\"/>"
                : $"<CommandResponse value=\"{resp.ResponseCode}\"/>");
            sb.Append("</Status></DataExchangeResponse>");
            return Hart(sb.ToString());
        }

        /// <summary>fdt:CommunicationError (FDTDataTypesSchema). kind ∈ abort busy invalidCommunicationReference noConnection … timeout.</summary>
        public static string CommunicationError(string tag, string kind, string descriptor) =>
            Hart($"<fdt:CommunicationError communicationError=\"{kind}\" tag=\"{E(tag)}\" descriptor=\"{E(descriptor)}\"/>");

        /// <summary>FDTConnectResponseSchema (parentInformation de IFdtCommunicationEvents2::OnConnectResponse2).</summary>
        public static string ParentInformation() =>
            "<?xml version=\"1.0\"?><FDT xmlns=\"x-schema:FDTConnectResponseSchema.xml\" xmlns:fdtvers=\"x-schema:DTMInformationSchema.xml\">" +
            "<ParentVersion><fdtvers:FDTVersion major=\"1\" minor=\"2\" release=\"0\" build=\"0\"/></ParentVersion></FDT>";

        // ------------------------------------------------------------ varredura (DTMTopologyScanSchema)

        public static string TopologyScan(IEnumerable<HartScanEntry> found)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\"?><TopologyScan xmlns=\"x-schema:DTMTopologyScanSchema.xml\" " +
                      "xmlns:fdthart=\"x-schema:FDTHARTCommunicationSchema.xml\" xmlns:fdthartdevice=\"x-schema:DTMHARTDeviceSchema.xml\" " +
                      $"xmlns:fdt=\"{FdtNs}\">");
            foreach (var d in found)
            {
                var a = d.UniqueAddress;
                if (a == null) continue;
                sb.Append($"<fdthartdevice:HARTDevice fdthart:manufacturerId=\"{a.ManufacturerOrTypeHigh}\" fdthart:deviceTypeId=\"{a.DeviceType}\"");
                if (d.PollingAddress.HasValue) sb.Append($" fdthart:shortAddress=\"{d.PollingAddress.Value}\"");
                sb.Append($" fdt:tag=\"{E(d.Tag)}\">");
                // Atributos locais do elemento LongAddress (mesmo esquema) não levam prefixo.
                sb.Append($"<fdthart:LongAddress manufacturerId=\"{a.ManufacturerOrTypeHigh}\" deviceTypeId=\"{a.DeviceType}\" " +
                          $"address1=\"{a.Unique[2]}\" address2=\"{a.Unique[3]}\" address3=\"{a.Unique[4]}\"/>");
                sb.Append("</fdthartdevice:HARTDevice>");
            }
            sb.Append("</TopologyScan>");
            return sb.ToString();
        }

        // ------------------------------------------------------------ leitura

        public static XmlDocument Load(string xml)
        {
            var d = new XmlDocument { XmlResolver = null };
            d.LoadXml(xml ?? "");
            return d;
        }

        public static IEnumerable<XmlElement> Elements(XmlNode root, string localName) =>
            (root is XmlDocument doc ? doc.DocumentElement : root)?.SelectNodes("descendant-or-self::*")?.Cast<XmlElement>()
                .Where(e => e.LocalName == localName) ?? Enumerable.Empty<XmlElement>();

        /// <summary>Atributo pelo nome local, com ou sem prefixo (ex.: "tag" encontra fdt:tag).</summary>
        public static string Attr(XmlElement e, string localName) =>
            e?.Attributes.Cast<XmlAttribute>().FirstOrDefault(a => a.LocalName == localName)?.Value;

        public static bool Bool(string v, bool dflt) =>
            v == null ? dflt : v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);

        public static int Int(string v, int dflt) =>
            int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int r) ? r : dflt;
    }
}
