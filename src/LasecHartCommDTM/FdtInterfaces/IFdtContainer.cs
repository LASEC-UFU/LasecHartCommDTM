using System.Runtime.InteropServices;

namespace LasecHartCommDTM.FdtInterfaces
{
    // Interface do frame FDT passada ao DTM via IDtm.Environment()
    [Guid("036d1487-387b-11d4-86e1-00e0987270b9")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComVisible(true)]
    public interface IFdtContainer
    {
        void SaveRequest();
        void LockDataSet();
        void UnlockDataSet();
        [return: MarshalAs(UnmanagedType.BStr)] string GetXmlSchemaPath();
    }
}
