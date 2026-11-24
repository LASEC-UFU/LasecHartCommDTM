using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace LasecHartCommDTM
{
    // Interfaces de persistência OLE (ocidl.h) exigidas de um DTM FDT 1.x: IPersistPropertyBag ou IPersistStreamInit.
    //
    // O property bag chega como ponteiro bruto (IntPtr) e é chamado pela vtable (PropertyBagPtr). Assim o DTM funciona
    // tanto com Frames nativos (PACTware) quanto com Frames .NET no mesmo processo — nesse caso o CLR entregaria o
    // objeto gerenciado do Frame, cujo tipo IPropertyBag é outro tipo .NET (mesmo GUID) e não pode ser convertido.
    // IStream usa System.Runtime.InteropServices.ComTypes.IStream, compartilhado por todos os assemblies.

    [ComImport, Guid("7FD52380-4E07-101B-AE2D-08002B2EC713"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPersistStreamInit
    {
        void GetClassID(out Guid pClassID);
        [PreserveSig] int IsDirty();
        void Load([In, MarshalAs(UnmanagedType.Interface)] IStream pStm);
        void Save([In, MarshalAs(UnmanagedType.Interface)] IStream pStm, [MarshalAs(UnmanagedType.Bool)] bool fClearDirty);
        void GetSizeMax(out long pCbSize);
        void InitNew();
    }

    [ComImport, Guid("37D84F60-42CB-11CE-8135-00AA004BB851"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPersistPropertyBag
    {
        void GetClassID(out Guid pClassID);
        void InitNew();
        /// <param name="pPropBag">IPropertyBag*</param>
        /// <param name="pErrorLog">IErrorLog* (pode ser nulo)</param>
        void Load(IntPtr pPropBag, IntPtr pErrorLog);
        void Save(IntPtr pPropBag, [MarshalAs(UnmanagedType.Bool)] bool fClearDirty, [MarshalAs(UnmanagedType.Bool)] bool fSaveAllProperties);
    }

    /// <summary>Chamadas a um IPropertyBag* pela vtable: Read (posição 3) e Write (posição 4), depois de IUnknown.</summary>
    internal static class PropertyBagPtr
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int ReadFn(IntPtr self, [MarshalAs(UnmanagedType.LPWStr)] string name, ref object value, IntPtr errorLog);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int WriteFn(IntPtr self, [MarshalAs(UnmanagedType.LPWStr)] string name, ref object value);

        private static T Slot<T>(IntPtr itf, int slot) where T : class =>
            (T)(object)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(itf), slot * IntPtr.Size), typeof(T));

        public static int Read(IntPtr bag, string name, out object value, IntPtr errorLog)
        {
            object v = null;
            int hr = Slot<ReadFn>(bag, 3)(bag, name, ref v, errorLog);
            value = v;
            return hr;
        }

        public static int Write(IntPtr bag, string name, object value) => Slot<WriteFn>(bag, 4)(bag, name, ref value);
    }
}
