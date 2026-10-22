using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;
using Verse;

namespace RimTouch
{
    // Registers only the current process game window. No global hooks or input injection.
    internal static class WindowsTouchBackend
    {
        private const uint WmTouch = 0x0240, WmKillFocus = 0x0008, WmCancelMode = 0x001F, WmNcDestroy = 0x0082;
        private const uint Down = 0x0002, Up = 0x0004;
        private static readonly UIntPtr SubclassId = new UIntPtr(0x52544F55);
        private static readonly SubclassProc Callback = WindowProc;
        private static IntPtr window;
        private static bool registeredHere;
        public static bool ForwardPromotedMouse { get; private set; }
        private static long retryAt;
        private static bool loggedFailure;
        public static string Status { get; private set; } = "Windows touch: waiting for game window";
        [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] internal struct TouchInfo
        {
            public int X, Y;
            public IntPtr Source;
            public uint Id, Flags, Mask, Time;
            public UIntPtr ExtraInfo;
            public uint Width, Height;
        }
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate IntPtr SubclassProc(IntPtr hwnd, uint message, UIntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr data);
        [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool RegisterTouchWindow(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnregisterTouchWindow(IntPtr hwnd);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsTouchWindow(IntPtr hwnd, out uint flags);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetTouchInputInfo(IntPtr handle, uint count, [Out] TouchInfo[] points, int size);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseTouchInputHandle(IntPtr handle);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ScreenToClient(IntPtr hwnd, ref Point point);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetClientRect(IntPtr hwnd, out Rect rect);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint process);
        [DllImport("user32.dll")] private static extern IntPtr GetMessageExtraInfo();
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("comctl32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowSubclass(IntPtr hwnd, SubclassProc callback, UIntPtr id, UIntPtr data);
        [DllImport("comctl32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool RemoveWindowSubclass(IntPtr hwnd, SubclassProc callback, UIntPtr id);
        [DllImport("comctl32.dll")] private static extern IntPtr DefSubclassProc(IntPtr hwnd, uint message, UIntPtr wParam, IntPtr lParam);

        public static void Poll(bool enabled)
        {
            if (!OperatingSystem.IsWindows()) { Status = "Native touch unavailable: Windows required"; return; }
            if (!enabled) { Detach(); Status = "Touch mode disabled"; return; }
            if (window != IntPtr.Zero && IsWindow(window)) return;
            if (Environment.TickCount64 < retryAt) return;
            retryAt = Environment.TickCount64 + 1000;
            try
            {
                using var process = Process.GetCurrentProcess();
                IntPtr candidate = process.MainWindowHandle;
                if (candidate == IntPtr.Zero) return;
                uint thread = GetWindowThreadProcessId(candidate, out uint owner);
                if (owner != (uint)process.Id || thread != GetCurrentThreadId())
                { Status = "Windows touch: waiting for game window thread"; return; }
                bool alreadyRegistered = IsTouchWindow(candidate, out _);
                if (!SetWindowSubclass(candidate, Callback, SubclassId, UIntPtr.Zero))
                    throw new InvalidOperationException("Window subclass failed: " + Marshal.GetLastWin32Error());
                if (!alreadyRegistered && !RegisterTouchWindow(candidate, 0))
                {
                    RemoveWindowSubclass(candidate, Callback, SubclassId);
                    throw new InvalidOperationException("Touch registration failed: " + Marshal.GetLastWin32Error());
                }
                window = candidate; registeredHere = !alreadyRegistered;
                Status = "Windows native touch ready";
                Log.Message("[RimTouch] Windows native touch ready; game window only.");
            }
            catch (Exception error)
            {
                Status = "Windows touch unavailable: " + error.Message;
                if (!loggedFailure) { loggedFailure = true; Log.Warning("[RimTouch] " + Status); }
            }
        }
        public static void Detach()
        {
            if (window != IntPtr.Zero && OperatingSystem.IsWindows() && IsWindow(window))
            {
                RemoveWindowSubclass(window, Callback, SubclassId);
                if (registeredHere) UnregisterTouchWindow(window);
            }
            window = IntPtr.Zero; registeredHere = false; TouchInput.Buffer.Clear();
        }
        private static IntPtr WindowProc(IntPtr hwnd, uint message, UIntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr data)
        {
            bool enabled = RimTouchMod.Settings != null && RimTouchMod.Settings.enableTouchMode;
            if (message == WmTouch && enabled)
            {
                try
                {
                    uint count = (uint)(wParam.ToUInt64() & 0xFFFF);
                    if (count > 0 && count <= 256 && GetClientRect(hwnd, out Rect bounds))
                    {
                        var inputs = new TouchInfo[count];
                        if (GetTouchInputInfo(lParam, count, inputs, Marshal.SizeOf<TouchInfo>()))
                            foreach (var input in inputs)
                            {
                                var point = new Point { X = input.X / 100, Y = input.Y / 100 };
                                if (!ScreenToClient(hwnd, ref point)) continue;
                                Vector2 position = TouchContactBuffer.ToRenderPosition(point.X, point.Y, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top, Screen.width, Screen.height);
                                TouchPhase phase = (input.Flags & Down) != 0 ? TouchPhase.Began : (input.Flags & Up) != 0 ? TouchPhase.Ended : TouchPhase.Moved;
                                if (phase == TouchPhase.Began && (input.Flags & 0x10) != 0)
                                    ForwardPromotedMouse = TouchInputDriver.NeedsNativeMouse(position);
                                TouchInput.Buffer.Enqueue(unchecked((int)input.Id), position, phase);
                            }
                    }
                }
                catch { TouchInput.Buffer.CancelAll(); }
                finally { CloseTouchInputHandle(lParam); }
                return IntPtr.Zero;
            }
            if (message == WmKillFocus || message == WmCancelMode) TouchInput.Buffer.CancelAll();
            if (message == WmNcDestroy)
            {
                TouchInput.Buffer.Clear();
                RemoveWindowSubclass(hwnd, Callback, SubclassId);
                window = IntPtr.Zero; registeredHere = false;
            }
            // Ignore only touch-promoted mouse clicks; physical mouse input continues normally.
            if (enabled && !ForwardPromotedMouse && message >= 0x0200 && message <= 0x020E &&
                ((ulong)GetMessageExtraInfo().ToInt64() & 0xFFFFFF80UL) == 0xFF515780UL)
                return IntPtr.Zero;
            return DefSubclassProc(hwnd, message, wParam, lParam);
        }
    }
}
