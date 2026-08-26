using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace Droute.Installer.Classes
{
    internal class Config
    {
        public enum LogLevelValue
        {
            Trace = 0,
            Debug = 1,
            Info  = 2,
            Warn  = 3,
            Error = 4,
            Off   = 5
        };

        private const string REGISTRY_PATH = @"Software\droute";
        private const string INI_SECTION = "droute";

        private readonly string _iniPath;

        #region [ Values ]

        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 1080;

        public string User { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        public int ConnectTimeout { get; set; } = 5000;
        public int ReconnectInterval { get; set; } = 3000;
        public LogLevelValue LogLevel { get; set; } = LogLevelValue.Info;

        #endregion

        public bool UsesIni => !string.IsNullOrEmpty(_iniPath);
        public string IniPath => _iniPath;

        public Config(bool autoUpdate = true)
            : this(null, autoUpdate)
        {
        }

        public Config(string iniPath, bool autoUpdate = true)
        {
            _iniPath = string.IsNullOrWhiteSpace(iniPath) ? null : iniPath;

            if (!UsesIni)
            {
                using (RegistryKey rk = Registry.CurrentUser.CreateSubKey(REGISTRY_PATH)) { }
            }

            if (autoUpdate)
                Update();

            Normalize();
        }

        public void Update()
        {
            try
            {
                if (UsesIni)
                    UpdateFromIni();
                else
                    UpdateFromRegistry();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"error during update configuration: {ex.ToString()}");
            }

            Normalize();
        }

        public void Apply()
        {
            try
            {
                Normalize();

                if (UsesIni)
                    ApplyToIni();
                else
                    ApplyToRegistry();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"error during apply configuration: {ex.ToString()}");
            }
        }

        public void Reset()
        {
            try
            {
                if (UsesIni)
                {
                    if (File.Exists(_iniPath))
                        File.Delete(_iniPath);
                }
                else
                {
                    Registry.CurrentUser.DeleteSubKeyTree(REGISTRY_PATH, throwOnMissingSubKey: false);
                }

                Host = "127.0.0.1";
                Port = 1080;
                User = string.Empty;
                Password = string.Empty;
                ConnectTimeout = 5000;
                ReconnectInterval = 3000;
                LogLevel = LogLevelValue.Info;

                Normalize();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"error during reset configuration: {ex.ToString()}");
            }
        }

        private void UpdateFromRegistry()
        {
            using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(REGISTRY_PATH))
            {
                if (rk == null) return;

                foreach (var prop in GetConfigProperties())
                {
                    object regValue = rk.GetValue(prop.Name);
                    if (regValue == null) continue;

                    TrySetProperty(prop, regValue);
                }
            }
        }

        private void ApplyToRegistry()
        {
            using (RegistryKey rk = Registry.CurrentUser.CreateSubKey(REGISTRY_PATH))
            {
                foreach (var prop in GetConfigProperties())
                {
                    object value = prop.GetValue(this);

                    if (prop.PropertyType == typeof(int) || prop.PropertyType.IsEnum)
                    {
                        rk.SetValue(prop.Name, Convert.ToInt32(value), RegistryValueKind.DWord);
                    }
                    else
                    {
                        rk.SetValue(prop.Name, value?.ToString() ?? string.Empty, RegistryValueKind.String);
                    }
                }
            }
        }

        private void UpdateFromIni()
        {
            if (!File.Exists(_iniPath))
                return;

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string currentSection = string.Empty;

            foreach (string rawLine in File.ReadAllLines(_iniPath))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#"))
                    continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = line.Substring(1, line.Length - 2).Trim();
                    continue;
                }

                if (!currentSection.Equals(INI_SECTION, StringComparison.OrdinalIgnoreCase))
                    continue;

                int separator = line.IndexOf('=');
                if (separator <= 0)
                    continue;

                string key = line.Substring(0, separator).Trim();
                string value = line.Substring(separator + 1);
                if (key.Length == 0)
                    continue;

                values[key] = value;
            }

            foreach (var prop in GetConfigProperties())
            {
                string rawValue;
                if (!values.TryGetValue(prop.Name, out rawValue))
                    continue;

                TrySetProperty(prop, rawValue);
            }
        }

        private void ApplyToIni()
        {
            string directory = Path.GetDirectoryName(_iniPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var builder = new StringBuilder();
            builder.Append('[').Append(INI_SECTION).AppendLine("]");

            foreach (var prop in GetConfigProperties())
            {
                object value = prop.GetValue(this);

                if (prop.PropertyType == typeof(int) || prop.PropertyType.IsEnum)
                    builder.Append(prop.Name).Append('=').Append(Convert.ToInt32(value)).AppendLine();
                else
                    builder.Append(prop.Name).Append('=').Append(value?.ToString() ?? string.Empty).AppendLine();
            }

            File.WriteAllText(_iniPath, builder.ToString(), new UTF8Encoding(false));
        }

        private IEnumerable<PropertyInfo> GetConfigProperties()
        {
            foreach (var prop in GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.CanRead && prop.CanWrite)
                    yield return prop;
            }
        }

        private void TrySetProperty(PropertyInfo prop, object rawValue)
        {
            try
            {
                if (prop.PropertyType == typeof(int))
                {
                    prop.SetValue(this, Convert.ToInt32(rawValue));
                }
                else if (prop.PropertyType.IsEnum)
                {
                    object enumValue = rawValue is string
                        ? Enum.Parse(prop.PropertyType, rawValue.ToString(), true)
                        : Enum.ToObject(prop.PropertyType, rawValue);
                    if (Enum.IsDefined(prop.PropertyType, enumValue))
                        prop.SetValue(this, enumValue);
                }
                else
                {
                    prop.SetValue(this, rawValue?.ToString() ?? string.Empty);
                }
            }
            catch { }
        }

        private void Normalize()
        {
            Host = string.IsNullOrWhiteSpace(Host) ? "127.0.0.1" : Host.Trim();

            if (Port < 1 || Port > 65535)
                Port = 1080;

            if (ConnectTimeout < 100)
                ConnectTimeout = 5000;

            if (ReconnectInterval < 100)
                ReconnectInterval = 3000;
        }
    }
}
