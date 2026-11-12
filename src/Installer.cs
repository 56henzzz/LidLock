using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

namespace LidLock
{
    /// <summary>Instalar, activar, desactivar y desinstalar. Todo cambio del sistema pasa por aqui.</summary>
    internal static class Installer
    {
        public const string MutexName = "Local\\LidLock.SingleInstance";
        public const string WindowClass = "LidLock.TrayWindow";

        public static string CurrentExe
        {
            get { return Process.GetCurrentProcess().MainModule.FileName; }
        }

        public static string InstallDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LidLock"); }
        }

        public static string InstalledExe
        {
            get { return Path.Combine(InstallDir, "LidLock.exe"); }
        }

        private static string StartMenuShortcut
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "LidLock.lnk"); }
        }

        public static bool IsInstalled
        {
            get { return string.Equals(Path.GetFullPath(CurrentExe), Path.GetFullPath(InstalledExe), StringComparison.OrdinalIgnoreCase); }
        }

        /// <summary>Ejecuta este mismo exe con permisos de administrador y espera el resultado.</summary>
        private static bool RunElevated(string arguments)
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo(CurrentExe, arguments);
                info.Verb = "runas";
                info.UseShellExecute = true;
                info.WindowStyle = ProcessWindowStyle.Hidden;
                using (Process process = Process.Start(info))
                {
                    process.WaitForExit();
                    return process.ExitCode == 0;
                }
            }
            catch (Win32Exception)
            {
                return false; // el usuario cancelo el aviso de UAC
            }
        }

        public const int Ok = 0, NoLid = 1, Failed = 2;

        /// <summary>Guarda los ajustes actuales y pone la tapa en "No hacer nada".</summary>
        public static int Activate()
        {
            if (!PowerSettings.HasLidSetting()) return NoLid;

            Settings.SetSnapshot(PowerSettings.Snapshot());
            if (!RunElevated("--apply") || !PowerSettings.IsApplied())
            {
                Settings.ClearSnapshot();
                return Failed;
            }
            Settings.SetActive(true);
            return Ok;
        }

        /// <summary>Restaura los ajustes que tenia el usuario antes de activar LidLock.</summary>
        public static bool Deactivate()
        {
            string snapshot = Settings.GetSnapshot() ?? "";
            if (!RunElevated("--restore \"" + snapshot + "\"")) return false;

            Settings.ClearSnapshot();
            Settings.SetActive(false);
            return true;
        }

        /// <summary>Primer arranque desde el exe descargado: pide consentimiento, instala y activa.</summary>
        public static int FirstRun()
        {
            Mutex running;
            if (Mutex.TryOpenExisting(MutexName, out running))
            {
                running.Dispose();
                Native.MessageBox(IntPtr.Zero, Strings.Get("AlreadyRunning"), "LidLock", Native.MB_OK | Native.MB_ICONINFORMATION | Native.MB_SETFOREGROUND);
                return 0;
            }

            if (Native.MessageBox(IntPtr.Zero, Strings.Get("ConsentInstall"), "LidLock",
                    Native.MB_OKCANCEL | Native.MB_ICONINFORMATION | Native.MB_SETFOREGROUND) != Native.IDOK)
                return 0;

            int result = Activate();
            if (result != Ok)
            {
                Native.MessageBox(IntPtr.Zero, Strings.Get(result == NoLid ? "ErrNoLid" : "ErrAdmin"), "LidLock",
                    Native.MB_OK | Native.MB_ICONERROR | Native.MB_SETFOREGROUND);
                return 1;
            }

            try
            {
                Directory.CreateDirectory(InstallDir);
                File.Copy(CurrentExe, InstalledExe, true);
                Settings.SetAutostart(true, InstalledExe);
                CreateStartMenuShortcut();
            }
            catch (Exception)
            {
                Deactivate();
                Settings.RemoveAll();
                Native.MessageBox(IntPtr.Zero, Strings.Get("ErrAdmin"), "LidLock", Native.MB_OK | Native.MB_ICONERROR | Native.MB_SETFOREGROUND);
                return 1;
            }

            Process.Start(InstalledExe, "--welcome");
            return 0;
        }

        /// <summary>Restaura los ajustes y elimina LidLock. Devuelve false si no se pudo restaurar (no se borra nada).</summary>
        public static bool Uninstall()
        {
            if (Settings.Active && !Deactivate()) return false;

            Settings.RemoveAll();
            try { File.Delete(StartMenuShortcut); } catch (IOException) { }

            // El exe en uso no se puede borrar mientras corre: un cmd oculto lo borra unos segundos despues.
            ProcessStartInfo cleanup = new ProcessStartInfo("cmd.exe",
                "/c ping -n 3 127.0.0.1 >nul & rmdir /s /q \"" + InstallDir + "\"");
            cleanup.CreateNoWindow = true;
            cleanup.UseShellExecute = false;
            Process.Start(cleanup);
            return true;
        }

        private static void CreateStartMenuShortcut()
        {
            Type shell = Type.GetTypeFromProgID("WScript.Shell");
            object wsh = Activator.CreateInstance(shell);
            object link = shell.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, wsh, new object[] { StartMenuShortcut });
            Type linkType = link.GetType();
            linkType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, link, new object[] { InstalledExe });
            linkType.InvokeMember("Description", BindingFlags.SetProperty, null, link, new object[] { "LidLock" });
            linkType.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
        }
    }
}
