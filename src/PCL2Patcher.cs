using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

// PCL2 2.13.1.1 theme-unlock patcher.
// Applies 11 IL edits to "Plain Craft Launcher 2.exe": unlocks all themes,
// restores the per-theme palette from PCL 2.14.0.0, removes auto update and
// auto notice fetching. Original file is backed up before writing.
static class Patcher
{
    const string TargetName = "Plain Craft Launcher 2.exe";
    static ModuleDefMD mod;

    static void Log(string s) { Console.WriteLine(s); }

    public static int Entry(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = "PCL2 主题解锁补丁";

        string path = null;
        bool force = false;
        foreach (var a in args)
        {
            if (a == "-f" || a == "--force") force = true;
            else if (path == null) path = a;
        }
        if (path == null)
        {
            string local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, TargetName);
            if (File.Exists(local)) path = local;
        }
        if (path == null)
        {
            Console.WriteLine("未找到 " + TargetName);
            Console.WriteLine("用法：把本程序放到 PCL 目录下双击运行，或把 " + TargetName + " 拖到本程序上。");
            Console.WriteLine("按任意键退出…");
            Pause();
            return 2;
        }
        if (!File.Exists(path))
        {
            Console.WriteLine("文件不存在：" + path);
            Pause();
            return 2;
        }

