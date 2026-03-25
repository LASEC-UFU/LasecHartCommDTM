using System.Runtime.InteropServices;

namespace LasecHartCommDTM.FdtInterfaces
{
    // Interface do frame FDT passada ao DTM via IDtm.Environment()
    [Guid("036d1487-387b-11d4-86e1-00e0987270b9")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    [ComVisible(true)]
    public interface IFdtContainer
    {
        bool SaveRequest([MarshalAs(UnmanagedType.BStr)] string systemTag);
        bool LockDataSet([MarshalAs(UnmanagedType.BStr)] string systemTag);
        bool UnlockDataSet([MarshalAs(UnmanagedType.BStr)] string systemTag);
        [return: MarshalAs(UnmanagedType.BStr)] string GetXmlSchemaPath();
    }
}
