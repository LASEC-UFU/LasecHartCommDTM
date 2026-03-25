using System.Runtime.InteropServices;

namespace LasecHartCommDTM.FdtInterfaces
{
    // Interface FDT 1.x oficial — GUID conforme padrão FDT Group
    [Guid("036d1481-387b-11d4-86e1-00e0987270b9")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    [ComVisible(true)]
    public interface IDtm
    {
        bool Environment(
            [MarshalAs(UnmanagedType.BStr)] string systemTag,
            [MarshalAs(UnmanagedType.Interface)] IFdtContainer container);

        bool InitNew([MarshalAs(UnmanagedType.BStr)] string deviceType);

        bool Config([MarshalAs(UnmanagedType.BStr)] string userInfo);

        bool SetCommunication(
            [MarshalAs(UnmanagedType.Interface)] IFdtCommunication communication);

        bool PrepareToRelease();
        bool PrepareToReleaseCommunication();
        bool ReleaseCommunication();
        bool PrepareToDelete();

        bool SetLanguage(int languageId);

        [return: MarshalAs(UnmanagedType.BStr)] string GetFunctions(
            [MarshalAs(UnmanagedType.BStr)] string operationState);

        bool InvokeFunctionRequest(
            [MarshalAs(UnmanagedType.BStr)] string invokeId,
            [MarshalAs(UnmanagedType.BStr)] string functionCall);

        bool PrivateDialogEnabled(bool enabled);
    }
}
