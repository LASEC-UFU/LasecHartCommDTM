using System;
using System.Runtime.InteropServices;
using CT = System.Runtime.InteropServices.ComTypes;

class Program
{
    [DllImport("oleaut32.dll", CharSet = CharSet.Unicode)]
    static extern int LoadTypeLibEx(string szFile, int regkind, out CT.ITypeLib pptlib);

    static void Main(string[] args)
    {
        string path = args.Length > 0 ? args[0] : @"C:\WINDOWS\SysWow64\CWHARTFDT.ocx";
        CT.ITypeLib tlib;
        int hr = LoadTypeLibEx(path, 0, out tlib);
        if (hr != 0) { Console.WriteLine("FAIL: 0x" + hr.ToString("X8")); return; }

        int count = tlib.GetTypeInfoCount();
        for (int i = 0; i < count; i++)
        {
            CT.ITypeInfo ti;
            tlib.GetTypeInfo(i, out ti);
            string name, doc, help; int ctx;
            tlib.GetDocumentation(i, out name, out doc, out ctx, out help);
            IntPtr pAttr;
            ti.GetTypeAttr(out pAttr);
            var attr = (CT.TYPEATTR)Marshal.PtrToStructure(pAttr, typeof(CT.TYPEATTR));
            ti.ReleaseTypeAttr(pAttr);
            Console.WriteLine((CT.TYPEKIND)attr.typekind + " " + name + " " + attr.guid.ToString("B") + " impl=" + attr.cImplTypes);
            if (attr.typekind == CT.TYPEKIND.TKIND_COCLASS)
            {
                for (int j = 0; j < attr.cImplTypes; j++)
                {
                    int href;
                    ti.GetRefTypeOfImplType(j, out href);
                    CT.ITypeInfo refTi;
                    ti.GetRefTypeInfo(href, out refTi);
                    string rn, rd, rh; int rc;
                    refTi.GetDocumentation(-1, out rn, out rd, out rc, out rh);
                    IntPtr pra;
                    refTi.GetTypeAttr(out pra);
                    var ra = (CT.TYPEATTR)Marshal.PtrToStructure(pra, typeof(CT.TYPEATTR));
                    refTi.ReleaseTypeAttr(pra);
                    CT.IMPLTYPEFLAGS flags;
                    ti.GetImplTypeFlags(j, out flags);
                    Console.WriteLine("  [" + j + "] " + rn + " " + ra.guid.ToString("B") + " " + flags);
                }
            }
        }
    }
}
