using System;
using System.IO;
using System.Security;
using Microsoft.Win32;

namespace Verity.Shared
{
    public sealed class SettingsData
    {
        public string ApiKey { get; set; } = "";
        public string Model { get; set; } = "";
        public bool ConsentAccepted { get; set; }

        // Pet position, -1 meaning "not placed yet".
        public int PetX { get; set; } = -1;
        public int PetY { get; set; } = -1;
    }

    /// <summary>
    /// Reads and writes settings under HKCU\Software\Verity.
    ///
    /// The API key is a plaintext REG_SZ value. HKCU is per-user by definition,
    /// so the key is only readable by the account running Verity, and it never
    /// travels in a file that might get synced or committed. That is the whole
    /// reason for preferring the registry over an ini here.
    /// </summary>
    public static class Settings
    {
        private const string KeyPath = @"Software\Verity";

        public static string RegistryPath => @"HKEY_CURRENT_USER\" + KeyPath;

        private static RegistryKey? Open(bool writable)
        {
            try
            {
                return Registry.CurrentUser.CreateSubKey(KeyPath, writable);
            }
            catch (SecurityException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            catch (IOException) { return null; }
        }

        public static SettingsData Load()
        {
            var data = new SettingsData();
            using var key = Open(writable: false);
            if (key == null) return data;

            data.ApiKey = key.GetValue("ApiKey", "") as string ?? "";
            data.Model = key.GetValue("Model", "") as string ?? "";
            data.ConsentAccepted = (key.GetValue("ConsentAccepted", 0) as int? ?? 0) != 0;
            data.PetX = unchecked((int)(key.GetValue("PetX", -1) as int? ?? -1));
            data.PetY = unchecked((int)(key.GetValue("PetY", -1) as int? ?? -1));
            return data;
        }

        public static void Save(SettingsData data)
        {
            using var key = Open(writable: true);
            if (key == null) return;

            key.SetValue("ApiKey", data.ApiKey ?? "", RegistryValueKind.String);
            key.SetValue("Model", data.Model ?? "", RegistryValueKind.String);
            key.SetValue("ConsentAccepted", data.ConsentAccepted ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue("PetX", data.PetX, RegistryValueKind.DWord);
            key.SetValue("PetY", data.PetY, RegistryValueKind.DWord);
        }

        /// <summary>Removes every value Verity stored. Used by the pet's "reset" menu item.</summary>
        public static void Clear()
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(KeyPath, throwOnMissingSubKey: false); }
            catch (SecurityException) { }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }
    }
}