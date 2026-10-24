using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

[assembly: AssemblyTitle("RavenCheat")]
[assembly: AssemblyDescription("Ravenfield internal cheat injector")]
[assembly: AssemblyProduct("RavenCheat")]
[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyFileVersion("2.0.0.0")]

internal static class Program
{
    private const uint PROCESS_CREATE_THREAD = 0x0002;
    private const uint PROCESS_QUERY_INFORMATION = 0x0400;
    private const uint PROCESS_VM_OPERATION = 0x0008;
    private const uint PROCESS_VM_WRITE = 0x0020;
    private const uint PROCESS_VM_READ = 0x0010;
    private const uint MEM_COMMIT = 0x1000;
    private const uint MEM_RESERVE = 0x2000;
    private const uint PAGE_EXECUTE_READWRITE = 0x40;
    private const uint PAGE_READWRITE = 0x04;

    [StructLayout(LayoutKind.Sequential)]
    private struct MODULEINFO
    {
        public IntPtr lpBaseOfDll;
        public uint SizeOfImage;
        public IntPtr EntryPoint;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAllocEx(IntPtr process, IntPtr address, UIntPtr size, uint type, uint protect);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteProcessMemory(IntPtr process, IntPtr address, byte[] buffer, UIntPtr size, out UIntPtr written);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(IntPtr process, IntPtr address, byte[] buffer, UIntPtr size, out UIntPtr read);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateRemoteThread(IntPtr process, IntPtr attr, UIntPtr stack, IntPtr start, IntPtr param, uint flags, out uint tid);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, uint ms);

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EnumProcessModulesEx(IntPtr process, IntPtr[] modules, uint size, out uint needed, uint filter);

