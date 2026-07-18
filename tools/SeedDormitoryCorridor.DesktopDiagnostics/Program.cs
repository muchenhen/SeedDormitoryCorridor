using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace SeedDormitoryCorridor.DesktopDiagnostics;

internal static class Program
{
    private const string ProcessName = "SeedDormitoryCorridor.App";
    private const int GwlExStyle = -20;
    private const long WsExTopMost = 0x00000008;
    private const long WsExTransparent = 0x00000020;
    private const long WsExToolWindow = 0x00000080;
    private const long WsExLayered = 0x00080000;
    private const long WsExAppWindow = 0x00040000;
    private const long WsExNoActivate = 0x08000000;
    private const uint WmNcHitTest = 0x0084;
    private const uint WmMouseActivate = 0x0021;
    private const int HtTransparent = -1;
    private const int HtClient = 1;
    private const int MaNoActivate = 3;
    private const uint MonitorDefaultToNearest = 2;
    private const uint InputMouse = 0;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint GrGdiObjects = 0;
    private const uint GrUserObjects = 1;

    private static int Main(string[] args)
    {
        bool performDrag = args.Any(arg => string.Equals(arg, "--drag", StringComparison.OrdinalIgnoreCase));
        int resourceSeconds = GetOptionalPositiveIntArgument(args, "--resource-seconds");
        using Process process = ResolveProcess(args);
        List<nint> windows = EnumerateProcessWindows(process.Id);
        List<nint> layeredWindows = windows.Where(IsVisibleLayeredWindow).ToList();

        Console.WriteLine($"Process: {process.ProcessName} ({process.Id})");
        Console.WriteLine($"Top-level windows: {windows.Count}; visible layered windows: {layeredWindows.Count}");
        if (layeredWindows.Count != 1)
        {
            foreach (nint window in windows)
            {
                PrintWindow(window);
            }

            Console.Error.WriteLine("FAIL: Expected exactly one visible layered pet window.");
            return 1;
        }

        nint petWindow = layeredWindows[0];
        WindowInfo info = ReadWindowInfo(petWindow);
        PrintWindow(petWindow);

        var checks = new List<CheckResult>
        {
            Check("visible", IsWindowVisible(petWindow), "window is visible"),
            Check("layered", HasStyle(info.ExStyle, WsExLayered), "WS_EX_LAYERED is set"),
            Check("tool-window", HasStyle(info.ExStyle, WsExToolWindow), "WS_EX_TOOLWINDOW is set"),
            Check("no-activate", HasStyle(info.ExStyle, WsExNoActivate), "WS_EX_NOACTIVATE is set"),
            Check("not-app-window", !HasStyle(info.ExStyle, WsExAppWindow), "WS_EX_APPWINDOW is not set"),
            Check("topmost", HasStyle(info.ExStyle, WsExTopMost), "WS_EX_TOPMOST is set"),
            Check("non-empty-bounds", info.Rect.Width > 0 && info.Rect.Height > 0,
                $"bounds are {info.Rect.Width}x{info.Rect.Height}"),
        };

        uint dpi = GetDpiForWindow(petWindow);
        checks.Add(Check("dpi", dpi > 0, $"window DPI is {dpi} ({dpi / 96d:P0})"));

        nint monitor = MonitorFromWindow(petWindow, MonitorDefaultToNearest);
        var monitorInfo = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        bool monitorRead = monitor != 0 && GetMonitorInfo(monitor, ref monitorInfo);
        checks.Add(Check("monitor", monitorRead,
            monitorRead ? $"monitor={monitorInfo.Monitor}; work={monitorInfo.Work}" : "monitor lookup failed"));

        nint foregroundBefore = GetForegroundWindow();
        int mouseActivateResult = unchecked((int)SendMessage(petWindow, WmMouseActivate, 0, 0));
        Thread.Sleep(50);
        nint foregroundAfter = GetForegroundWindow();
        checks.Add(Check("mouse-no-activate", mouseActivateResult == MaNoActivate,
            $"WM_MOUSEACTIVATE returned {mouseActivateResult}"));
        checks.Add(Check("focus-preserved", foregroundBefore == foregroundAfter,
            $"foreground stayed 0x{foregroundBefore:X}"));

        HitTestSummary hitTests = ProbeHitTests(petWindow, info.Rect);
        bool fullClickThrough = HasStyle(info.ExStyle, WsExTransparent);
        bool expectedHitTests = fullClickThrough
            ? hitTests.Client == 0 && hitTests.Transparent > 0
            : hitTests.Client > 0 && hitTests.Transparent > 0;
        checks.Add(Check("pixel-hit-test", expectedHitTests,
            $"client={hitTests.Client}, transparent={hitTests.Transparent}, other={hitTests.Other}, fullClickThrough={fullClickThrough}"));

        checks.Add(resourceSeconds > 0
            ? MonitorResources(process, resourceSeconds)
            : MonitorResources(process, 1));

        if (performDrag)
        {
            try
            {
                checks.Add(PerformControlledDrag(petWindow, info.Rect, monitorInfo));
            }
            catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
            {
                checks.Add(Check("controlled-drag", false, $"input injection failed: {exception.Message}"));
            }
        }

        if (args.Any(arg => string.Equals(arg, "--restore-saved-position", StringComparison.OrdinalIgnoreCase)))
        {
            checks.Add(RestoreSavedPosition(petWindow));
        }

        int positionIndex = Array.FindIndex(args,
            arg => string.Equals(arg, "--position", StringComparison.OrdinalIgnoreCase));
        if (positionIndex >= 0)
        {
            int x = 0;
            int y = 0;
            bool validPosition = positionIndex + 2 < args.Length &&
                int.TryParse(args[positionIndex + 1], System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out x) &&
                int.TryParse(args[positionIndex + 2], System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out y);
            checks.Add(validPosition
                ? SetPosition(petWindow, x, y, "restore-requested-position")
                : Check("restore-requested-position", false, "expected --position <x> <y>"));
        }

        Console.WriteLine();
        foreach (CheckResult check in checks)
        {
            Console.WriteLine($"{(check.Passed ? "PASS" : "FAIL")} {check.Name}: {check.Detail}");
        }

        Console.WriteLine();
        Console.WriteLine("Manual-only: visual alpha/black-edge quality, Alt+Tab list, taskbar UI, mixed-DPI movement, interactive switching, and installer behavior.");
        return checks.All(check => check.Passed) ? 0 : 1;
    }

