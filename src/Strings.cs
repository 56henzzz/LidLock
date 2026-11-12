using System;
using System.Collections.Generic;
using System.Globalization;

namespace LidLock
{
    /// <summary>Textos de la interfaz en español e inglés (solo se carga el idioma del sistema).</summary>
    internal static class Strings
    {
        private static readonly Dictionary<string, string> Table = Load();

        public static string Get(string key)
        {
            string value;
            return Table.TryGetValue(key, out value) ? value : key;
        }

        public static string Get(string key, params object[] args)
        {
            return string.Format(Get(key), args);
        }

        private static Dictionary<string, string> Load()
        {
            bool es = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es";
            Dictionary<string, string> t = new Dictionary<string, string>();

            t["Active"] = es ? "Activo" : "Active";
            t["Inactive"] = es ? "Inactivo" : "Inactive";
            t["Tooltip"] = "LidLock - {0}";

            t["MenuToggle"] = es ? "Bloquear al cerrar la tapa" : "Lock when the lid closes";
            t["MenuAutostart"] = es ? "Iniciar con Windows" : "Open at boot";
            t["MenuCheck"] = es ? "Buscar actualizaciones" : "Check for updates";
            t["MenuUpdate"] = es ? "Actualizar a {0}" : "Update to {0}";
            t["MenuGitHub"] = es ? "Abrir en GitHub" : "Open on GitHub";
            t["MenuUninstall"] = es ? "Desinstalar..." : "Uninstall...";
            t["MenuExit"] = es ? "Salir" : "Exit";

            t["ConsentInstall"] = es
                ? "LidLock hará estos cambios en tu PC:\n\n" +
                  "• Se copiará en %LOCALAPPDATA%\\LidLock y creará un acceso en el menú Inicio.\n" +
                  "• Cambiará \"Acción al cerrar la tapa\" a \"No hacer nada\" en todos los planes de energía (pide permiso de administrador). Antes guardará tus valores actuales.\n" +
                  "• Se iniciará con Windows y quedará en la bandeja del sistema, junto al reloj.\n\n" +
                  "Al cerrar la tapa, Windows se bloqueará y tus programas seguirán funcionando.\n" +
                  "Podrás revertirlo todo desde el menú del icono (desactivar o desinstalar).\n\n" +
                  "¿Quieres continuar?"
                : "LidLock will make these changes to your PC:\n\n" +
                  "• It will copy itself to %LOCALAPPDATA%\\LidLock and add a Start menu shortcut.\n" +
                  "• It will set \"Lid close action\" to \"Do nothing\" in all power plans (asks for administrator permission). Your current values are saved first.\n" +
                  "• It will start with Windows and live in the system tray, next to the clock.\n\n" +
                  "When you close the lid, Windows will lock and your programs keep running.\n" +
                  "You can undo everything from the tray icon menu (disable or uninstall).\n\n" +
                  "Do you want to continue?";

            t["ConsentActivate"] = es
                ? "Se cambiará \"Acción al cerrar la tapa\" a \"No hacer nada\" en todos los planes de energía (pide permiso de administrador). Antes se guardarán tus valores actuales.\n\nAl cerrar la tapa, Windows se bloqueará y tus programas seguirán funcionando.\n\n¿Quieres continuar?"
                : "\"Lid close action\" will be set to \"Do nothing\" in all power plans (asks for administrator permission). Your current values are saved first.\n\nWhen you close the lid, Windows will lock and your programs keep running.\n\nDo you want to continue?";

            t["ConfirmUninstall"] = es
                ? "Se restaurarán tus ajustes anteriores de la tapa y se eliminará LidLock: archivos, inicio con Windows, acceso del menú Inicio y claves de registro.\n\n¿Quieres desinstalarlo?"
                : "Your previous lid settings will be restored and LidLock will be removed: files, start with Windows, Start menu shortcut and registry keys.\n\nDo you want to uninstall it?";

            t["ExitAsk"] = es
                ? "Si sales, LidLock dejará de bloquear al cerrar la tapa, pero la tapa seguirá en \"No hacer nada\" (el equipo no se bloqueará ni se suspenderá).\n\nSí = restaurar mis ajustes anteriores y salir\nNo = salir sin restaurar\nCancelar = volver"
                : "If you exit, LidLock stops locking when the lid closes, but the lid action stays on \"Do nothing\" (the PC will neither lock nor sleep).\n\nYes = restore my previous settings and exit\nNo = exit without restoring\nCancel = go back";

            t["ErrNoLid"] = es
                ? "Este equipo no tiene ajuste de cierre de tapa (¿es un PC de escritorio?). No se hizo ningún cambio."
                : "This PC has no lid close setting (is it a desktop PC?). Nothing was changed.";
            t["ErrAdmin"] = es
                ? "No se pudieron aplicar los cambios. Comprueba que aceptaste el aviso de permiso de administrador."
                : "The changes could not be applied. Make sure you accepted the administrator permission prompt.";
            t["AlreadyRunning"] = es
                ? "LidLock ya está en ejecución. Busca su icono en la bandeja del sistema (junto al reloj)."
                : "LidLock is already running. Look for its icon in the system tray (next to the clock).";
            t["Welcome"] = es
                ? "Listo: al cerrar la tapa se bloqueará Windows. Gestiona LidLock desde este icono."
                : "All set: Windows will lock when you close the lid. Manage LidLock from this icon.";

            t["UpToDate"] = es ? "Ya tienes la última versión ({0})." : "You already have the latest version ({0}).";
            t["UpdateFailed"] = es
                ? "No se pudo comprobar o descargar la actualización. Inténtalo más tarde."
                : "Could not check for or download the update. Try again later.";
            t["UpdateNoRepo"] = es
                ? "Esta compilación no tiene un repositorio de GitHub configurado."
                : "This build has no GitHub repository configured.";
            t["UpdateBalloon"] = es
                ? "Hay una nueva versión: {0}. Actualiza desde el menú del icono."
                : "A new version is available: {0}. Update from the tray icon menu.";

            return t;
        }
    }
}
