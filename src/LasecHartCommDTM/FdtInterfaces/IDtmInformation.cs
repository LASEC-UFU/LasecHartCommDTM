using System.Runtime.InteropServices;

namespace LasecHartCommDTM.FdtInterfaces
{
    // Interface obrigatória para o scan do catálogo FDT 1.x
    // PACTware chama GetInformation() para obter o XML de descrição do DTM
    [Guid("036d147f-387b-11d4-86e1-00e0987270b9")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    [ComVisible(true)]
    public interface IDtmInformation
    {
        [return: MarshalAs(UnmanagedType.BStr)] string GetInformation();
    }
}
