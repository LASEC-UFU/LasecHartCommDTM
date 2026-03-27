using System;
using System.Runtime.InteropServices;
using CT = System.Runtime.InteropServices.ComTypes;

class Program
{
    [DllImport("oleaut32.dll", CharSet = CharSet.Unicode)]
    static extern int LoadTypeLib(string szFile, out CT.ITypeLib ppTLib);

    static void Main(string[] args)
    {
        string path = args.Length > 0 ? args[0] : @"C:\WINDOWS\SysWow64\CWHARTFDT.ocx";
        CT.ITypeLib tlib;
        int hr = LoadTypeLib(path, out tlib);
        if (hr != 0) { Console.WriteLine("FAIL: 0x" + hr.ToString("X8")); return; }

        int count = tlib.GetTypeInfoCount();
        Console.WriteLine("TypeLib: " + count + " types");

        for (int i = 0; i < count; i++)
        {
            CT.ITypeInfo ti;
            tlib.GetTypeInfo(i, out ti);
            string name, doc, hf;
            int hc;
            ti.GetDocumentation(-1, out name, out doc, out hc, out hf);
            CT.TYPEKIND tk;
            tlib.GetTypeInfoType(i, out tk);

            IntPtr pa;
            ti.GetTypeAttr(out pa);
            var a = (CT.TYPEATTR)Marshal.PtrToStructure(pa, typeof(CT.TYPEATTR));
            Console.WriteLine("{0}: {1} kind={2} guid={3} funcs={4} impls={5}", 
                i, name, tk, a.guid.ToString("B"), a.cFuncs, a.cImplTypes);
            ti.ReleaseTypeAttr(pa);

            // For coclass (kind=5), show implemented interfaces
            if (tk == CT.TYPEKIND.TKIND_COCLASS)
            {
                for (int j = 0; j < a.cImplTypes; j++)
                {
                    int href;
                    ti.GetRefTypeOfImplType(j, out href);
                    CT.ITypeInfo rt;
                    ti.GetRefTypeInfo(href, out rt);
                    string rn, rd, rhf;
                    int rhc;
                    rt.GetDocumentation(-1, out rn, out rd, out rhc, out rhf);

                    IntPtr rpa;
                    rt.GetTypeAttr(out rpa);
                    var ra = (CT.TYPEATTR)Marshal.PtrToStructure(rpa, typeof(CT.TYPEATTR));
                    CT.IMPLTYPEFLAGS ifl;
                    ti.GetImplTypeFlags(j, out ifl);
                    Console.WriteLine("    impl[{0}]: {1} guid={2} flags={3}", 
                        j, rn, ra.guid.ToString("B"), ifl);
                    rt.ReleaseTypeAttr(rpa);
                }
            }

            // For interface (kind=3 TKIND_DISPATCH or kind=0 TKIND_ENUM), show methods
            if (tk == CT.TYPEKIND.TKIND_DISPATCH || tk == CT.TYPEKIND.TKIND_INTERFACE)
            {
                for (int f = 0; f < a.cFuncs && f < 20; f++)
                {
                    IntPtr pfd;
                    ti.GetFuncDesc(f, out pfd);
                    var fd = (CT.FUNCDESC)Marshal.PtrToStructure(pfd, typeof(CT.FUNCDESC));
                    string[] names = new string[1];
                    int pcNames;
                    ti.GetNames(fd.memid, names, 1, out pcNames);
                    Console.WriteLine("    func[{0}]: {1} memid={2} invkind={3}", 
                        f, names[0] ?? "?", fd.memid, fd.invkind);
                    ti.ReleaseFuncDesc(pfd);
                }
            }
        }
    }
}