    [DllImport("psapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetModuleFileNameEx(IntPtr process, IntPtr module, StringBuilder name, int size);

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool GetModuleInformation(IntPtr process, IntPtr module, out MODULEINFO info, uint size);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hwnd, string text, string caption, uint type);

    private static int Main()
    {
        try
        {
            string dll = ExtractDll();
            Process[] found = Process.GetProcessesByName("ravenfield");
            if (found.Length == 0)
            {
                Box("没有找到 ravenfield.exe。\n先启动游戏，进入战局后再运行。", true);
                return 1;
            }
            StringBuilder log = new StringBuilder();
            for (int i = 0; i < found.Length; i++)
            {
                string domainName = Inject(found[i].Id, dll);
                log.AppendLine("已注入 PID " + found[i].Id + " (" + domainName + ")");
            }
            log.Append("回到游戏按 Insert。");
            Box(log.ToString(), false);
            return 0;
        }
        catch (Exception e)
        {
            Record("Injection failed: " + e);
            Box(e.Message, true);
            return 1;
        }
    }

    private static void Box(string text, bool error)
    {
        MessageBox(IntPtr.Zero, text, "RavenCheat", error ? 0x10u : 0x40u);
    }

    private static string ExtractDll()
    {
        Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("RavenCheat.dll");
        if (stream == null) throw new InvalidOperationException("程序里没有嵌入 RavenCheat.dll。");
        string path = Path.Combine(Path.GetTempPath(), "RavenCheat_" + Process.GetCurrentProcess().Id + ".dll");
        using (stream)
        using (FileStream file = File.Create(path))
        {
            stream.CopyTo(file);
        }
        return path;
    }

    private static string Inject(int pid, string dllPath)
    {
        IntPtr process = OpenProcess(PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ, false, pid);
        if (process == IntPtr.Zero) throw new InvalidOperationException("无法打开游戏进程。用管理员身份再试一次。");
        try
        {
            ulong mono = FindModule(process, "mono-2.0-bdwgc.dll");
            if (mono == 0) throw new InvalidOperationException("游戏进程里还没有 mono。等游戏进到主菜单后再注入。");
            ulong root = Export(process, mono, "mono_get_root_domain");
            ulong attach = Export(process, mono, "mono_thread_attach");
            ulong open = Export(process, mono, "mono_assembly_open");
            ulong imageOf = Export(process, mono, "mono_assembly_get_image");
            ulong classFrom = Export(process, mono, "mono_class_from_name");
            ulong methodFrom = Export(process, mono, "mono_class_get_method_from_name");
            ulong invoke = Export(process, mono, "mono_runtime_invoke");

            ulong rootDomain = Call(process, root, 0, 0, 0, 0, 0, 0);
            if (rootDomain == 0) throw new InvalidOperationException("mono_get_root_domain 失败。");
            string domainName = "Mono root domain";
            ulong domain = rootDomain;
            Record("Selected domain " + domainName + " for PID " + pid + ".");

            ulong path = AllocUtf8(process, dllPath);
            ulong assembly = Call(process, open, attach, domain, path, 0, 0, 0);
            if (assembly == 0) throw new InvalidOperationException("mono 没有加载 DLL：" + dllPath);

            ulong image = Call(process, imageOf, attach, domain, assembly, 0, 0, 0);
            if (image == 0) throw new InvalidOperationException("mono_assembly_get_image 失败。");

            ulong ns = AllocUtf8(process, "RavenCheat");
            ulong name = AllocUtf8(process, "Loader");
            ulong klass = Call(process, classFrom, attach, domain, image, ns, name, 0);
            if (klass == 0) throw new InvalidOperationException("找不到 RavenCheat.Loader。");

            ulong init = AllocUtf8(process, "Init");
            ulong method = Call(process, methodFrom, attach, domain, klass, init, 0, 0);
            if (method == 0) throw new InvalidOperationException("找不到 Loader.Init。");

            ulong excSlot = Alloc(process, 8, false);
            Call(process, invoke, attach, domain, method, 0, 0, excSlot);
            byte[] exc = Read(process, excSlot, 8);
            ulong thrown = BitConverter.ToUInt64(exc, 0);
            if (thrown != 0) throw new InvalidOperationException("Loader.Init 抛出了 Mono 异常。");
            Record("Loader.Init returned for PID " + pid + ".");
            return domainName;
        }
        finally
        {
            CloseHandle(process);
        }
    }

    private static void Record(string message)
    {
        try
        {
            string logPath = Path.Combine(Path.GetTempPath(), "RavenCheat-injector.log");
            File.AppendAllText(logPath, DateTime.UtcNow.ToString("o") + " " + message + Environment.NewLine);
        }
        catch (Exception)
        {
        }
    }

    private static ulong FindModule(IntPtr process, string fileName)
    {
        IntPtr[] modules = new IntPtr[512];
        uint needed;
        if (!EnumProcessModulesEx(process, modules, (uint)(modules.Length * IntPtr.Size), out needed, 3))
            throw new InvalidOperationException("枚举模块失败。");
        int count = (int)(needed / (uint)IntPtr.Size);
        if (count > modules.Length) count = modules.Length;
        for (int i = 0; i < count; i++)
        {
            StringBuilder sb = new StringBuilder(1024);
            if (GetModuleFileNameEx(process, modules[i], sb, sb.Capacity) == 0) continue;
            if (sb.ToString().EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
            {
                MODULEINFO info;
                if (!GetModuleInformation(process, modules[i], out info, (uint)Marshal.SizeOf(typeof(MODULEINFO))))
                    throw new InvalidOperationException("读取 mono 模块信息失败。");
                return (ulong)info.lpBaseOfDll.ToInt64();
            }
        }
        return 0;
    }

    private static ulong Export(IntPtr process, ulong module, string name)
    {
        byte[] dos = Read(process, module, 0x40);
        int pe = BitConverter.ToInt32(dos, 0x3C);
        byte[] nt = Read(process, module + (ulong)pe, 0x200);
        int exportRva = BitConverter.ToInt32(nt, 24 + 112);
        int exportSize = BitConverter.ToInt32(nt, 24 + 116);
        if (exportRva == 0) throw new InvalidOperationException("mono 没有导出表。");
        byte[] dir = Read(process, module + (ulong)exportRva, 40);
        int count = BitConverter.ToInt32(dir, 24);
        int funcs = BitConverter.ToInt32(dir, 28);
        int names = BitConverter.ToInt32(dir, 32);
        int ords = BitConverter.ToInt32(dir, 36);
        byte[] nameRvas = Read(process, module + (ulong)names, count * 4);
        byte[] ordinals = Read(process, module + (ulong)ords, count * 2);
        byte[] funcRvas = Read(process, module + (ulong)funcs, BitConverter.ToInt32(dir, 20) * 4);
        for (int i = 0; i < count; i++)
        {
            int rva = BitConverter.ToInt32(nameRvas, i * 4);
            string found = ReadCString(process, module + (ulong)rva);
            if (found != name) continue;
            int ord = BitConverter.ToUInt16(ordinals, i * 2);
            int fn = BitConverter.ToInt32(funcRvas, ord * 4);
            if (fn >= exportRva && fn < exportRva + exportSize)
                throw new InvalidOperationException(name + " 是转发导出。");
            return module + (ulong)fn;
        }
        throw new InvalidOperationException("找不到导出 " + name);
    }

    private static ulong Call(IntPtr process, ulong func, ulong attach, ulong domain, ulong a0, ulong a1, ulong a2, ulong a3)
    {
        ulong result = Alloc(process, 8, false);
        byte[] code = Shell(func, attach, domain, a0, a1, a2, a3, result);
        ulong page = Alloc(process, (ulong)code.Length, true);
        UIntPtr written;
        if (!WriteProcessMemory(process, (IntPtr)page, code, (UIntPtr)code.Length, out written))
            throw new InvalidOperationException("写入 shellcode 失败。");
        uint tid;
        IntPtr thread = CreateRemoteThread(process, IntPtr.Zero, UIntPtr.Zero, (IntPtr)page, IntPtr.Zero, 0, out tid);
        if (thread == IntPtr.Zero) throw new InvalidOperationException("CreateRemoteThread 失败。");
        WaitForSingleObject(thread, 15000);
        CloseHandle(thread);
        return BitConverter.ToUInt64(Read(process, result, 8), 0);
    }

    private static byte[] Shell(ulong func, ulong attach, ulong domain, ulong a0, ulong a1, ulong a2, ulong a3, ulong result)
    {
        MemoryStream ms = new MemoryStream();
        BinaryWriter w = new BinaryWriter(ms);
        w.Write(new byte[] { 0x48, 0x83, 0xEC, 0x28 });
        w.Write((byte)0x48); w.Write((byte)0xB8); w.Write(attach);
        w.Write(new byte[] { 0x48, 0x85, 0xC0 });
        w.Write((byte)0x74); w.Write((byte)0x0C);
        w.Write((byte)0x48); w.Write((byte)0xB9); w.Write(domain);
        w.Write(new byte[] { 0xFF, 0xD0 });
        w.Write((byte)0x48); w.Write((byte)0xB9); w.Write(a0);
        w.Write((byte)0x48); w.Write((byte)0xBA); w.Write(a1);
        w.Write((byte)0x49); w.Write((byte)0xB8); w.Write(a2);
        w.Write((byte)0x49); w.Write((byte)0xB9); w.Write(a3);
        w.Write((byte)0x48); w.Write((byte)0xB8); w.Write(func);
        w.Write(new byte[] { 0xFF, 0xD0 });
        w.Write((byte)0x49); w.Write((byte)0xBA); w.Write(result);
        w.Write(new byte[] { 0x49, 0x89, 0x02 });
        w.Write(new byte[] { 0x48, 0x83, 0xC4, 0x28 });
        w.Write((byte)0xC3);
        w.Flush();
        return ms.ToArray();
    }

    private static ulong Alloc(IntPtr process, ulong size, bool exec)
    {
        IntPtr mem = VirtualAllocEx(process, IntPtr.Zero, (UIntPtr)size, MEM_COMMIT | MEM_RESERVE, exec ? PAGE_EXECUTE_READWRITE : PAGE_READWRITE);
        if (mem == IntPtr.Zero) throw new InvalidOperationException("远程内存分配失败。");
        return (ulong)mem.ToInt64();
    }

    private static ulong AllocUtf8(IntPtr process, string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text + "\0");
        ulong mem = Alloc(process, (ulong)bytes.Length, false);
        UIntPtr written;
        if (!WriteProcessMemory(process, (IntPtr)mem, bytes, (UIntPtr)bytes.Length, out written))
            throw new InvalidOperationException("写入字符串失败。");
        return mem;
    }

    private static byte[] Read(IntPtr process, ulong address, int size)
    {
        byte[] buffer = new byte[size];
        UIntPtr read;
        if (!ReadProcessMemory(process, (IntPtr)address, buffer, (UIntPtr)size, out read) || read.ToUInt64() != (ulong)size)
            throw new InvalidOperationException("读取进程内存失败。");
        return buffer;
    }

    private static string ReadCString(IntPtr process, ulong address)
    {
        byte[] raw = Read(process, address, 128);
        int n = 0;
        while (n < raw.Length && raw[n] != 0) n++;
        return Encoding.ASCII.GetString(raw, 0, n);
    }
}
