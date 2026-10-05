using System.IO;
using System.Text.Json;
using System.Windows.Input;

namespace ToolBox.Shared;

/// <summary>全局热键的功能标识（同时作为 RegisterHotKey 的 id）。</summary>
internal static class HotkeyIds
{
    public const int Translator = 9001;
    public const int Resize = 9002;
    public const int TopMost = 9000;
}

/// <summary>单个全局热键配置：修饰键组合 + 主键文本（如 "1"、"F2"、"A"）。</summary>
internal sealed class HotkeyConfig
{
    public bool Ctrl { get; set; }
    public bool Shift { get; set; }
    public bool Alt { get; set; }
    public bool Win { get; set; }
    public string Key { get; set; } = string.Empty;

    public uint ToModifiers()
    {
        uint modifiers = 0;
        if (Ctrl) modifiers |= WindowServices.MOD_CONTROL;
        if (Shift) modifiers |= WindowServices.MOD_SHIFT;
        if (Alt) modifiers |= WindowServices.MOD_ALT;
        if (Win) modifiers |= WindowServices.MOD_WIN;
        return modifiers;
    }

    /// <summary>解析为可注册的热键；修饰键至少一个且主键合法才算有效。</summary>
    public bool TryResolve(out uint modifiers, out uint virtualKey, out string displayText)
    {
        modifiers = ToModifiers();
        virtualKey = 0;
        displayText = string.Empty;

        if (modifiers == 0)
        {
            return false;
        }

        var keyText = (Key ?? string.Empty).Trim().ToUpperInvariant();
        if (!TryParseVirtualKey(keyText, out virtualKey))
        {
            return false;
        }

        displayText = BuildDisplayText(modifiers, keyText);
        return true;
    }

    public string BuildDisplayText()
    {
        return TryResolve(out _, out _, out var display) ? display : "无效配置";
    }

    internal static string BuildDisplayText(uint modifiers, string keyText)
    {
        var parts = new List<string>();
        if ((modifiers & WindowServices.MOD_CONTROL) != 0)
        {
            parts.Add("Ctrl");
        }
        if ((modifiers & WindowServices.MOD_SHIFT) != 0)
        {
            parts.Add("Shift");
        }
        if ((modifiers & WindowServices.MOD_ALT) != 0)
        {
            parts.Add("Alt");
        }
        if ((modifiers & WindowServices.MOD_WIN) != 0)
        {
            parts.Add("Win");
        }

        parts.Add(keyText);
        return string.Join("+", parts);
    }

    // 移植自 MyTranslator：支持单个字母数字、F1-F24 以及 WPF Key 枚举名
    internal static bool TryParseVirtualKey(string keyText, out uint virtualKey)
    {
        virtualKey = 0;
        if (string.IsNullOrWhiteSpace(keyText))
        {
            return false;
        }

        if (keyText.Length == 1)
        {
            var c = keyText[0];
            if (char.IsLetterOrDigit(c))
            {
                virtualKey = c;
                return true;
            }
        }

        if (keyText.StartsWith("F", StringComparison.OrdinalIgnoreCase)
            && Enum.TryParse<System.Windows.Input.Key>(keyText, true, out var functionKey)
            && functionKey >= System.Windows.Input.Key.F1
            && functionKey <= System.Windows.Input.Key.F24)
        {
            virtualKey = (uint)KeyInterop.VirtualKeyFromKey(functionKey);
            return true;
        }

        if (Enum.TryParse<System.Windows.Input.Key>(keyText, true, out var key))
        {
            virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
            return virtualKey != 0;
        }

        return false;
    }
}

/// <summary>外壳持久化设置：各功能的全局热键。</summary>
internal sealed class ShellSettings
{
    public HotkeyConfig Translator { get; set; } = new() { Ctrl = true, Shift = true, Key = "1" };
    public HotkeyConfig Resize { get; set; } = new() { Ctrl = true, Shift = true, Key = "2" };
    public HotkeyConfig TopMost { get; set; } = new() { Ctrl = true, Shift = true, Key = "3" };

    public HotkeyConfig? ForId(int hotkeyId) => hotkeyId switch
    {
        HotkeyIds.Translator => Translator,
        HotkeyIds.Resize => Resize,
        HotkeyIds.TopMost => TopMost,
        _ => null
    };

    public void SetForId(int hotkeyId, HotkeyConfig config)
    {
        switch (hotkeyId)
        {
            case HotkeyIds.Translator:
                Translator = config;
                break;
            case HotkeyIds.Resize:
                Resize = config;
                break;
            case HotkeyIds.TopMost:
                TopMost = config;
                break;
        }
    }
}

/// <summary>设置持久化：%LocalAppData%\ToolBox\settings.json。</summary>
internal static class ShellSettingsStore
{
    private static readonly string SettingsFolderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ToolBox");
    private static readonly string SettingsFilePath = Path.Combine(SettingsFolderPath, "settings.json");

    public static ShellSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                return new ShellSettings();
            }

            return JsonSerializer.Deserialize<ShellSettings>(File.ReadAllText(SettingsFilePath)) ?? new ShellSettings();
        }
        catch
        {
            return new ShellSettings();
        }
    }

    public static void Save(ShellSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsFolderPath);
            File.WriteAllText(
                SettingsFilePath,
                JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // 设置保存失败不影响主流程
        }
    }
}
