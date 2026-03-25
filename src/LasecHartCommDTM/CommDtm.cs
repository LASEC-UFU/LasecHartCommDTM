using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using LasecHartCommDTM.FdtInterfaces;
using HartEngine;

namespace LasecHartCommDTM
{
    [Guid("B4B6B3E7-639D-460B-B9A0-6C7F7EB20010")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class CommDtm : IDtmInformation, IDtm, IFdtCommunication
    {
        private static CommDtm _current;
        internal static CommDtm Current => _current;

        private ChannelManager _manager;
        private IFdtCommunicationEvents _callback;
        private bool _connected;

        private string _mode      = "udp";
        private string _serialPort = "COM1";
        private string _udpHost   = "127.0.0.1";
        private int    _udpPort   = 5094;
        private int    _baud      = 1200;

        public CommDtm()
        {
            Log("CommDtm() constructor called");
            _manager = new ChannelManager();
            _current = this;
        }

        private static void Log(string msg)
        {
            try
            {
                var path = @"C:\ProgramData\PACTware Consortium e.V\PACTware 5.0\LasecHartDTM.log";
                var line = DateTime.Now.ToString("HH:mm:ss.fff") + " [" + System.Diagnostics.Process.GetCurrentProcess().ProcessName + "] " + msg;
                System.IO.File.AppendAllText(path, line + System.Environment.NewLine);
            }
            catch { }
        }

        // ----------------------------------------------------------------
        // IDtmInformation — chamado pelo PACTware durante o scan do catálogo
        // ----------------------------------------------------------------
        public string GetInformation()
        {
            Log("GetInformation() called");
            // HART bus category GUID per PWFDT.INI
            const string hartBusCategoryId = "036D1498-387B-11D4-86E1-00E0987270B9";

            return
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<FDT xmlns=\"x-schema:DTMInformationSchema.xml\" xmlns:fdt=\"x-schema:FDTDataTypesSchema.xml\">" +
                  "<DtmInfo>" +
                    "<FDTVersion major=\"1\" minor=\"2\"/>" +
                    "<fdt:VersionInformation" +
                    "  name=\"Lasec HART Communication DTM\"" +
                    "  vendor=\"JosueLab\"" +
                    "  version=\"1.0.0\"" +
                    "  date=\"2026-03-24\"/>" +
                    "<DtmDeviceTypes>" +
                      "<fdt:DtmDeviceType>" +
                        "<fdt:VersionInformation" +
                        "  name=\"Lasec HART Communication DTM\"" +
                        "  vendor=\"JosueLab\"" +
                        "  version=\"1.0.0\"" +
                        "  date=\"2026-03-24\"/>" +
                        "<fdt:SupportedLanguages>" +
                          "<fdt:LanguageId languageId=\"1033\"/>" +
                        "</fdt:SupportedLanguages>" +
                        "<fdt:BusCategories>" +
                          "<fdt:BusCategory" +
                          "  busCategory=\"" + hartBusCategoryId + "\"" +
                          "  busCategoryName=\"HART\">" +
                            "<fdt:CommunicationTypeEntry communicationType=\"supported\"/>" +
                          "</fdt:BusCategory>" +
                        "</fdt:BusCategories>" +
                      "</fdt:DtmDeviceType>" +
                    "</DtmDeviceTypes>" +
                  "</DtmInfo>" +
                "</FDT>";
        }

        // ----------------------------------------------------------------
        // IDtm — interface base FDT 1.x
        // ----------------------------------------------------------------
        public bool Environment(string systemTag, IFdtContainer container) => true;
        public bool InitNew(string deviceType) => true;
        public bool Config(string userInfo) => true;

        public bool SetCommunication(IFdtCommunication communication) => true;

        public bool PrepareToRelease() { _connected = false; return true; }
        public bool PrepareToReleaseCommunication() { _connected = false; return true; }
        public bool ReleaseCommunication() { _manager?.Dispose(); _connected = false; return true; }
        public bool PrepareToDelete() => true;
        public bool SetLanguage(int languageId) => true;

        public string GetFunctions(string operationState)
        {
            return
                "<FDTFunctions xmlns=\"x-schema:DTMFunctionsSchema.xml\">" +
                  "<fdt:Function" +
                  "  functionId=\"Configure\"" +
                  "  label=\"Configure\"" +
                  "  xmlns:fdt=\"x-schema:FDTDataTypesSchema.xml\"/>" +
                "</FDTFunctions>";
        }

        public bool InvokeFunctionRequest(string invokeId, string functionCall) => true;
        public bool PrivateDialogEnabled(bool enabled) => true;

        // ----------------------------------------------------------------
        // IFdtCommunication — canal de comunicação HART
        // ----------------------------------------------------------------
        public void Abort(string fieldbusFrame)
        {
            _manager?.Dispose();
            _connected = false;
        }

        public bool ConnectRequest(IFdtCommunicationEvents callBack, string invokeId, string protocolId, string fieldbusFrame)
        {
            try
            {
                _callback = callBack;
                _manager.Configure(_mode, _serialPort, _udpHost, _udpPort, _baud);
                _connected = true;

                callBack?.OnConnectResponse(invokeId,
                    "<FDTConnectResponse xmlns=\"x-schema:FDTConnectResponseSchema.xml\">" +
                    "<fdt:ConnectResponse result=\"success\" xmlns:fdt=\"x-schema:FDTDataTypesSchema.xml\"/>" +
                    "</FDTConnectResponse>");
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool DisconnectRequest(string invokeId, string fieldbusFrame)
        {
            _manager?.Dispose();
            _connected = false;
            _callback?.OnDisconnectResponse(invokeId, string.Empty);
            return true;
        }

        public bool TransactionRequest(string invokeId, string fieldbusFrame)
        {
            try
            {
                if (!_connected) return false;

                // Extrai frame HART do XML e executa a transação
                var frame = HartXmlHelper.ParseRequestFrame(fieldbusFrame);
                var response = _manager.SendAndReceive(frame, 1000);
                var responseXml = HartXmlHelper.BuildResponseXml(response);

                _callback?.OnTransactionResponse(invokeId, responseXml);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public string GetSupportedProtocols()
        {
            const string hartBusCategoryId = "036D1498-387B-11D4-86E1-00E0987270B9";
            return
                "<FDTProtocols xmlns=\"x-schema:DTMProtocolsSchema.xml\">" +
                  "<fdt:Protocol" +
                  "  protocolId=\"" + hartBusCategoryId + "\"" +
                  "  xmlns:fdt=\"x-schema:FDTDataTypesSchema.xml\"/>" +
                "</FDTProtocols>";
        }

        public bool SequenceBegin(string fieldbusFrame) => true;
        public bool SequenceStart(string fieldbusFrame) => true;
        public bool SequenceEnd(string fieldbusFrame) => true;

        // Chamado pela UI (DtmView) para configurar parâmetros do canal
        public void Configure(string mode, string serialPort, string udpHost, int udpPort, int baud)
        {
            _mode       = mode?.ToLowerInvariant() ?? "udp";
            _serialPort = serialPort ?? "COM1";
            _udpHost    = string.IsNullOrEmpty(udpHost) || udpHost.Trim().Length == 0 ? "127.0.0.1" : udpHost;
            _udpPort    = udpPort <= 0 ? 5094 : udpPort;
            _baud       = baud <= 0 ? 1200 : baud;
        }

        // ----------------------------------------------------------------
        // Registro FDT DTM no COM (32-bit)
        // ----------------------------------------------------------------
        private const string FdtDtmCategoryId = "{036D1490-387B-11D4-86E1-00E0987270B9}";

        [ComRegisterFunction]
        public static void Register(Type t)
        {
            try
            {
                string clsidKeyPath = @"CLSID\" + t.GUID.ToString("B");

                // x86 process: registry is automatically redirected to 32-bit view
                using (var clsidKey = Registry.ClassesRoot.OpenSubKey(clsidKeyPath, true))
                {
                    if (clsidKey != null)
                        clsidKey.CreateSubKey(@"Implemented Categories\" + FdtDtmCategoryId);
                }

                using (var catKey = Registry.ClassesRoot.CreateSubKey(@"Component Categories\" + FdtDtmCategoryId))
                {
                    if (catKey != null)
                        catKey.SetValue(null, "FDT DTM");
                }
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Erro ao registrar categoria FDT DTM: " + ex.Message);
            }
        }

        [ComUnregisterFunction]
        public static void Unregister(Type t)
        {
            try
            {
                string clsidKeyPath = @"CLSID\" + t.GUID.ToString("B");

                using (var clsidKey = Registry.ClassesRoot.OpenSubKey(clsidKeyPath, true))
                {
                    if (clsidKey != null)
                        try { clsidKey.DeleteSubKeyTree(@"Implemented Categories\" + FdtDtmCategoryId); } catch { }
                }
            }
            catch { }
        }
    }
}