        try
        {
            return Run(path, force);
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine("[失败] " + ex.GetType().Name + ": " + ex.Message);
            Console.WriteLine("原文件未被修改。");
            Pause();
            return 1;
        }
    }

    static int Run(string path, bool force)
    {
        Log("目标：" + path);
        foreach (var p in Process.GetProcessesByName("Plain Craft Launcher 2"))
        {
            Console.WriteLine("[提示] 检测到 PCL 正在运行，请先关闭后再运行本补丁。");
            Pause();
            return 3;
        }

        mod = ModuleDefMD.Load(path);
        var ver = mod.Assembly.Version;
        Log("版本：" + ver);
        if (!force && !(ver.Major == 2 && ver.Minor == 13))
        {
            Console.WriteLine("[提示] 该文件不是 2.13.x 版本，补丁可能不适用。加 -f 参数可强制继续。");
            Pause();
            return 4;
        }

        // already patched?
        var themeCheck = M("PCL.ModSecret", "ThemeCheckOne");
        if (themeCheck.Body.Instructions.Count <= 2)
        {
            Console.WriteLine("[跳过] 这个文件已经打过补丁，无需重复操作。");
            Pause();
            return 0;
        }

        // backup
        string bak = path + ".bak_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        File.Copy(path, bak, false);
        Log("已备份：" + bak);

        Log("");
        StubBool("PCL.ModSecret", "ThemeCheckOne", true);
        StubBool("PCL.ModSecret", "ThemeUnlock", true);
        StubRank();
        UnlockThemeUi();
        HideDonateOverlay();
        FixThemeRefreshArg();
        InjectThemeColorTable();
        RemoveUpdate();
        TruncateAt("PCL.ModSecret", "ServerSub", 0x00F2);

        string tmp = path + ".tmp";
        mod.Write(tmp);
        mod = null;
        GC.Collect();
        GC.WaitForPendingFinalizers();

        // sanity check the produced assembly
        var check = ModuleDefMD.Load(tmp);
        var tc = check.Find("PCL.ModSecret", false).FindMethod("ThemeCheckOne");
        if (tc == null || tc.Body.Instructions.Count != 2) throw new Exception("自检失败：ThemeCheckOne 未生效");
        check = null;
        GC.Collect();
        GC.WaitForPendingFinalizers();

        File.Copy(tmp, path, true);
        File.Delete(tmp);

        Log("");
        Log("[完成] 补丁已应用：" + path);
        Log("如需还原，用同目录下的备份文件覆盖回去即可。");
        Console.WriteLine();
        Console.WriteLine("按任意键退出…");
        Pause();
        return 0;
    }

    static void Pause()
    {
        try { Console.ReadKey(true); } catch { }
    }

    // ---------- helpers ----------

    static TypeDef T(string name) { return mod.Find(name, false); }

    static MethodDef M(string type, string name)
    {
        var t = T(type);
        if (t == null) throw new Exception("找不到类型 " + type);
        var m = t.FindMethod(name);
        if (m == null) throw new Exception("找不到方法 " + type + "::" + name);
        return m;
    }

    static IMethod FindCall(string declType, string name)
    {
        foreach (var t in mod.GetTypes())
            foreach (var m in t.Methods)
            {
                if (!m.HasBody) continue;
                foreach (var i in m.Body.Instructions)
                {
                    var im = i.Operand as IMethod;
                    if (im != null && im.Name == name && im.DeclaringType != null && im.DeclaringType.FullName == declType)
                        return im;
                }
            }
        throw new Exception("找不到调用目标 " + declType + "::" + name);
    }

    static IMethod FindCallN(string declType, string name, int paramCount)
    {
        foreach (var t in mod.GetTypes())
            foreach (var m in t.Methods)
            {
                if (!m.HasBody) continue;
                foreach (var i in m.Body.Instructions)
                {
                    var im = i.Operand as IMethod;
                    if (im != null && im.Name == name && im.DeclaringType != null && im.DeclaringType.FullName == declType
                        && im.GetParamCount() == paramCount)
                        return im;
                }
            }
        throw new Exception("找不到调用目标 " + declType + "::" + name + "/" + paramCount);
    }

    static IMethod FindGenericCall(string declType, string name, string genericArg)
    {
        foreach (var t in mod.GetTypes())
            foreach (var m in t.Methods)
            {
                if (!m.HasBody) continue;
                foreach (var i in m.Body.Instructions)
                {
                    var ms = i.Operand as MethodSpec;
                    if (ms != null && ms.Name == name && ms.DeclaringType != null && ms.DeclaringType.FullName == declType
                        && ms.GenericInstMethodSig != null && ms.GenericInstMethodSig.GenericArguments.Count == 1
                        && ms.GenericInstMethodSig.GenericArguments[0].FullName == genericArg)
                        return ms;
                }
            }
        throw new Exception("找不到泛型调用 " + declType + "::" + name + "<" + genericArg + ">");
    }

    static void StubBool(string type, string name, bool value)
    {
        var m = M(type, name);
        var body = m.Body;
        body.ExceptionHandlers.Clear();
        body.Variables.Clear();
        body.Instructions.Clear();
        body.Instructions.Add(Instruction.Create(value ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0));
        body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        body.MaxStack = 1;
        body.UpdateInstructionOffsets();
        Log("  · " + name + " -> " + value);
    }

    static void StubVoid(string type, string name)
    {
        var m = M(type, name);
        var body = m.Body;
        body.ExceptionHandlers.Clear();
        body.Variables.Clear();
        body.Instructions.Clear();
        body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        body.MaxStack = 1;
        body.UpdateInstructionOffsets();
        Log("  · " + name + " -> 空实现");
    }

    static void TruncateAt(string type, string name, int cutOffset)
    {
        var m = M(type, name);
        var body = m.Body;
        int idx = -1;
        for (int i = 0; i < body.Instructions.Count; i++)
            if (body.Instructions[i].Offset >= cutOffset) { idx = i; break; }
        if (idx < 0) throw new Exception("截断点不存在：" + cutOffset.ToString("X"));

        var ret = Instruction.Create(OpCodes.Ret);
        while (body.Instructions.Count > idx) body.Instructions.RemoveAt(idx);
        body.Instructions.Add(ret);

        for (int i = 0; i < body.Instructions.Count; i++)
        {
            var ins = body.Instructions[i];
            var tgt = ins.Operand as Instruction;
            if (tgt != null && !body.Instructions.Contains(tgt)) ins.Operand = ret;
            var tgts = ins.Operand as Instruction[];
            if (tgts != null)
                for (int k = 0; k < tgts.Length; k++)
                    if (!body.Instructions.Contains(tgts[k])) tgts[k] = ret;
        }

        for (int i = body.ExceptionHandlers.Count - 1; i >= 0; i--)
        {
            var eh = body.ExceptionHandlers[i];
            if (eh.TryStart == null || !body.Instructions.Contains(eh.TryStart) ||
                eh.HandlerStart == null || !body.Instructions.Contains(eh.HandlerStart))
            { body.ExceptionHandlers.RemoveAt(i); continue; }
            if (eh.TryEnd != null && !body.Instructions.Contains(eh.TryEnd)) eh.TryEnd = ret;
            if (eh.HandlerEnd != null && !body.Instructions.Contains(eh.HandlerEnd)) eh.HandlerEnd = ret;
            if (eh.FilterStart != null && !body.Instructions.Contains(eh.FilterStart)) eh.FilterStart = ret;
        }
        body.UpdateInstructionOffsets();
        Log("  · ServerSub 截断于 IL_" + cutOffset.ToString("X4") + "（移除公告/配置联网拉取）");
    }

    static void StubRank()
    {
        var rank = M("PCL.ModSecret", "get_CurrentRank");
        rank.Body.ExceptionHandlers.Clear();
        rank.Body.Variables.Clear();
        rank.Body.Instructions.Clear();
        rank.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4_5));
        rank.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        rank.Body.MaxStack = 1;
        rank.Body.UpdateInstructionOffsets();
        Log("  · CurrentRank -> Rank98");
    }

    static void UnlockThemeUi()
    {
        var ui = T("PCL.PageSetupUI");
        var target = M("PCL.PageSetupUI", "PageSetupUI_Loaded");
        var setEnabled = FindCall("System.Windows.UIElement", "set_IsEnabled");
        var setVisibility = FindCall("System.Windows.UIElement", "set_Visibility");
        var setOpacity = FindCall("System.Windows.UIElement", "set_Opacity");

        var pre = new List<Instruction>();
        foreach (var id in new string[] { "6", "7", "8", "9", "10", "11", "12", "13", "14" })
        {
            var get = ui.FindMethod("get_RadioLauncherTheme" + id);
            if (get == null) throw new Exception("缺少控件 RadioLauncherTheme" + id);
            pre.Add(Instruction.Create(OpCodes.Ldarg_0));
            pre.Add(Instruction.Create(OpCodes.Callvirt, get));
            pre.Add(Instruction.Create(OpCodes.Ldc_I4_1));
            pre.Add(Instruction.Create(OpCodes.Callvirt, setEnabled));
        }
        foreach (var mask in new string[] { "LabLauncherTheme8Copy", "LabLauncherTheme9Copy", "LabLauncherTheme11Click" })
        {
            var get = ui.FindMethod("get_" + mask);
            if (get == null) throw new Exception("缺少控件 " + mask);
            pre.Add(Instruction.Create(OpCodes.Ldarg_0));
            pre.Add(Instruction.Create(OpCodes.Callvirt, get));
            pre.Add(Instruction.Create(OpCodes.Ldc_I4_2));
            pre.Add(Instruction.Create(OpCodes.Callvirt, setVisibility));
        }
        var get5 = ui.FindMethod("get_RadioLauncherTheme5");
        pre.Add(Instruction.Create(OpCodes.Ldarg_0));
        pre.Add(Instruction.Create(OpCodes.Callvirt, get5));
        pre.Add(Instruction.Create(OpCodes.Ldc_R8, 1.0));
        pre.Add(Instruction.Create(OpCodes.Callvirt, setOpacity));
        pre.Add(Instruction.Create(OpCodes.Ldarg_0));
        pre.Add(Instruction.Create(OpCodes.Callvirt, get5));
        pre.Add(Instruction.Create(OpCodes.Ldc_I4_1));
        pre.Add(Instruction.Create(OpCodes.Callvirt, setEnabled));
        foreach (var mask in new string[] { "RadioLauncherTheme5Gray", "LabLauncherTheme5Unlock" })
        {
            var get = ui.FindMethod("get_" + mask);
            pre.Add(Instruction.Create(OpCodes.Ldarg_0));
            pre.Add(Instruction.Create(OpCodes.Callvirt, get));
            pre.Add(Instruction.Create(OpCodes.Ldc_I4_2));
            pre.Add(Instruction.Create(OpCodes.Callvirt, setVisibility));
        }

        var body = target.Body;
        for (int i = pre.Count - 1; i >= 0; i--) body.Instructions.Insert(0, pre[i]);
        if (body.MaxStack < 3) body.MaxStack = 3;
        body.UpdateInstructionOffsets();
        Log("  · 主题入口启用 + 遮罩折叠（" + pre.Count + " 条指令）");
    }

    static void HideDonateOverlay()
    {
        var m = M("PCL.PageSetupUI", "PageSetupUI_Loaded");
        var ins = m.Body.Instructions;
        int getIdx = -1;
        for (int i = 0; i < ins.Count; i++)
        {
            var im = ins[i].Operand as IMethod;
            if (im != null && im.Name == "get_PanLauncherHide") { getIdx = i; break; }
        }
        if (getIdx < 0) throw new Exception("找不到 PanLauncherHide 调用");
        int cst = getIdx + 1;
        if (ins[cst].OpCode.Code != Code.Ldc_I4_0) throw new Exception("PanLauncherHide 可见性常量异常");
        ins[cst].OpCode = OpCodes.Ldc_I4_2;
        m.Body.UpdateInstructionOffsets();
        Log("  · \"支持 PCL 开发\" 灰罩 -> Collapsed");
    }

    static void FixThemeRefreshArg()
    {
        var m = M("PCL.ModSecret", "ThemeRefresh");
        var ins = m.Body.Instructions;
        if (ins.Count >= 2 && ins[0].OpCode.Code == Code.Ldc_I4_0 && ins[1].OpCode.Code == Code.Starg_S)
        {
            ins[0].OpCode = OpCodes.Nop;
            ins[1].OpCode = OpCodes.Nop;
            ins[1].Operand = null;
            m.Body.UpdateInstructionOffsets();
            Log("  · ThemeRefresh 参数强制归零已移除");
        }
        else
        {
            throw new Exception("ThemeRefresh 入口结构不符合预期");
        }
    }

    static void InjectThemeColorTable()
    {
        var type = T("PCL.ModSecret");
        var m = M("PCL.ModSecret", "ThemeRefresh");
        var fHue = type.FindField("ColorHue");
        var fSat = type.FindField("ColorSat");
        var fLight = type.FindField("ColorLightAdjust");
        var fDelta = type.FindField("ColorHueTopbarDelta");
        var fThemeNow = type.FindField("ThemeNow");
        if (fHue == null || fSat == null || fLight == null || fDelta == null || fThemeNow == null)
            throw new Exception("主题颜色字段缺失");

        IMethod opInt = null;
        foreach (var t in mod.GetTypes())
        {
            foreach (var mm in t.Methods)
            {
                if (!mm.HasBody) continue;
                foreach (var i in mm.Body.Instructions)
                {
                    var im = i.Operand as IMethod;
                    if (im != null && im.Name == "op_Implicit" && im.DeclaringType != null
                        && im.DeclaringType.FullName.Contains("Int32,System.Int32[]")
                        && im.MethodSig != null && im.MethodSig.Params.Count == 1
                        && im.MethodSig.Params[0] is GenericVar
                        && ((GenericVar)im.MethodSig.Params[0]).Number == 0
                        && im.MethodSig.RetType.FullName.Contains("OneOf"))
                    { opInt = im; break; }
                }
                if (opInt != null) break;
            }
            if (opInt != null) break;
        }
        if (opInt == null) throw new Exception("找不到 OneOf<int,int[]>::op_Implicit(int)");
        var opArr = new MemberRefUser(mod, "op_Implicit", MethodSig.CreateStatic(opInt.MethodSig.RetType, new GenericVar(1)), opInt.DeclaringType);

        var ins = m.Body.Instructions;
        int insertAt = -1;
        for (int i = 1; i < ins.Count; i++)
        {
            var f = ins[i].Operand as IField;
            if (ins[i].OpCode.Code == Code.Stsfld && f != null && f.Name == "ColorLightAdjust" && ins[i - 1].OpCode.Code == Code.Ldc_I4_0)
            { insertAt = i + 1; break; }
        }
        if (insertAt < 0) throw new Exception("找不到默认颜色代码块");

        object[][] table = new object[][]
        {
            new object[] { 0, 210, 85, 0, null },
            new object[] { 1, 180, 50, 0, null },
            new object[] { 2, 130, 40, 0, new int[] { 5, -5, 5 } },
            new object[] { 3, 75, 50, 0, null },
            new object[] { 4, 35, 30, 0, null },
            new object[] { 5, 225, 10, -20, null },
            new object[] { 6, 345, 70, 15, null },
            new object[] { 7, 285, 50, 0, null },
            new object[] { 8, 50, 65, 10, null },
            new object[] { 9, 25, 75, 5, null },
            new object[] { 10, 5, 70, -5, null },
            new object[] { 11, 230, 60, -5, null },
            new object[] { 13, 290, 50, 0, new int[] { -30, 10, 50 } },
            new object[] { 42, 225, 60, -15, null }
        };

        var gen = new List<Instruction>();
        foreach (var row in table)
        {
            gen.Add(Instruction.Create(OpCodes.Ldsfld, fThemeNow));
            gen.Add(Instruction.Create(OpCodes.Ldc_I4, (int)row[0]));
            var next = Instruction.Create(OpCodes.Nop);
            gen.Add(Instruction.Create(OpCodes.Bne_Un, next));
            gen.Add(Instruction.Create(OpCodes.Ldc_I4, (int)row[1]));
            gen.Add(Instruction.Create(OpCodes.Stsfld, fHue));
            gen.Add(Instruction.Create(OpCodes.Ldc_I4, (int)row[2]));
            gen.Add(Instruction.Create(OpCodes.Stsfld, fSat));
            gen.Add(Instruction.Create(OpCodes.Ldc_I4, (int)row[3]));
            gen.Add(Instruction.Create(OpCodes.Stsfld, fLight));
            var deltas = row[4] as int[];
            if (deltas == null)
            {
                gen.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                gen.Add(Instruction.Create(OpCodes.Call, opInt));
            }
            else
            {
                gen.Add(Instruction.Create(OpCodes.Ldc_I4, deltas.Length));
                gen.Add(Instruction.Create(OpCodes.Newarr, mod.CorLibTypes.Int32.ToTypeDefOrRef()));
                for (int k = 0; k < deltas.Length; k++)
                {
                    gen.Add(Instruction.Create(OpCodes.Dup));
                    gen.Add(Instruction.Create(OpCodes.Ldc_I4, k));
                    gen.Add(Instruction.Create(OpCodes.Ldc_I4, deltas[k]));
                    gen.Add(Instruction.Create(OpCodes.Stelem_I4));
                }
                gen.Add(Instruction.Create(OpCodes.Call, opArr));
            }
            gen.Add(Instruction.Create(OpCodes.Stsfld, fDelta));
            var end = Instruction.Create(OpCodes.Nop);
            gen.Add(Instruction.Create(OpCodes.Br, end));
            gen.Add(next);
            gen.Add(end);
        }

        var rand = FindCallN("PCL.ModBase", "RandomInteger", 2);
        var next12 = Instruction.Create(OpCodes.Nop);
        var end12 = Instruction.Create(OpCodes.Nop);
        gen.Add(Instruction.Create(OpCodes.Ldsfld, fThemeNow));
        gen.Add(Instruction.Create(OpCodes.Ldc_I4, 12));
        gen.Add(Instruction.Create(OpCodes.Bne_Un, next12));
        gen.Add(Instruction.Create(OpCodes.Ldc_I4_0));
        gen.Add(Instruction.Create(OpCodes.Ldc_I4, 359));
        gen.Add(Instruction.Create(OpCodes.Call, rand));
        gen.Add(Instruction.Create(OpCodes.Stsfld, fHue));
        gen.Add(Instruction.Create(OpCodes.Ldc_I4, 45));
        gen.Add(Instruction.Create(OpCodes.Stsfld, fSat));
        gen.Add(Instruction.Create(OpCodes.Ldc_I4_0));
        gen.Add(Instruction.Create(OpCodes.Stsfld, fLight));
        gen.Add(Instruction.Create(OpCodes.Ldc_I4_0));
        gen.Add(Instruction.Create(OpCodes.Call, opInt));
        gen.Add(Instruction.Create(OpCodes.Stsfld, fDelta));
        gen.Add(Instruction.Create(OpCodes.Br, end12));
        gen.Add(next12);
        gen.Add(end12);

        var getInt = FindGenericCall("PCL.Settings", "Get", "System.Int32");
        var nextCustom = Instruction.Create(OpCodes.Nop);
        var endCustom = Instruction.Create(OpCodes.Nop);
        gen.Add(Instruction.Create(OpCodes.Ldsfld, fThemeNow));
        gen.Add(Instruction.Create(OpCodes.Ldc_I4, 14));
        gen.Add(Instruction.Create(OpCodes.Bne_Un, nextCustom));
        foreach (var pair in new string[][] { new string[] { "UiLauncherHue", "ColorHue" }, new string[] { "UiLauncherSat", "ColorSat" } })
        {
            gen.Add(Instruction.Create(OpCodes.Ldstr, pair[0]));
            gen.Add(Instruction.Create(OpCodes.Ldnull));
            gen.Add(Instruction.Create(OpCodes.Call, getInt));
            gen.Add(Instruction.Create(OpCodes.Stsfld, type.FindField(pair[1])));
        }
        gen.Add(Instruction.Create(OpCodes.Ldstr, "UiLauncherLight"));
        gen.Add(Instruction.Create(OpCodes.Ldnull));
        gen.Add(Instruction.Create(OpCodes.Call, getInt));
        gen.Add(Instruction.Create(OpCodes.Ldc_I4, 20));
        gen.Add(Instruction.Create(OpCodes.Sub_Ovf));
        gen.Add(Instruction.Create(OpCodes.Stsfld, fLight));
        gen.Add(Instruction.Create(OpCodes.Ldstr, "UiLauncherDelta"));
        gen.Add(Instruction.Create(OpCodes.Ldnull));
        gen.Add(Instruction.Create(OpCodes.Call, getInt));
        gen.Add(Instruction.Create(OpCodes.Ldc_I4, 90));
        gen.Add(Instruction.Create(OpCodes.Sub_Ovf));
        gen.Add(Instruction.Create(OpCodes.Call, opInt));
        gen.Add(Instruction.Create(OpCodes.Stsfld, fDelta));
        gen.Add(Instruction.Create(OpCodes.Br, endCustom));
        gen.Add(nextCustom);
        gen.Add(endCustom);

        for (int i = gen.Count - 1; i >= 0; i--) ins.Insert(insertAt, gen[i]);
        if (m.Body.MaxStack < 5) m.Body.MaxStack = 5;
        body_fix(m);
        Log("  · 每主题配色 + 标题栏渐变表已注入（" + gen.Count + " 条指令）");
    }

    static void body_fix(MethodDef m) { m.Body.UpdateInstructionOffsets(); }

    static void RemoveUpdate()
    {
        StubVoid("PCL.ModSecret", "UpdateStart");
        StubVoid("PCL.ModSecret", "DownloadLatestPCL");
        var chk = M("PCL.ModSecret", "UpdateCheckByButton");
        var hint = M("PCL.ModMain", "Hint");
        chk.Body.ExceptionHandlers.Clear();
        chk.Body.Variables.Clear();
        chk.Body.Instructions.Clear();
        chk.Body.Instructions.Add(Instruction.Create(OpCodes.Ldstr, "此版本已移除 PCL 更新功能"));
        chk.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4_0));
        chk.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4_1));
        chk.Body.Instructions.Add(Instruction.Create(OpCodes.Call, hint));
        chk.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        chk.Body.MaxStack = 3;
        chk.Body.UpdateInstructionOffsets();
        Log("  · UpdateCheckByButton -> 提示");
    }
}