    private static int GetOptionalPositiveIntArgument(string[] args, string option)
    {
        int index = Array.FindIndex(args, arg => string.Equals(arg, option, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return 0;
        }

        if (index + 1 >= args.Length ||
            !int.TryParse(args[index + 1], System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out int value) || value <= 0)
        {
            throw new ArgumentException($"Expected {option} <positive-seconds>.", nameof(args));
        }

        return value;
    }

    private static Process ResolveProcess(string[] args)
    {
        int pidIndex = Array.FindIndex(args, arg => string.Equals(arg, "--pid", StringComparison.OrdinalIgnoreCase));
        if (pidIndex >= 0)
        {
            if (pidIndex + 1 >= args.Length ||
                !int.TryParse(args[pidIndex + 1], System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out int processId))
            {
                throw new ArgumentException("Expected --pid <process-id>.", nameof(args));
            }

            return Process.GetProcessById(processId);
        }

        Process[] processes = Process.GetProcessesByName(ProcessName);
        if (processes.Length != 1)
        {
            foreach (Process process in processes)
            {
                process.Dispose();
            }

            throw new InvalidOperationException($"Expected exactly one {ProcessName} process; found {processes.Length}.");
        }

        return processes[0];
    }

    private static CheckResult MonitorResources(Process process, int durationSeconds)
    {
        ResourceSample baseline = CaptureResourceSample(process);
        ResourceSample peak = baseline;
        ResourceSample current = baseline;
        var stopwatch = Stopwatch.StartNew();
        TimeSpan nextProgress = TimeSpan.FromMinutes(1);
        TimeSpan target = TimeSpan.FromSeconds(durationSeconds);

        while (stopwatch.Elapsed < target)
        {
            TimeSpan remaining = target - stopwatch.Elapsed;
            Thread.Sleep(remaining < TimeSpan.FromSeconds(5) ? remaining : TimeSpan.FromSeconds(5));
            if (process.HasExited)
            {
                return Check("resource-stability", false, $"process exited after {stopwatch.Elapsed.TotalSeconds:F1}s");
            }

            current = CaptureResourceSample(process);
            peak = new ResourceSample(
                Math.Max(peak.GdiObjects, current.GdiObjects),
                Math.Max(peak.UserObjects, current.UserObjects),
                Math.Max(peak.WorkingSetBytes, current.WorkingSetBytes),
                Math.Max(peak.PrivateBytes, current.PrivateBytes),
                current.CpuTime);

            if (stopwatch.Elapsed >= nextProgress && durationSeconds >= 60)
            {
                Console.WriteLine($"RESOURCE t={stopwatch.Elapsed.TotalMinutes:F1}m GDI={current.GdiObjects} USER={current.UserObjects} " +
                    $"WS={ToMiB(current.WorkingSetBytes):F1}MiB Private={ToMiB(current.PrivateBytes):F1}MiB");
                nextProgress += TimeSpan.FromMinutes(1);
            }
        }

        double averageCpuPercent = (current.CpuTime - baseline.CpuTime).TotalSeconds /
            Math.Max(stopwatch.Elapsed.TotalSeconds * Environment.ProcessorCount, 0.001) * 100;
        bool stable = current.GdiObjects <= baseline.GdiObjects + 2 &&
            current.UserObjects <= baseline.UserObjects + 2 &&
            peak.GdiObjects <= baseline.GdiObjects + 4 &&
            peak.UserObjects <= baseline.UserObjects + 4 &&
            current.WorkingSetBytes <= baseline.WorkingSetBytes + (32L * 1024 * 1024) &&
            current.PrivateBytes <= baseline.PrivateBytes + (16L * 1024 * 1024) &&
            averageCpuPercent <= 2;
        return Check(durationSeconds >= 60 ? "long-resource-stability" : "short-resource-stability", stable,
            $"duration={stopwatch.Elapsed.TotalSeconds:F1}s; " +
            $"GDI {baseline.GdiObjects}->{current.GdiObjects} peak {peak.GdiObjects}; " +
            $"USER {baseline.UserObjects}->{current.UserObjects} peak {peak.UserObjects}; " +
            $"WS {ToMiB(baseline.WorkingSetBytes):F1}->{ToMiB(current.WorkingSetBytes):F1}MiB peak {ToMiB(peak.WorkingSetBytes):F1}; " +
            $"Private {ToMiB(baseline.PrivateBytes):F1}->{ToMiB(current.PrivateBytes):F1}MiB peak {ToMiB(peak.PrivateBytes):F1}; " +
            $"averageCPU={averageCpuPercent:F3}%");
    }

    private static ResourceSample CaptureResourceSample(Process process)
    {
        process.Refresh();
        return new ResourceSample(
            GetGuiResources(process.Handle, GrGdiObjects),
            GetGuiResources(process.Handle, GrUserObjects),
            process.WorkingSet64,
            process.PrivateMemorySize64,
            process.TotalProcessorTime);
    }

    private static double ToMiB(long bytes) => bytes / (1024d * 1024d);

    private static List<nint> EnumerateProcessWindows(int processId)
    {
        var windows = new List<nint>();
        EnumWindows((window, parameter) =>
        {
            _ = GetWindowThreadProcessId(window, out uint ownerProcessId);
            if (ownerProcessId == (uint)processId)
            {
                windows.Add(window);
            }

            return true;
        }, 0);
        return windows;
    }

    private static bool IsVisibleLayeredWindow(nint window)
    {
        long exStyle = GetWindowLongPtr(window, GwlExStyle).ToInt64();
        return IsWindowVisible(window) && HasStyle(exStyle, WsExLayered);
    }

    private static WindowInfo ReadWindowInfo(nint window)
    {
        if (!GetWindowRect(window, out NativeRect rect))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "GetWindowRect failed.");
        }

