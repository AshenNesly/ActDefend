using System.Diagnostics;
using System.Security.Principal;

namespace ActDefend.Core.Elevation;

/// <summary>
/// Utility for checking and requesting Windows Administrator elevation.
///
/// Elevation behaviour (from brief §6):
/// 1. On startup, check if the process is elevated.
/// 2. If not elevated, relaunch with UAC prompt and exit the current instance.
/// 3. If elevation is denied, the caller must NOT claim monitoring is active.
///
/// Note: The app.manifest already contains requestedExecutionLevel=requireAdministrator,
/// so Windows typically handles UAC before the app launches. This helper provides
/// a runtime verification layer and handles edge cases (e.g., launched from another
/// non-elevated process without ShellExecute).
/// </summary>
public static class ElevationHelper
{
    /// <summary>
    /// Returns true when the current process token has Administrator group membership
    /// AND the group is enabled (elevated).
    /// </summary>
    public static bool IsElevated()
    {
        using var identity  = WindowsIdentity.GetCurrent();
        var       principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// Attempts to relaunch the current executable with UAC elevation (ShellExecute "runas").
    /// Returns true if the relaunch process was started successfully.
    /// Returns false if the user cancelled the UAC prompt or if an error occurred.
    ///
    /// The caller should exit the current process after this returns true.
    /// </summary>
    /// <param name="args">Original command-line arguments to pass through.</param>
    public static bool TryRelaunchElevated(string[] args)
    {
        try
        {
            var exePath    = Environment.ProcessPath
                            ?? Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath))
                return false;

            var startInfo = new ProcessStartInfo
            {
                FileName        = exePath,
                Arguments       = args.Length > 0
                                    ? string.Join(' ', args.Select(a => $"\"{a}\""))
                                    : string.Empty,
                Verb            = "runas",           // triggers UAC prompt
                UseShellExecute = true,              // required for "runas" verb
                WorkingDirectory = Environment.CurrentDirectory
            };

            Process.Start(startInfo);
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // User cancelled UAC prompt (error code 1223) or access denied.
            return false;
        }
        catch
        {
            return false;
        }
    }
}
