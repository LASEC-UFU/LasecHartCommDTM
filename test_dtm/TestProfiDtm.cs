using System;
using System.IO;
using System.Reflection;

class TestDtmCat
{
    static void Main()
    {
        var pactDir = @"C:\Program Files (x86)\PACTware Consortium\PACTware 5.0\";
        foreach (var dll in Directory.GetFiles(pactDir, "*.dll"))
            try { Assembly.LoadFrom(dll); } catch { }
        Assembly.Load("Fdt.Datatypes, Version=1.0.0.0, Culture=neutral, PublicKeyToken=d09fa7629c8cbe5a");

        Type readerIfaceType = null, readerImplType = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            if ((readerIfaceType = asm.GetType("OEMFDTContainer.Runtime.IDtmInfoReader")) != null) break;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types; try { types = asm.GetTypes(); } catch { continue; }
            foreach (var t in types)
                if (!t.IsInterface && !t.IsAbstract && readerIfaceType != null && readerIfaceType.IsAssignableFrom(t))
                    { readerImplType = t; break; }
            if (readerImplType != null) break;
        }

        // Get the constructor parameter type
        var ctor = readerImplType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)[0];
        var ctorParamType = ctor.GetParameters()[0].ParameterType;
        Console.WriteLine("Constructor param type: " + ctorParamType.FullName);
        Console.WriteLine("  Interface? " + ctorParamType.IsInterface);
        Console.WriteLine("  Assembly: " + ctorParamType.Assembly.GetName().Name);

        // What does this type look like?
        Console.WriteLine("  Methods:");
        foreach (var m in ctorParamType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            if (!m.IsSpecialName)
                Console.WriteLine("    " + m.ReturnType.Name + " " + m.Name);

        // Find implementations of ctorParamType
        Console.WriteLine("\nLooking for implementations...");
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types; try { types = asm.GetTypes(); } catch { continue; }
            foreach (var t in types)
                if (!t.IsInterface && !t.IsAbstract && ctorParamType.IsAssignableFrom(t))
                    Console.WriteLine("  [" + asm.GetName().Name + "] " + t.FullName);
        }

        // Try to find a factory method that creates IDtmInfoReader
        Console.WriteLine("\nLooking for factory methods in RuntimeFrame that return IDtmInfoReader...");
        var rtAsm = Assembly.LoadFrom(pactDir + "RuntimeFrame.dll");
        Type[] rtTypes; try { rtTypes = rtAsm.GetTypes(); } catch (ReflectionTypeLoadException e) { rtTypes = e.Types; }
        foreach (var t in rtTypes)
        {
            if (t == null) continue;
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
            {
                if (m.ReturnType == readerIfaceType || (readerIfaceType.IsAssignableFrom(m.ReturnType) && m.ReturnType != typeof(object)))
                    Console.WriteLine("  " + t.Name + "." + m.Name + "() -> " + m.ReturnType.Name);
            }
        }

        Console.ReadLine();
    }
}
