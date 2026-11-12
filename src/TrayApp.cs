using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace LidLock
{
    /// <summary>
    /// Icono de bandeja + vigilante de la tapa. Una ventana oculta recibe los avisos de Windows;
    /// mientras no pasa nada el proceso duerme en GetMessage (0 % de CPU).
    /// </summary>
    internal sealed class TrayApp
    {
        private const uint WM_TRAY = Native.WM_APP + 1;
        private const uint WM_UPDATE_DONE = Native.WM_APP + 2;
        private const uint WM_SHOW_RUNNING = Native.WM_APP + 3;

        private const uint IDM_HEADER = 100, IDM_TOGGLE = 1, IDM_AUTOSTART = 2, IDM_UPDATE = 3, IDM_GITHUB = 4, IDM_UNINSTALL = 5, IDM_EXIT = 6;

        private static readonly int[] IconSizes = { 16, 20, 24, 32, 40, 48 };
        private static Guid lidSwitch = new Guid("BA3E0F4D-B817-4094-A2D1-D56379E6A0F3");

        private readonly Native.WndProc windowProc;
        private readonly bool showWelcome;
        private IntPtr window;
        private IntPtr iconOn;
        private IntPtr iconOff;
        private uint taskbarCreated;
        private bool active;
        private bool notifiedUpdate;

        public TrayApp(bool showWelcome)
        {
            this.showWelcome = showWelcome;
            windowProc = WindowProc; // referencia viva: el GC no debe recoger el delegado
        }

        public int Run()
        {
            active = Settings.Active;

            Native.WNDCLASSEX wc = new Native.WNDCLASSEX();
            wc.cbSize = (uint)Marshal.SizeOf(typeof(Native.WNDCLASSEX));
            wc.lpfnWndProc = windowProc;
            wc.hInstance = Native.GetModuleHandle(null);
            wc.lpszClassName = Installer.WindowClass;
            Native.RegisterClassEx(ref wc);

            const int WS_EX_TOOLWINDOW = 0x80;
            window = Native.CreateWindowEx(WS_EX_TOOLWINDOW, Installer.WindowClass, "LidLock", 0, 0, 0, 0, 0,
                IntPtr.Zero, IntPtr.Zero, wc.hInstance, IntPtr.Zero);
            if (window == IntPtr.Zero) return 1;

            taskbarCreated = Native.RegisterWindowMessage("TaskbarCreated");
            Native.RegisterPowerSettingNotification(window, ref lidSwitch, 0);
            Native.EnableThemedMenus();

            LoadIcons();
            AddIcon();
            if (showWelcome) ShowBalloon(Strings.Get("Welcome"));

            StartUpdateCheck(false);
            Native.TrimMemory();

            Native.MSG msg;
            while (Native.GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)
            {
                Native.TranslateMessage(ref msg);
                Native.DispatchMessage(ref msg);
            }

            Native.DestroyIcon(iconOn);
            Native.DestroyIcon(iconOff);
            return 0;
        }

        // ---- ventana ----

        private IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (msg == WM_TRAY)
                {
                    uint mouse = (uint)(lParam.ToInt64() & 0xFFFF);
                    if (mouse == Native.WM_RBUTTONUP || mouse == Native.WM_LBUTTONUP || mouse == Native.WM_CONTEXTMENU) ShowMenu();
                    return IntPtr.Zero;
                }
                if (msg == Native.WM_POWERBROADCAST && wParam.ToInt64() == Native.PBT_POWERSETTINGCHANGE)
                {
                    OnPowerSetting(lParam);
                    return (IntPtr)1;
                }
                if (msg == WM_UPDATE_DONE)
                {
                    OnUpdateChecked((int)lParam, wParam != IntPtr.Zero);
                    return IntPtr.Zero;
                }
                if (msg == WM_SHOW_RUNNING)
                {
                    ShowBalloon(Strings.Get("AlreadyRunning"));
                    return IntPtr.Zero;
                }
                if (msg == taskbarCreated && taskbarCreated != 0)
                {
                    AddIcon(); // el Explorador se reinicio: hay que volver a poner el icono
                    return IntPtr.Zero;
                }
                if (msg == Native.WM_DESTROY)
                {
                    Native.PostQuitMessage(0);
                    return IntPtr.Zero;
                }
            }
            catch (Exception)
            {
                // Una excepcion aqui cerraria el proceso: el vigilante debe seguir vivo.
            }
            return Native.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        /// <summary>Estructura POWERBROADCAST_SETTING: GUID (16 bytes), longitud (4), datos. Tapa cerrada = 0.</summary>
        private void OnPowerSetting(IntPtr lParam)
        {
            Guid setting = (Guid)Marshal.PtrToStructure(lParam, typeof(Guid));
            if (setting != lidSwitch || !active) return;
            if (Marshal.ReadByte(lParam, 20) == 0) Native.LockWorkStation();
        }

        // ---- icono de bandeja ----

        private void LoadIcons()
        {
            int wanted = Native.GetSystemMetrics(49); // SM_CXSMICON
            int size = IconSizes[IconSizes.Length - 1];
            foreach (int candidate in IconSizes)
            {
                if (candidate >= wanted) { size = candidate; break; }
            }
            iconOn = LoadIcon("on", size);
            iconOff = LoadIcon("off", size);
        }

        private static IntPtr LoadIcon(string state, int size)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("LidLock." + state + "-" + size + ".png"))
            {
                if (stream == null) return IntPtr.Zero;
                byte[] bytes = new byte[stream.Length];
                int read = 0;
                while (read < bytes.Length) read += stream.Read(bytes, read, bytes.Length - read);
                return Native.CreateIconFromResourceEx(bytes, (uint)bytes.Length, true, 0x00030000, size, size, 0);
            }
        }

        private Native.NOTIFYICONDATA NewIconData(uint flags)
        {
            Native.NOTIFYICONDATA data = new Native.NOTIFYICONDATA();
            data.cbSize = (uint)Marshal.SizeOf(typeof(Native.NOTIFYICONDATA));
            data.hWnd = window;
            data.uID = 1;
            data.uFlags = flags;
            return data;
        }

        private string Tooltip()
        {
            return Strings.Get("Tooltip", Strings.Get(active ? "Active" : "Inactive"));
        }

        private void AddIcon()
        {
            Native.NOTIFYICONDATA data = NewIconData(Native.NIF_MESSAGE | Native.NIF_ICON | Native.NIF_TIP);
            data.uCallbackMessage = WM_TRAY;
            data.hIcon = active ? iconOn : iconOff;
            data.szTip = Tooltip();
            Native.Shell_NotifyIcon(Native.NIM_ADD, ref data);
        }

        private void RefreshIcon()
        {
            Native.NOTIFYICONDATA data = NewIconData(Native.NIF_ICON | Native.NIF_TIP);
            data.hIcon = active ? iconOn : iconOff;
            data.szTip = Tooltip();
            Native.Shell_NotifyIcon(Native.NIM_MODIFY, ref data);
        }

        private void ShowBalloon(string text)
        {
            Native.NOTIFYICONDATA data = NewIconData(Native.NIF_INFO);
            data.szInfoTitle = "LidLock";
            data.szInfo = text;
            data.dwInfoFlags = Native.NIIF_INFO;
            Native.Shell_NotifyIcon(Native.NIM_MODIFY, ref data);
        }

        private void Quit()
        {
            Native.NOTIFYICONDATA data = NewIconData(0);
            Native.Shell_NotifyIcon(Native.NIM_DELETE, ref data);
            Native.DestroyWindow(window);
        }

        // ---- menu ----

        private void ShowMenu()
        {
            IntPtr menu = Native.CreatePopupMenu();
            Native.AppendMenu(menu, Native.MF_STRING | Native.MF_GRAYED, (UIntPtr)IDM_HEADER,
                "LidLock " + BuildInfo.Version + "  —  " + Strings.Get(active ? "Active" : "Inactive"));
            Native.AppendMenu(menu, Native.MF_SEPARATOR, UIntPtr.Zero, null);
            Native.AppendMenu(menu, Native.MF_STRING | (active ? Native.MF_CHECKED : 0), (UIntPtr)IDM_TOGGLE, Strings.Get("MenuToggle"));
            Native.AppendMenu(menu, Native.MF_STRING | (Settings.Autostart ? Native.MF_CHECKED : 0), (UIntPtr)IDM_AUTOSTART, Strings.Get("MenuAutostart"));
            Native.AppendMenu(menu, Native.MF_SEPARATOR, UIntPtr.Zero, null);

            if (Updater.HasUpdate)
            {
                Native.AppendMenu(menu, Native.MF_STRING, (UIntPtr)IDM_UPDATE, Strings.Get("MenuUpdate", Updater.LatestTag));
                Native.SetMenuDefaultItem(menu, IDM_UPDATE, 0);
            }
            else
            {
                Native.AppendMenu(menu, Native.MF_STRING, (UIntPtr)IDM_UPDATE, Strings.Get("MenuCheck"));
            }
            Native.AppendMenu(menu, Native.MF_STRING, (UIntPtr)IDM_GITHUB, Strings.Get("MenuGitHub"));
            Native.AppendMenu(menu, Native.MF_SEPARATOR, UIntPtr.Zero, null);
            Native.AppendMenu(menu, Native.MF_STRING, (UIntPtr)IDM_UNINSTALL, Strings.Get("MenuUninstall"));
            Native.AppendMenu(menu, Native.MF_STRING, (UIntPtr)IDM_EXIT, Strings.Get("MenuExit"));

            Native.POINT cursor;
            Native.GetCursorPos(out cursor);
            Native.SetForegroundWindow(window); // sin esto el menu no se cierra al hacer clic fuera
            int command = Native.TrackPopupMenuEx(menu, Native.TPM_RIGHTBUTTON | Native.TPM_BOTTOMALIGN | Native.TPM_RETURNCMD,
                cursor.X, cursor.Y, window, IntPtr.Zero);
            Native.PostMessage(window, 0, IntPtr.Zero, IntPtr.Zero);
            Native.DestroyMenu(menu);

            switch ((uint)command)
            {
                case IDM_TOGGLE: ToggleActive(); break;
                case IDM_AUTOSTART: Settings.SetAutostart(!Settings.Autostart, Installer.InstalledExe); break;
                case IDM_UPDATE: OnUpdateClicked(); break;
                case IDM_GITHUB: OpenGitHub(); break;
                case IDM_UNINSTALL: UninstallClicked(); break;
                case IDM_EXIT: ExitClicked(); break;
            }
            Native.TrimMemory();
        }

        private static void Message(string text, uint icon)
        {
            Native.MessageBox(IntPtr.Zero, text, "LidLock", Native.MB_OK | icon | Native.MB_SETFOREGROUND);
        }

        private void ToggleActive()
        {
            if (active)
            {
                if (Installer.Deactivate()) active = false;
                else Message(Strings.Get("ErrAdmin"), Native.MB_ICONERROR);
            }
            else
            {
                if (Native.MessageBox(IntPtr.Zero, Strings.Get("ConsentActivate"), "LidLock",
                        Native.MB_OKCANCEL | Native.MB_ICONQUESTION | Native.MB_SETFOREGROUND) != Native.IDOK) return;

                int result = Installer.Activate();
                if (result == Installer.Ok) active = true;
                else Message(Strings.Get(result == Installer.NoLid ? "ErrNoLid" : "ErrAdmin"), Native.MB_ICONERROR);
            }
            RefreshIcon();
        }

        private void UninstallClicked()
        {
            if (Native.MessageBox(IntPtr.Zero, Strings.Get("ConfirmUninstall"), "LidLock",
                    Native.MB_YESNO | Native.MB_ICONQUESTION | Native.MB_SETFOREGROUND) != Native.IDYES) return;

            if (Installer.Uninstall()) Quit();
            else Message(Strings.Get("ErrAdmin"), Native.MB_ICONERROR);
        }

        private void ExitClicked()
        {
            if (active)
            {
                int answer = Native.MessageBox(IntPtr.Zero, Strings.Get("ExitAsk"), "LidLock",
                    Native.MB_YESNOCANCEL | Native.MB_ICONQUESTION | Native.MB_SETFOREGROUND);
                if (answer == Native.IDCANCEL) return;
                if (answer == Native.IDYES && !Installer.Deactivate())
                {
                    Message(Strings.Get("ErrAdmin"), Native.MB_ICONERROR);
                    return;
                }
            }
            Quit();
        }

        private static void OpenGitHub()
        {
            if (!Updater.Enabled) return;
            Process.Start(new ProcessStartInfo("https://github.com/" + BuildInfo.Repo) { UseShellExecute = true });
        }

        // ---- actualizaciones ----

        private void StartUpdateCheck(bool manual)
        {
            Thread worker = new Thread(delegate()
            {
                if (!manual) Thread.Sleep(15000); // no competir con el arranque de Windows
                int result = Updater.Check();
                Native.PostMessage(window, WM_UPDATE_DONE, manual ? (IntPtr)1 : IntPtr.Zero, (IntPtr)result);
            });
            worker.IsBackground = true;
            worker.Priority = ThreadPriority.BelowNormal;
            worker.Start();
        }

        private void OnUpdateChecked(int result, bool manual)
        {
            if (result == Updater.Available && !notifiedUpdate)
            {
                notifiedUpdate = true;
                ShowBalloon(Strings.Get("UpdateBalloon", Updater.LatestTag));
            }
            if (manual)
            {
                if (result == Updater.UpToDate) Message(Strings.Get("UpToDate", BuildInfo.Version), Native.MB_ICONINFORMATION);
                else if (result == Updater.NoRepo) Message(Strings.Get("UpdateNoRepo"), Native.MB_ICONINFORMATION);
                else if (result == Updater.Failed) Message(Strings.Get("UpdateFailed"), Native.MB_ICONERROR);
            }
            Native.TrimMemory();
        }

        private void OnUpdateClicked()
        {
            if (!Updater.HasUpdate)
            {
                StartUpdateCheck(true);
                return;
            }
            if (Updater.DownloadAndStage()) Quit();
            else Message(Strings.Get("UpdateFailed"), Native.MB_ICONERROR);
        }

        /// <summary>Si ya hay una instancia, le pide que muestre un aviso (lo usa Program al abrir LidLock dos veces).</summary>
        public static void NotifyRunningInstance()
        {
            IntPtr other = Native.FindWindow(Installer.WindowClass, null);
            if (other != IntPtr.Zero) Native.PostMessage(other, WM_SHOW_RUNNING, IntPtr.Zero, IntPtr.Zero);
        }
    }
}
