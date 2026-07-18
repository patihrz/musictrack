using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using SpotifyNowPlayingOverlay.Models;

namespace SpotifyNowPlayingOverlay.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly string _settingsFilePath;
        private readonly string _settingsDirectory;
        private AppSettings _currentSettings;

        public AppSettings CurrentSettings => _currentSettings;

        public event EventHandler? SettingsChanged;

        public SettingsService()
        {
            _settingsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MusicTrack"
            );
            _settingsFilePath = Path.Combine(_settingsDirectory, "settings.json");
            _currentSettings = new AppSettings();
            LoadSettings();
        }

        public void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        _currentSettings = settings;
                        return;
                    }
                }
            }
            catch (Exception)
            {
                // Fallback to default settings
            }

            _currentSettings = new AppSettings();
        }

        public void SaveSettings()
        {
            try
            {
                if (!Directory.Exists(_settingsDirectory))
                {
                    Directory.CreateDirectory(_settingsDirectory);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_currentSettings, options);
                File.WriteAllText(_settingsFilePath, json);

                ApplyStartupSetting(_currentSettings.StartWithWindows);

                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception)
            {
                // Ignored
            }
        }

        public string GetRefreshToken()
        {
            if (string.IsNullOrEmpty(_currentSettings.EncryptedRefreshToken))
                return string.Empty;

            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(_currentSettings.EncryptedRefreshToken);
                byte[] decryptedBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch
            {
                return string.Empty;
            }
        }

        public void SetRefreshToken(string refreshToken)
        {
            if (string.IsNullOrEmpty(refreshToken))
            {
                _currentSettings.EncryptedRefreshToken = string.Empty;
            }
            else
            {
                try
                {
                    byte[] rawBytes = Encoding.UTF8.GetBytes(refreshToken);
                    byte[] encryptedBytes = ProtectedData.Protect(rawBytes, null, DataProtectionScope.CurrentUser);
                    _currentSettings.EncryptedRefreshToken = Convert.ToBase64String(encryptedBytes);
                }
                catch
                {
                    // Fallback to unencrypted in case of severe OS environment failure, but we want security
                    _currentSettings.EncryptedRefreshToken = string.Empty;
                }
            }
            SaveSettings();
        }

        private void ApplyStartupSetting(bool startWithWindows)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (key != null)
                {
                    string appName = "MusicTrack";
                    if (startWithWindows)
                    {
                        string exePath = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "MusicTrack.exe");
                        key.SetValue(appName, $"\"{exePath}\"");
                    }
                    else
                    {
                        key.DeleteValue(appName, false);
                    }
                }
            }
            catch
            {
                // Ignored (permission restrictions or Registry not supported)
            }
        }
    }
}
