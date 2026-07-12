using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Reflection;

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
            Error = 4
        };

        private const string REGISTRY_PATH = @"Software\droute";

        #region [ Values ]

        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 1080;

        public string User { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        public int ConnectTimeout { get; set; } = 5000;
        public int ReconnectInterval { get; set; } = 3000;
        public LogLevelValue LogLevel { get; set; } = LogLevelValue.Info;

        #endregion

        public Config(bool autoUpdate = true) 
        {
            using (RegistryKey rk = Registry.CurrentUser.CreateSubKey(REGISTRY_PATH)) { }

            if (autoUpdate)
                Update();

            Normalize();
        }

        public void Update()
        {
            try
            {
                using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(REGISTRY_PATH))
                {
                    if (rk == null) return;

                    foreach (var prop in this.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        object regValue = rk.GetValue(prop.Name);
                        if (regValue == null) continue;

                        try
                        {
                            if (prop.PropertyType == typeof(int))
                            {
                                prop.SetValue(this, Convert.ToInt32(regValue));
                            }
                            else if (prop.PropertyType.IsEnum)
                            {
                                object enumValue = Enum.ToObject(prop.PropertyType, regValue);
                                if (Enum.IsDefined(prop.PropertyType, enumValue))
                                    prop.SetValue(this, enumValue);
                            }
                            else
                            {
                                prop.SetValue(this, regValue.ToString());
                            }
                        }
                        catch { }
                    }
                }
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

                using (RegistryKey rk = Registry.CurrentUser.CreateSubKey(REGISTRY_PATH))
                {
                    foreach (var prop in this.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        object value = prop.GetValue(this);

                        if (prop.PropertyType == typeof(int) || prop.PropertyType.IsEnum)
                        {
                            rk.SetValue(prop.Name, (int)value, RegistryValueKind.DWord);
                        }
                        else
                        {
                            rk.SetValue(prop.Name, value?.ToString() ?? string.Empty, RegistryValueKind.String);
                        }
                    }
                }
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
                Registry.CurrentUser.DeleteSubKeyTree(REGISTRY_PATH, throwOnMissingSubKey: false);

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