        return new WindowInfo(rect, GetWindowLongPtr(window, GwlExStyle).ToInt64(), GetClassNameText(window), GetWindowTextValue(window));
    }

    private static void PrintWindow(nint window)
    {
        WindowInfo info = ReadWindowInfo(window);
        Console.WriteLine($"Window 0x{window:X}: visible={IsWindowVisible(window)}, rect={info.Rect}, exStyle=0x{info.ExStyle:X8}, class='{info.ClassName}', title='{info.Title}'");
    }

    private static HitTestSummary ProbeHitTests(nint window, NativeRect rect)
    {
        int client = 0;
        int transparent = 0;
        int other = 0;
        ScreenPoint? bestClient = null;
        long bestDistanceSquared = long.MaxValue;
        int centerX = rect.Left + (rect.Width / 2);
        int centerY = rect.Top + (rect.Height / 2);
        const int grid = 13;
        for (int row = 0; row < grid; row++)
        {
            for (int column = 0; column < grid; column++)
            {
                int x = rect.Left + ((column * 2 + 1) * rect.Width / (grid * 2));
                int y = rect.Top + ((row * 2 + 1) * rect.Height / (grid * 2));
                int result = unchecked((int)SendMessage(window, WmNcHitTest, 0, MakePointParameter(x, y)));
                if (result == HtClient)
                {
                    client++;
                    long deltaX = x - centerX;
                    long deltaY = y - centerY;
                    long distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
                    if (distanceSquared < bestDistanceSquared)
                    {
                        bestDistanceSquared = distanceSquared;
                        bestClient = new ScreenPoint(x, y);
                    }
                }
                else if (result == HtTransparent)
                {
                    transparent++;
                }
                else
                {
                    other++;
                }
            }
        }

        return new HitTestSummary(client, transparent, other, bestClient);
    }

    private static CheckResult PerformControlledDrag(nint window, NativeRect originalRect, MonitorInfo monitorInfo)
    {
        ScreenPoint? clientPoint = ProbeHitTests(window, originalRect).BestClientPoint;
        if (clientPoint is null)
        {
            return Check("controlled-drag", false, "no opaque client point was found");
        }

        if (!GetCursorPos(out ScreenPoint originalCursor))
        {
            return Check("controlled-drag", false, "GetCursorPos failed");
        }

        int deltaX = originalRect.Right + 64 < monitorInfo.Work.Right ? 48 : -48;
        try
        {
            nint foregroundBefore = GetForegroundWindow();
            Drag(window, clientPoint.Value, new ScreenPoint(clientPoint.Value.X + deltaX, clientPoint.Value.Y));
            Thread.Sleep(150);
            if (!GetWindowRect(window, out NativeRect movedRect))
            {
                return Check("controlled-drag", false, "could not read moved bounds");
            }

            int actualDeltaX = movedRect.Left - originalRect.Left;
            bool moved = Math.Sign(actualDeltaX) == Math.Sign(deltaX) && Math.Abs(actualDeltaX) >= 4 && movedRect.Top == originalRect.Top;
            if (!moved)
            {
                return Check("controlled-drag", false, $"window delta was ({actualDeltaX},{movedRect.Top - originalRect.Top})");
            }

            Thread.Sleep(100);
            bool released = GetWindowRect(window, out NativeRect releasedRect) &&
                releasedRect.Left == movedRect.Left && releasedRect.Top == movedRect.Top;
            bool focusPreserved = GetForegroundWindow() == foregroundBefore;
            return Check("controlled-drag", moved && released && focusPreserved,
                $"requestedDeltaX={deltaX}, actualDeltaX={actualDeltaX}, released={released}, focusPreserved={focusPreserved}");
        }
        finally
        {
            _ = SetCursorPos(originalCursor.X, originalCursor.Y);
            if (GetWindowRect(window, out NativeRect finalRect) &&
                (finalRect.Left != originalRect.Left || finalRect.Top != originalRect.Top))
            {
                _ = SetWindowPos(window, 0, originalRect.Left, originalRect.Top, 0, 0, 0x0001 | 0x0004 | 0x0010);
            }
        }
    }

    private static CheckResult RestoreSavedPosition(nint window)
    {
        string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SeedDormitoryCorridor", "settings.json");
        if (!File.Exists(settingsPath))
        {
            return Check("restore-saved-position", false, $"settings file not found: {settingsPath}");
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(settingsPath));
        if (!document.RootElement.TryGetProperty("x", out JsonElement xElement) ||
            !document.RootElement.TryGetProperty("y", out JsonElement yElement) ||
            !xElement.TryGetInt32(out int x) || !yElement.TryGetInt32(out int y))
        {
            return Check("restore-saved-position", false, "settings do not contain integer x/y values");
        }

        return SetPosition(window, x, y, "restore-saved-position");
    }

    private static CheckResult SetPosition(nint window, int x, int y, string checkName)
    {
        if (!SetWindowPos(window, 0, x, y, 0, 0, 0x0001 | 0x0004 | 0x0010))
        {
            return Check(checkName, false, new Win32Exception(Marshal.GetLastWin32Error()).Message);
        }

        Thread.Sleep(100);
        bool restored = GetWindowRect(window, out NativeRect rect) && rect.Left == x && rect.Top == y;
        return Check(checkName, restored, $"requested=({x},{y}), actual=({rect.Left},{rect.Top})");
    }

    private static void Drag(nint window, ScreenPoint from, ScreenPoint to)
    {
        if (!SetCursorPos(from.X, from.Y))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SetCursorPos failed.");
        }

        Thread.Sleep(40);
        int hitTest = unchecked((int)SendMessage(window, WmNcHitTest, 0, MakePointParameter(from.X, from.Y)));
        if (hitTest != HtClient)
        {
            throw new InvalidOperationException($"Drag start became non-client before mouse down (hit={hitTest}).");
        }

        nint windowAtPoint = WindowFromPoint(from);
        if (windowAtPoint != window)
        {
            throw new InvalidOperationException($"WindowFromPoint returned 0x{windowAtPoint:X}, expected 0x{window:X}.");
        }

        SendMouseButton(MouseEventLeftDown);
        try
        {
            const int steps = 6;
            for (int step = 1; step <= steps; step++)
            {
                int x = from.X + ((to.X - from.X) * step / steps);
                int y = from.Y + ((to.Y - from.Y) * step / steps);
                _ = SetCursorPos(x, y);
                Thread.Sleep(step == steps ? 100 : 25);
            }
        }
        finally
        {
            SendMouseButton(MouseEventLeftUp);
        }
    }

    private static void SendMouseButton(uint flags)
    {
        NativeInput[] inputs =
        [
            new NativeInput
            {
                Type = InputMouse,
                Data = new InputUnion
                {
                    Mouse = new MouseInput { Flags = flags },
                },
            },
        ];
        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeInput>());
        if (sent != inputs.Length)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"SendInput sent {sent} of {inputs.Length} events.");
        }
    }

    private static string GetClassNameText(nint window)
    {
        var buffer = new char[256];
        int length = GetClassName(window, buffer, buffer.Length);
        return new string(buffer, 0, length);
    }

    private static string GetWindowTextValue(nint window)
    {
        int length = GetWindowTextLength(window);
        var buffer = new char[length + 1];
        int copied = GetWindowText(window, buffer, buffer.Length);
        return new string(buffer, 0, copied);
    }

    private static bool HasStyle(long value, long style) => (value & style) == style;

    private static nint MakePointParameter(int x, int y) => unchecked((nint)((uint)(ushort)x | ((uint)(ushort)y << 16)));

    private static CheckResult Check(string name, bool passed, string detail) => new(name, passed, detail);

    private sealed record CheckResult(string Name, bool Passed, string Detail);

    private sealed record WindowInfo(NativeRect Rect, long ExStyle, string ClassName, string Title);

    private readonly record struct HitTestSummary(int Client, int Transparent, int Other, ScreenPoint? BestClientPoint);

    private readonly record struct ResourceSample(uint GdiObjects, uint UserObjects, long WorkingSetBytes,
        long PrivateBytes, TimeSpan CpuTime);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct ScreenPoint(int X, int Y);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeRect
    {
        internal readonly int Left;
        internal readonly int Top;
        internal readonly int Right;
        internal readonly int Bottom;
        internal int Width => Right - Left;
        internal int Height => Bottom - Top;
        public override string ToString() => $"({Left},{Top})-({Right},{Bottom}) {Width}x{Height}";
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        internal uint Size;
        internal NativeRect Monitor;
        internal NativeRect Work;
        internal uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeInput
    {
        internal uint Type;
        internal InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        internal MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        internal int X;
        internal int Y;
        internal uint MouseData;
        internal uint Flags;
        internal uint Time;
        internal nuint ExtraInfo;
    }

    private delegate bool EnumWindowsCallback(nint window, nint parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsCallback callback, nint parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint window);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out NativeRect rect);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint window, int index);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint window, [Out] char[] className, int maximumCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint window, [Out] char[] text, int maximumCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(nint window);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint window, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint window);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint window, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out ScreenPoint point);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern nint WindowFromPoint(ScreenPoint point);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, [In] NativeInput[] inputs, int inputSize);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern uint GetGuiResources(nint process, uint flags);
}
