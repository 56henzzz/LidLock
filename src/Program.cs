using System;
using System.Threading;

namespace LidLock
{
    internal static class Program
    {
        // Argumentos:
        //   --apply / --restore "<instantanea>"  uso interno: pasos con permisos de administrador
        //   --welcome                            tras instalar: muestra el aviso de bienvenida
        //   --no-install                         desarrollo: abre la bandeja sin instalar
        private static int Main(string[] args)
        {
            string command = args.Length > 0 ? args[0] : "";
            if (command == "--apply") return PowerSettings.Apply() ? 0 : 1;
            if (command == "--restore") return PowerSettings.Restore(args.Length > 1 ? args[1] : "") ? 0 : 1;

            bool noInstall = Array.IndexOf(args, "--no-install") >= 0;
            if (!noInstall && !Installer.IsInstalled) return Installer.FirstRun();

            bool created;
            using (Mutex mutex = new Mutex(true, Installer.MutexName, out created))
            {
                if (!created)
                {
                    TrayApp.NotifyRunningInstance();
                    return 0;
                }
                return new TrayApp(Array.IndexOf(args, "--welcome") >= 0).Run();
            }
        }
    }
}
