using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using HartEngine;
using Jigfdt.Fdt100;
using Microsoft.Win32;

namespace LasecHartCommDTM
{
    /// <summary>
    /// Lasec HART Communication DTM — Comm DTM FDT 1.2 (mestre HART), equivalente ao "HART Communication" da
    /// CodeWrights: um canal HART ao qual os DTMs de instrumento se conectam. Meios: modem HART serial (RS-232,
    /// USB, Bluetooth), HART-IP UDP/TCP (gateways WirelessHART, multiplexadores) e ponte UDP.
    ///
    /// Interfaces (FDT 1.2 §4): IDtm, IDtm2, IDtmInformation, IDtmChannel, IDtmParameter, IDtmActiveXInformation,
    /// IFdtEvents; persistência por IPersistPropertyBag e IPersistStreamInit.
    /// </summary>
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    [Guid(ClassId)]
    [ProgId(ProgIdText)]
    public sealed class CommDtm : StandardOleMarshalObject,
        IDtm, IDtm2, IDtmInformation, IDtmChannel, IDtmParameter, IDtmActiveXInformation, IFdtEvents,
        IPersistPropertyBag, IPersistStreamInit
    {
        public const string ClassId = "B4B6B3E7-639D-460B-B9A0-6C7F7EB20010";
        public const string ProgIdText = "JosueLab.LasecHartCommDTM.CommDtm";

        private StaDispatcher _dispatcher; // criado em Environment, na thread do DTM
        private readonly HartChannel _channel;
        private HartLinkSettings _settings = new HartLinkSettings();
        private IFdtContainer _container;
        private bool _dirty;
        private bool _released;

        public CommDtm()
        {
            CommLog = new HartCommLog { FilePath = Path.Combine(Path.GetTempPath(), "LasecHartCommDTM.log") };
            StaDispatcher.OnError = m => CommLog.Error("evento: " + m);
            _channel = new HartChannel(this);
            CommLog.Info($"{DtmXml.DtmName} {DtmXml.Version}: instância criada");
        }

        internal HartCommLog CommLog { get; }
        internal HartChannel Channel => _channel;
        internal string SystemTag { get; private set; } = "";
        internal string GuiLabel { get; private set; }
        internal int Lcid { get; private set; } = 1033;
        internal bool Portuguese => DtmXml.IsPortuguese(Lcid);
        internal bool LinkOnline { get; private set; }
        internal IDtmEvents Events => _container as IDtmEvents;

        /// <summary>Configuração atual (cópia: alterar só por <see cref="ApplySettings"/>).</summary>
        internal HartLinkSettings Settings => _settings.Clone();

        /// <summary>Disparado na thread do DTM quando a configuração ou o estado do meio muda.</summary>
        internal event Action StateChanged;

        internal void Post(Action a)
        {
            if (_dispatcher != null) _dispatcher.Post(a);
            else try { a(); } catch (Exception ex) { CommLog.Error("evento: " + ex.Message); }
        }

        internal void NotifyOnline(bool online)
        {
            Post(() =>
            {
                if (LinkOnline == online) return;
                LinkOnline = online;
                try { Events?.OnOnlineStateChanged(SystemTag, online); } catch (Exception ex) { CommLog.Error("OnOnlineStateChanged: " + ex.Message); }
                StateChanged?.Invoke();
            });
        }

        /// <summary>
        /// Aplica uma nova configuração (vinda da tela). Recusa se houver instrumentos conectados ou se o Frame não
        /// liberar o data set. Retorna null em sucesso ou a mensagem de erro.
        /// </summary>
        internal string ApplySettings(HartLinkSettings s)
        {
            string invalid = s.Validate();
            if (invalid != null) return invalid;
            if (_channel.ConnectionCount > 0)
                return Portuguese ? "Desconecte os instrumentos antes de mudar a configuração." : "Disconnect the devices before changing the configuration.";
            bool locked = false;
            try { locked = _container?.LockDataSet(SystemTag) ?? true; } catch { locked = true; }
            if (!locked) return Portuguese ? "O Frame não liberou o data set para edição." : "The frame did not grant the data set lock.";
            try
            {
                _settings = s.Clone();
                _dirty = true;
                _channel.Reconfigure();
                CommLog.Info("Configuração aplicada: " + _settings.Describe());
                try { Events?.OnParameterChanged(SystemTag, DtmXml.Parameters(SystemTag)); } catch { }
                try { _container?.SaveRequest(SystemTag); } catch (Exception ex) { CommLog.Error("SaveRequest: " + ex.Message); }
            }
            finally
            {
                try { _container?.UnlockDataSet(SystemTag); } catch { }
            }
            StateChanged?.Invoke();
            return null;
        }

        internal void NotifyApplicationClosed(string invokeId) =>
            Post(() => { try { Events?.OnApplicationClosed(invokeId); } catch (Exception ex) { CommLog.Error("OnApplicationClosed: " + ex.Message); } });

        // ================================================================ IDtm

        public bool Environment(string systemTag, IFdtContainer container)
        {
            SystemTag = systemTag ?? "";
            _container = container;
            if (_dispatcher == null) _dispatcher = new StaDispatcher();
            CommLog.Info($"Environment: {SystemTag}");
            return true;
        }

        public bool InitNew(string deviceType) => true;

        public bool Config(string userInformation) => true;

        /// <summary>Comm DTM raiz: não usa comunicação de um pai (o Frame passa NULL).</summary>
        public bool SetCommunication(IFdtCommunication communication) => communication == null;

        public bool PrepareToRelease()
        {
            if (!_released)
            {
                _released = true;
                _channel.Shutdown();
                CommLog.Info("PrepareToRelease");
            }
            Post(() =>
            {
                try { Events?.OnPreparedToRelease(SystemTag); } catch (Exception ex) { CommLog.Error("OnPreparedToRelease: " + ex.Message); }
                _container = null;
                _dispatcher?.Dispose();
                _dispatcher = null;
            });
            return true;
        }

        public bool PrepareToReleaseCommunication()
        {
            Post(() => { try { Events?.OnPreparedToReleaseCommunication(SystemTag); } catch { } });
            return true;
        }

        public bool ReleaseCommunication() => true;

        public bool PrepareToDelete() => true;

        public bool SetLanguage(int languageId)
        {
            Lcid = languageId;
            return true;
        }

        public string GetFunctions(string operationState) => DtmXml.Functions(Lcid);

        public bool InvokeFunctionRequest(string invokeId, string functionCall) => false;

        public bool PrivateDialogEnabled(bool enabled) => true;

        // ================================================================ IDtm2

        public bool Environment2(string systemTag, IFdtContainer container, string frameInfo) => Environment(systemTag, container);

        public bool SetSystemGuiLabel(string systemGuiLabel)
        {
            GuiLabel = systemGuiLabel;
            return true;
        }

        // ================================================================ IDtmInformation / IDtmChannel / IDtmParameter

        public string GetInformation() => DtmXml.Information();

        public IFdtChannelCollection GetChannels() => new HartChannelCollection(_channel);

        public string GetParameters(string parameterPath) => DtmXml.Parameters(SystemTag);

        public bool SetParameters(string parameterPath, string xmlDocument) => false;

        // ================================================================ IDtmActiveXInformation

        public string GetActiveXGuid(string functionCall) => HasUi(functionCall) ? ConfigurationControl.ClassId : "";

        public string GetActiveXProgId(string functionCall) => HasUi(functionCall) ? ConfigurationControl.ProgIdText : "";

        private static bool HasUi(string functionCall)
        {
            int id = DtmXml.FunctionIdOf(functionCall);
            return id == DtmXml.FunctionConfiguration || id == DtmXml.FunctionLog || id == DtmXml.FunctionScan || id == DtmXml.FunctionAbout;
        }

        // ================================================================ IFdtEvents (Frame → DTM)

        public void OnChildParameterChanged(string systemTag) { }
        public void OnParameterChanged(string systemTag, string parameter) { }
        public void OnLockDataSet(string systemTag, string userName) { }
        public bool OnUnlockDataSet(string systemTag, string userName) => true;

        // ================================================================ persistência

        public void GetClassID(out Guid pClassID) => pClassID = new Guid(ClassId);

        void IPersistPropertyBag.InitNew() => ResetSettings();

        public void Load(IntPtr pPropBag, IntPtr pErrorLog)
        {
            if (pPropBag == IntPtr.Zero) throw new ArgumentNullException(nameof(pPropBag));
            var s = new HartLinkSettings();
            foreach (var name in HartLinkSettings.PropertyNames)
                if (PropertyBagPtr.Read(pPropBag, name, out object v, pErrorLog) == 0 && v != null) s.TrySet(name, v);
            _settings = s;
            _dirty = false;
            CommLog.Info("Data set carregado: " + s.Describe());
        }

        public void Save(IntPtr pPropBag, bool fClearDirty, bool fSaveAllProperties)
        {
            if (pPropBag == IntPtr.Zero) throw new ArgumentNullException(nameof(pPropBag));
            foreach (var kv in _settings.ToProperties())
            {
                int hr = PropertyBagPtr.Write(pPropBag, kv.Key, kv.Value);
                if (hr < 0) Marshal.ThrowExceptionForHR(hr);
            }
            PropertyBagPtr.Write(pPropBag, "Version", DtmXml.Version);
            if (fClearDirty) _dirty = false;
        }

        // IPersistStreamInit: texto UTF-8 "LasecHartCommDTM <versão>" + linhas Nome=Valor.
        private const string StreamHeader = "LasecHartCommDTM ";

        void IPersistStreamInit.InitNew() => ResetSettings();

        public int IsDirty() => _dirty ? 0 : 1; // S_OK / S_FALSE

        public void Load(IStream pStm)
        {
            var bytes = ReadAll(pStm);
            string text = Encoding.UTF8.GetString(bytes);
            if (!text.StartsWith(StreamHeader, StringComparison.Ordinal))
                throw new COMException("Data set não reconhecido pelo Lasec HART Communication DTM.", unchecked((int)0x80004005));
            _settings = HartLinkSettings.FromText(text.Substring(text.IndexOf('\n') + 1));
            _dirty = false;
            CommLog.Info("Data set carregado: " + _settings.Describe());
        }

        public void Save(IStream pStm, bool fClearDirty)
        {
            var bytes = Encoding.UTF8.GetBytes(StreamHeader + DtmXml.Version + "\n" + _settings.ToText());
            pStm.Write(bytes, bytes.Length, IntPtr.Zero);
            if (fClearDirty) _dirty = false;
        }

        public void GetSizeMax(out long pCbSize) => pCbSize = 4096;

        private void ResetSettings()
        {
            _settings = new HartLinkSettings();
            _dirty = true;
        }

        private static byte[] ReadAll(IStream s)
        {
            var ms = new MemoryStream();
            var buf = new byte[4096];
            IntPtr pRead = Marshal.AllocCoTaskMem(4);
            try
            {
                while (true)
                {
                    s.Read(buf, buf.Length, pRead);
                    int n = Marshal.ReadInt32(pRead);
                    if (n <= 0) break;
                    ms.Write(buf, 0, n);
                    if (ms.Length > 1 << 20) break;
                }
            }
            finally { Marshal.FreeCoTaskMem(pRead); }
            return ms.ToArray();
        }

        // ================================================================ registro COM (regasm)

        // Categorias FDT 1.2 (§ "Component categories"): DTM, canal, comunicação, apresentação ActiveX, HART.
        private static readonly string[] Categories =
        {
            "{036D1490-387B-11D4-86E1-00E0987270B9}", // CATID_FDT_DTM
            "{036D1493-387B-11D4-86E1-00E0987270B9}", // CATID_FDT_CHANNEL
            "{036D1494-387B-11D4-86E1-00E0987270B9}", // CATID_FDT_COMMUNICATION
            "{036D1495-387B-11D4-86E1-00E0987270B9}", // CATID_FDT_PRESENTATIONACTIVEXCONTROL
            "{036D1498-387B-11D4-86E1-00E0987270B9}", // CATID_FDT_HART
        };

        private static readonly string[] CategoryNames = { "FDT DTM", "FDT DTM Channel", "FDT DTM Communication", "FDT DTM Presentation ActiveXControl", "FDT FIELDBUS Hart" };

        [ComRegisterFunction]
        internal static void Register(Type t)
        {
            if (t != typeof(CommDtm)) return;
            using (var cls = Registry.ClassesRoot.OpenSubKey(@"CLSID\" + t.GUID.ToString("B"), writable: true))
            {
                if (cls == null) return;
                cls.SetValue(null, DtmXml.DtmName + " DTM");
                using (var cats = cls.CreateSubKey("Implemented Categories"))
                    foreach (var c in Categories) cats.CreateSubKey(c)?.Dispose();
            }
            for (int i = 0; i < Categories.Length; i++)
                using (var k = Registry.ClassesRoot.CreateSubKey(@"Component Categories\" + Categories[i]))
                    if (k != null && k.GetValue("409") == null) k.SetValue("409", CategoryNames[i]);
        }

        [ComUnregisterFunction]
        internal static void Unregister(Type t)
        {
            if (t != typeof(CommDtm)) return;
            using (var cls = Registry.ClassesRoot.OpenSubKey(@"CLSID\" + t.GUID.ToString("B"), writable: true))
            {
                if (cls == null) return;
                foreach (var c in Categories) cls.DeleteSubKeyTree(@"Implemented Categories\" + c, throwOnMissingSubKey: false);
            }
        }
    }
}
