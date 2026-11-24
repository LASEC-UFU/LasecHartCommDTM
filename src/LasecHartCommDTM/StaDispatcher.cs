using System;
using System.Threading;
using System.Windows.Forms;

namespace LasecHartCommDTM
{
    /// <summary>
    /// Entrega ações na thread STA que criou o DTM (a thread de UI do Frame). As respostas FDT são assíncronas:
    /// a E/S roda numa thread própria e os eventos (OnConnectResponse, OnTransactionResponse, IDtmEvents…) voltam
    /// para a thread do DTM, como esperam os DTMs de instrumento e o Frame.
    /// </summary>
    internal sealed class StaDispatcher : IDisposable
    {
        private readonly Control _ctl;
        private readonly int _threadId = Thread.CurrentThread.ManagedThreadId;

        public StaDispatcher()
        {
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                _ctl = new Control();
                _ = _ctl.Handle; // cria a janela oculta nesta thread
            }
        }

        public static Action<string> OnError { get; set; }

        public bool OnOwnerThread => Thread.CurrentThread.ManagedThreadId == _threadId;

        /// <summary>Executa depois, na thread do DTM. Sem STA (criado numa MTA), executa na thread que chamou.</summary>
        public void Post(Action action)
        {
            void Run()
            {
                try { action(); }
                catch (Exception ex) { OnError?.Invoke(ex.Message); }
            }

            if (_ctl == null) { Run(); return; }
            try
            {
                if (!_ctl.IsDisposed && _ctl.IsHandleCreated) _ctl.BeginInvoke((Action)Run);
            }
            catch (InvalidOperationException) { /* janela já destruída: DTM liberado */ }
        }

        public void Dispose()
        {
            if (_ctl == null) return;
            if (OnOwnerThread) _ctl.Dispose();
            else Post(() => _ctl.Dispose());
        }
    }
}
