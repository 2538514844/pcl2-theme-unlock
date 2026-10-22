using System;
using System.IO;
using System.Reflection;

// Bootstrap: registers an assembly resolver for the embedded dnlib.dll before any
// dnlib type is touched, then hands over to Patcher.
static class Program
{
    static Program()
    {
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
    }

    static Assembly Resolve(object sender, ResolveEventArgs args)
    {
        string simple = new AssemblyName(args.Name).Name;
        if (simple != "dnlib") return null;
        using (Stream st = Assembly.GetExecutingAssembly().GetManifestResourceStream("dnlib.dll"))
        {
            if (st == null) return null;
            byte[] buf = new byte[st.Length];
            int read = 0;
            while (read < buf.Length)
            {
                int n = st.Read(buf, read, buf.Length - read);
                if (n <= 0) break;
                read += n;
            }
            return Assembly.Load(buf);
        }
    }

    static int Main(string[] args)
    {
        return Patcher.Entry(args);
    }
}
