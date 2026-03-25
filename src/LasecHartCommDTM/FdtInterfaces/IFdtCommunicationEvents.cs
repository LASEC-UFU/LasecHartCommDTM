using System.Runtime.InteropServices;

namespace LasecHartCommDTM.FdtInterfaces
{
    // Callbacks que o frame FDT fornece para receber respostas da comunicação
    [Guid("036d1485-387b-11d4-86e1-00e0987270b9")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    [ComVisible(true)]
    public interface IFdtCommunicationEvents
    {
        void OnAbort([MarshalAs(UnmanagedType.BStr)] string communicationReference);

        void OnConnectResponse(
            [MarshalAs(UnmanagedType.BStr)] string invokeId,
            [MarshalAs(UnmanagedType.BStr)] string response);

        void OnDisconnectResponse(
            [MarshalAs(UnmanagedType.BStr)] string invokeId,
            [MarshalAs(UnmanagedType.BStr)] string response);

        void OnTransactionResponse(
            [MarshalAs(UnmanagedType.BStr)] string invokeId,
            [MarshalAs(UnmanagedType.BStr)] string response);
    }
}
