using System;
using System.Runtime.InteropServices;
using CT = System.Runtime.InteropServices.ComTypes;
using System.Text;

class Program {
    [DllImport("oleaut32.dll", CharSet = CharSet.Unicode)]
    static extern int LoadTypeLib(string szFile, out CT.ITypeLib ppTLib);

    static void Main(string[] args) {
        string path = args.Length > 0 ? args[0] : @"C:\Program Files (x86)\PACTware 5.0\DTM700\JosueLab\LasecHartCommDTM\Dtm\LasecHartCommDTM.tlb";
        CT.ITypeLib tlib;
        int hr = LoadTypeLib(path, out tlib);
        if (hr != 0) { Console.WriteLine("LoadTypeLib failed: 0x" + hr.ToString("X8")); return; }

        int count = tlib.GetTypeInfoCount();
        Console.WriteLine("TypeInfo count: " + count);

        for (int i = 0; i < count; i++) {
            CT.ITypeInfo ti;
            tlib.GetTypeInfo(i, out ti);
            string name, doc, helpFile;
            int hctx;
            ti.GetDocumentation(-1, out name, out doc, out hctx, out helpFile);

            IntPtr pAttr;
            ti.GetTypeAttr(out pAttr);
            var ta = (CT.TYPEATTR)Marshal.PtrToStructure(pAttr, typeof(CT.TYPEATTR));
            ti.ReleaseTypeAttr(pAttr);

            Console.WriteLine("[" + i + "] " + ta.typekind + " " + name + " GUID=" + ta.guid.ToString("B"));

            if (ta.typekind == CT.TYPEKIND.TKIND_COCLASS) {
                for (int j = 0; j < ta.cImplTypes; j++) {
                    int href;
                    ti.GetRefTypeOfImplType(j, out href);
                    CT.ITypeInfo refTi;
                    ti.GetRefTypeInfo(href, out refTi);
                    string rn, rd, rf;
                    int rh;
                    refTi.GetDocumentation(-1, out rn, out rd, out rh, out rf);

                    CT.IMPLTYPEFLAGS flags;
                    ti.GetImplTypeFlags(j, out flags);

                    IntPtr pRefAttr;
                    refTi.GetTypeAttr(out pRefAttr);
                    var rta = (CT.TYPEATTR)Marshal.PtrToStructure(pRefAttr, typeof(CT.TYPEATTR));
                    refTi.ReleaseTypeAttr(pRefAttr);

                    Console.WriteLine("    impl[" + j + "] " + rn + " GUID=" + rta.guid.ToString("B") + " flags=" + flags);
                }
            }
        }
    }
}
