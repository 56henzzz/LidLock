using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace LidLock
{
    /// <summary>
    /// Ajuste "Accion al cerrar la tapa" de Windows, manejado con powercfg.exe.
    /// Una "instantanea" (snapshot) es el texto "H=1;guid=ac,dc;guid=ac,dc" con los valores originales.
    /// </summary>
    internal static class PowerSettings
    {
        private const string SubButtons = "4f971e89-eebd-4455-a8de-9e59040e7347";
        private const string LidAction = "5ca83367-6e45-459f-a27b-476b1d01c936";

        public const int DoNothing = 0;
        public const int Sleep = 1; // valor por defecto de Windows

        private static string Powercfg(string arguments, out int exitCode)
        {
            ProcessStartInfo info = new ProcessStartInfo("powercfg.exe", arguments);
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;

            using (Process process = Process.Start(info))
            {
                string output = process.StandardOutput.ReadToEnd();
                process.StandardError.ReadToEnd();
                process.WaitForExit();
                exitCode = process.ExitCode;
                return output;
            }
        }

        private static string Powercfg(string arguments)
        {
            int code;
            return Powercfg(arguments, out code);
        }

        private static bool Run(string arguments)
        {
            int code;
            Powercfg(arguments, out code);
            return code == 0;
        }

        private static bool IsGuidAt(string text, int start)
        {
            for (int k = 0; k < 36; k++)
            {
                char c = text[start + k];
                if (k == 8 || k == 13 || k == 18 || k == 23)
                {
                    if (c != '-') return false;
                }
                else if (!Uri.IsHexDigit(c))
                {
                    return false;
                }
            }
            return true;
        }

        private static List<string> ExtractGuids(string text)
        {
            List<string> guids = new List<string>();
            for (int i = 0; i + 36 <= text.Length; i++)
            {
                if (IsGuidAt(text, i))
                {
                    guids.Add(text.Substring(i, 36).ToLowerInvariant());
                    i += 35;
                }
            }
            return guids;
        }

        /// <summary>Los dos ultimos valores 0xNNNNNNNN del texto de powercfg: corriente alterna y bateria.</summary>
        private static int[] LastTwoValues(string text)
        {
            List<int> values = new List<int>();
            int i = 0;
            while ((i = text.IndexOf("0x", i, StringComparison.Ordinal)) >= 0)
            {
                int value;
                if (i + 10 <= text.Length &&
                    int.TryParse(text.Substring(i + 2, 8), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                {
                    values.Add(value);
                }
                i += 2;
            }
            if (values.Count < 2) return null;
            return new int[] { values[values.Count - 2], values[values.Count - 1] };
        }

        public static string ActiveScheme()
        {
            List<string> guids = ExtractGuids(Powercfg("/getactivescheme"));
            return guids.Count > 0 ? guids[0] : null;
        }

        public static List<string> Schemes()
        {
            return ExtractGuids(Powercfg("/list"));
        }

        /// <summary>Valores {corriente, bateria} del plan, o null si el equipo no tiene el ajuste. /qh incluye los ajustes ocultos.</summary>
        public static int[] GetLid(string scheme)
        {
            return LastTwoValues(Powercfg("/qh " + scheme + " " + SubButtons + " " + LidAction));
        }

        public static bool HasLidSetting()
        {
            string active = ActiveScheme();
            return active != null && GetLid(active) != null;
        }

        /// <summary>El ajuste existe pero Windows lo oculta en Opciones de energia (pasa en muchos portatiles).</summary>
        private static bool IsLidHidden()
        {
            string active = ActiveScheme();
            return active != null && LastTwoValues(Powercfg("/query " + active + " " + SubButtons + " " + LidAction)) == null;
        }

        public static bool IsApplied()
        {
            string active = ActiveScheme();
            int[] lid = active == null ? null : GetLid(active);
            return lid != null && lid[0] == DoNothing && lid[1] == DoNothing;
        }

        public static string Snapshot()
        {
            StringBuilder text = new StringBuilder();
            text.Append("H=").Append(IsLidHidden() ? 1 : 0);
            foreach (string scheme in Schemes())
            {
                int[] lid = GetLid(scheme);
                if (lid != null) text.Append(';').Append(scheme).Append('=').Append(lid[0]).Append(',').Append(lid[1]);
            }
            return text.ToString();
        }

        private static bool SetLid(string scheme, int ac, int dc)
        {
            return Run("/setacvalueindex " + scheme + " " + SubButtons + " " + LidAction + " " + ac)
                && Run("/setdcvalueindex " + scheme + " " + SubButtons + " " + LidAction + " " + dc);
        }

        // Los dos metodos siguientes necesitan administrador: se ejecutan en un proceso elevado.

        /// <summary>Muestra el ajuste y pone "No hacer nada" en todos los planes.</summary>
        public static bool Apply()
        {
            string active = ActiveScheme();
            if (active == null) return false;

            Run("-attributes " + SubButtons + " " + LidAction + " -ATTRIB_HIDE"); // solo cosmetico: no condiciona el resultado
            bool ok = true;
            foreach (string scheme in Schemes()) ok &= SetLid(scheme, DoNothing, DoNothing);
            return Run("/setactive " + active) && ok;
        }

        /// <summary>Devuelve los valores guardados; sin instantanea usa el valor por defecto de Windows (Suspender).</summary>
        public static bool Restore(string snapshot)
        {
            string active = ActiveScheme();
            if (active == null) return false;

            Dictionary<string, int[]> saved = new Dictionary<string, int[]>();
            bool hidden = false;
            foreach (string token in (snapshot ?? "").Split(';'))
            {
                string[] pair = token.Split('=');
                if (pair.Length != 2) continue;
                if (pair[0] == "H")
                {
                    hidden = pair[1] == "1";
                    continue;
                }
                string[] values = pair[1].Split(',');
                int ac, dc;
                if (values.Length == 2 && int.TryParse(values[0], out ac) && int.TryParse(values[1], out dc))
                    saved[pair[0].ToLowerInvariant()] = new int[] { ac, dc };
            }

            bool ok = true;
            foreach (string scheme in Schemes())
            {
                int[] lid;
                if (!saved.TryGetValue(scheme, out lid)) lid = new int[] { Sleep, Sleep };
                ok &= SetLid(scheme, lid[0], lid[1]);
            }
            if (hidden) Run("-attributes " + SubButtons + " " + LidAction + " +ATTRIB_HIDE");
            return Run("/setactive " + active) && ok;
        }
    }
}
