using System.Runtime.InteropServices;

namespace LasecHartCommDTM.FdtInterfaces
{
    // Interface FDT 1.x oficial — GUID conforme padrão FDT Group
    // DispIds MUST match PIA (Jigfdt.Fdt100.IDtm) to avoid CCW IDispatch confusion.
    [Guid("036d1481-387b-11d4-86e1-00e0987270b9")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    [ComVisible(true)]
    public interface IDtm
    {
        [DispId(1)] [return: MarshalAs(UnmanagedType.Bool)]
        bool Environment(
            [MarshalAs(UnmanagedType.BStr)] string systemTag,
            [MarshalAs(UnmanagedType.Interface)] IFdtContainer container);

        [DispId(2)] [return: MarshalAs(UnmanagedType.Bool)]
        bool InitNew([MarshalAs(UnmanagedType.BStr)] string deviceType);

        [DispId(3)] [return: MarshalAs(UnmanagedType.Bool)]
        bool Config([MarshalAs(UnmanagedType.BStr)] string userInfo);

        [DispId(4)] [return: MarshalAs(UnmanagedType.Bool)]
        bool SetCommunication(
            [MarshalAs(UnmanagedType.Interface)] IFdtCommunication communication);

        [DispId(5)] [return: MarshalAs(UnmanagedType.Bool)]
        bool PrepareToRelease();

        [DispId(6)] [return: MarshalAs(UnmanagedType.Bool)]
        bool PrepareToReleaseCommunication();

        [DispId(7)] [return: MarshalAs(UnmanagedType.Bool)]
        bool ReleaseCommunication();

        [DispId(8)] [return: MarshalAs(UnmanagedType.Bool)]
        bool PrepareToDelete();

        [DispId(9)] [return: MarshalAs(UnmanagedType.Bool)]
        bool SetLanguage(int languageId);

        [DispId(10)] [return: MarshalAs(UnmanagedType.BStr)]
        string GetFunctions([MarshalAs(UnmanagedType.BStr)] string operationState);

        [DispId(11)] [return: MarshalAs(UnmanagedType.Bool)]
        bool InvokeFunctionRequest(
            [MarshalAs(UnmanagedType.BStr)] string invokeId,
            [MarshalAs(UnmanagedType.BStr)] string functionCall);

        [DispId(12)] [return: MarshalAs(UnmanagedType.Bool)]
        bool PrivateDialogEnabled(bool enabled);
    }
}
