using System;
using Microsoft.Win32;

namespace LidLock
{
    /// <summary>
    /// Estado de LidLock en el registro de tu usuario (HKCU). Guarda si esta activo y los ajustes
    /// originales de la tapa; Installer.Uninstall borra todo.
    /// </summary>
    internal static class Settings
    {
        private const string KeyPath = @"Software\LidLock";
        private const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunName = "LidLock";

        public static bool Active
        {
            get
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(KeyPath))
                {
                    return key != null && Convert.ToInt32(key.GetValue("Active", 0)) == 1;
                }
            }
        }

        public static void SetActive(bool value)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(KeyPath))
            {
                key.SetValue("Active", value ? 1 : 0, RegistryValueKind.DWord);
            }
        }

        public static string GetSnapshot()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(KeyPath))
            {
                return key == null ? null : key.GetValue("Snapshot") as string;
            }
        }

        public static void SetSnapshot(string snapshot)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(KeyPath))
            {
                key.SetValue("Snapshot", snapshot, RegistryValueKind.String);
            }
        }

        public static void ClearSnapshot()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(KeyPath, true))
            {
                if (key != null) key.DeleteValue("Snapshot", false);
            }
        }

        public static bool Autostart
        {
            get
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunPath))
                {
                    return key != null && key.GetValue(RunName) != null;
                }
            }
        }

        public static void SetAutostart(bool enabled, string exePath)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunPath))
            {
                if (enabled) key.SetValue(RunName, "\"" + exePath + "\"", RegistryValueKind.String);
                else key.DeleteValue(RunName, false);
            }
        }

        /// <summary>Borra todo rastro de LidLock del registro.</summary>
        public static void RemoveAll()
        {
            SetAutostart(false, null);
            Registry.CurrentUser.DeleteSubKeyTree(KeyPath, false);
        }
    }
}
