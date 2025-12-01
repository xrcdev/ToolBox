using System;
using System.IO;
using System.Text.Json;

namespace AlwaysOnTop
{
    public class AppConfig
    {
        public HotkeysConfig Hotkeys { get; set; }

        public static AppConfig Load(string configPath = "config.json")
        {
            try
            {
                if (!File.Exists(configPath))
                {
                    Console.WriteLine($"⚠ 配置文件不存在: {configPath}，使用默认配置");
                    return GetDefaultConfig();
                }

                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json);
                return config ?? GetDefaultConfig();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠ 加载配置文件失败: {ex.Message}，使用默认配置");
                return GetDefaultConfig();
            }
        }

        private static AppConfig GetDefaultConfig()
        {
            return new AppConfig
            {
                Hotkeys = new HotkeysConfig
                {
                    Pin = new HotkeyConfig { Modifiers = "Shift, Alt", Key = "T" },
                    Unpin = new HotkeyConfig { Modifiers = "Control, Alt", Key = "T" }
                }
            };
        }
    }

    public class HotkeysConfig
    {
        public HotkeyConfig Pin { get; set; }
        public HotkeyConfig Unpin { get; set; }
    }

    public class HotkeyConfig
    {
        public string Modifiers { get; set; }
        public string Key { get; set; }
    }
}
