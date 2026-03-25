using System.Runtime.InteropServices;

namespace LasecHartCommDTM.FdtInterfaces
{
    // Interface FDT 1.x oficial para CommDTM — GUID conforme padrão FDT Group
    [Guid("039ecfc4-9ca8-44e6-944d-b37f288a34d8")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    [ComVisible(true)]
    public interface IFdtCommunication
    {
        void Abort([MarshalAs(UnmanagedType.BStr)] string fieldbusFrame);

        bool ConnectRequest(
            [MarshalAs(UnmanagedType.Interface)] IFdtCommunicationEvents callBack,
            [MarshalAs(UnmanagedType.BStr)] string invokeId,
            [MarshalAs(UnmanagedType.BStr)] string protocolId,
            [MarshalAs(UnmanagedType.BStr)] string fieldbusFrame);

        bool DisconnectRequest(
            [MarshalAs(UnmanagedType.BStr)] string invokeId,
            [MarshalAs(UnmanagedType.BStr)] string fieldbusFrame);

        bool TransactionRequest(
            [MarshalAs(UnmanagedType.BStr)] string invokeId,
            [MarshalAs(UnmanagedType.BStr)] string fieldbusFrame);

        [return: MarshalAs(UnmanagedType.BStr)] string GetSupportedProtocols();

        bool SequenceBegin([MarshalAs(UnmanagedType.BStr)] string fieldbusFrame);
        bool SequenceStart([MarshalAs(UnmanagedType.BStr)] string fieldbusFrame);
        bool SequenceEnd([MarshalAs(UnmanagedType.BStr)] string fieldbusFrame);
    }
}
