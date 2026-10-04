using System.IO;
using System.Text.Json;
using System.Windows;

namespace RemoveFileReadonlyAttribute.WPF
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑：批量移除文件（及可选的文件夹）只读属性，
    /// 支持选择目标文件夹、忽略指定文件夹/扩展名，界面操作移植自控制台版 Program.cs。
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int MaxLogLines = 5000;

        private static readonly string SettingsFolderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RemoveFileReadonlyAttribute.WPF");
        private static readonly string SettingsFilePath = Path.Combine(SettingsFolderPath, "settings.json");

        private sealed class AppSettings
        {
            public string TargetFolder { get; set; } = string.Empty;
            public List<string> IgnoredFolders { get; set; } = new() { ".git", "node_modules", "bin", "obj" };
            public List<string> IgnoredExtensions { get; set; } = new();
            public bool IncludeSubdirectories { get; set; } = true;
            public bool ProcessDirectories { get; set; }
            public bool OnlyLogChanges { get; set; } = true;
        }

        private sealed class ProcessingOptions
        {
            public HashSet<string> IgnoredFolders { get; init; } = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> IgnoredExtensions { get; init; } = new(StringComparer.OrdinalIgnoreCase);
            public bool IncludeSubdirectories { get; init; }
            public bool ProcessDirectories { get; init; }
            public bool OnlyLogChanges { get; init; }
        }

        private sealed class ProcessingStats
        {
            public int ScannedFiles;
            public int ModifiedFiles;
            public int SkippedFiles;
            public int FailedFiles;
            public int ModifiedDirectories;
        }

        private CancellationTokenSource? _cancellationTokenSource;
        private bool _logTruncated;

        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var folder = FolderPicker.PickFolder(this, FolderPathTextBox.Text.Trim());
            if (folder is not null)
            {
                FolderPathTextBox.Text = folder;
            }
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            var rootPath = FolderPathTextBox.Text.Trim();
            if (rootPath.Length == 0 || !Directory.Exists(rootPath))
            {
                MessageBox.Show(this, "请先选择一个有效的文件夹。", "提示");
                return;
            }

            var options = CollectOptions();
            SaveSettings();

            LogListBox.Items.Clear();
            _logTruncated = false;
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;
            SetBusy(true);
            StatsTextBlock.Text = "正在处理...";
            AppendLog($"开始处理：{rootPath}");

            try
            {
                var stats = await Task.Run(() => RemoveReadOnlyAttributes(rootPath, options, token));
                AppendLog("处理完成。");
                StatsTextBlock.Text = FormatStats(stats, "处理完成");
            }
            catch (OperationCanceledException)
            {
                AppendLog("已停止处理。");
                StatsTextBlock.Text = "已停止。";
            }
            catch (Exception ex)
            {
                AppendLog($"发生错误：{ex.Message}");
                StatsTextBlock.Text = "处理失败。";
                MessageBox.Show(this, ex.Message, "处理失败");
            }
            finally
            {
                SetBusy(false);
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            StopButton.IsEnabled = false;
            StatsTextBlock.Text = "正在停止...";
        }

        private void SetBusy(bool busy)
        {
            StartButton.IsEnabled = !busy;
            StopButton.IsEnabled = busy;
            BrowseButton.IsEnabled = !busy;
            FolderPathTextBox.IsEnabled = !busy;
            // 处理期间锁定选项区，防止运行中改动造成误解（选项在开始时已快照）
            OptionsGroupBox.IsEnabled = !busy;
        }

        private ProcessingOptions CollectOptions()
        {
            return new ProcessingOptions
            {
                IgnoredFolders = ParseEntries(IgnoredFoldersTextBox.Text),
                IgnoredExtensions = ParseEntries(IgnoredExtensionsTextBox.Text)
                    .Select(NormalizeExtension)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase),
                IncludeSubdirectories = IncludeSubdirectoriesCheckBox.IsChecked == true,
                ProcessDirectories = ProcessDirectoriesCheckBox.IsChecked == true,
                OnlyLogChanges = OnlyLogChangesCheckBox.IsChecked == true
            };
        }

        private static HashSet<string> ParseEntries(string text)
        {
            var entries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in text.Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var entry = raw.Trim();
                if (entry.Length > 0)
                {
                    entries.Add(entry);
                }
            }
            return entries;
        }

        private static string NormalizeExtension(string entry)
        {
            return entry.StartsWith('.') ? entry : "." + entry;
        }

        private ProcessingStats RemoveReadOnlyAttributes(string rootPath, ProcessingOptions options, CancellationToken token)
        {
            var stats = new ProcessingStats();
            var lastUiUpdate = Environment.TickCount64;

            var pending = new Stack<string>();
            pending.Push(rootPath);

            while (pending.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                var directory = pending.Pop();

                string[] files;
                try
                {
                    files = Directory.GetFiles(directory);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    stats.FailedFiles++;
                    AppendLog($"无法访问文件夹：{directory}（{ex.Message}）");
                    continue;
                }

                foreach (var file in files)
                {
                    token.ThrowIfCancellationRequested();
                    stats.ScannedFiles++;

                    if (options.IgnoredExtensions.Contains(Path.GetExtension(file)))
                    {
                        stats.SkippedFiles++;
                        if (!options.OnlyLogChanges)
                        {
                            AppendLog($"跳过（扩展名忽略）：{file}");
                        }
                        continue;
                    }

                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.IsReadOnly)
                        {
                            fileInfo.IsReadOnly = false;
                            stats.ModifiedFiles++;
                            AppendLog($"已移除只读属性：{file}");
                        }
                        else if (!options.OnlyLogChanges)
                        {
                            AppendLog($"无只读属性，跳过：{file}");
                        }
                    }
                    catch (Exception ex)
                    {
                        stats.FailedFiles++;
                        AppendLog($"处理失败：{file}（{ex.Message}）");
                    }

                    UpdateStatsUi(stats, ref lastUiUpdate);
                }

                if (options.ProcessDirectories)
                {
                    RemoveDirectoryReadOnly(directory, stats);
                }

                if (!options.IncludeSubdirectories)
                {
                    break;
                }

                string[] subdirectories;
                try
                {
                    subdirectories = Directory.GetDirectories(directory);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    AppendLog($"无法枚举子文件夹：{directory}（{ex.Message}）");
                    continue;
                }

                foreach (var subdirectory in subdirectories)
                {
                    if (IsIgnoredFolder(rootPath, subdirectory, options.IgnoredFolders))
                    {
                        if (!options.OnlyLogChanges)
                        {
                            AppendLog($"跳过（文件夹忽略）：{subdirectory}");
                        }
                        continue;
                    }

                    try
                    {
                        // 跳过符号链接/目录联接，避免循环遍历
                        if (File.GetAttributes(subdirectory).HasFlag(FileAttributes.ReparsePoint))
                        {
                            continue;
                        }
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                    {
                        AppendLog($"无法读取属性，跳过：{subdirectory}（{ex.Message}）");
                        continue;
                    }

                    pending.Push(subdirectory);
                }

                UpdateStatsUi(stats, ref lastUiUpdate);
            }

            return stats;
        }

        private void RemoveDirectoryReadOnly(string directory, ProcessingStats stats)
        {
            try
            {
                var info = new DirectoryInfo(directory);
                if (info.Exists && info.Attributes.HasFlag(FileAttributes.ReadOnly))
                {
                    info.Attributes &= ~FileAttributes.ReadOnly;
                    stats.ModifiedDirectories++;
                    AppendLog($"已移除文件夹只读属性：{directory}");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"处理文件夹失败：{directory}（{ex.Message}）");
            }
        }

        private static bool IsIgnoredFolder(string rootPath, string folderPath, HashSet<string> ignoredFolders)
        {
            if (ignoredFolders.Count == 0)
            {
                return false;
            }

            if (ignoredFolders.Contains(Path.GetFileName(folderPath)))
            {
                return true;
            }

            var relative = Path.GetRelativePath(rootPath, folderPath);
            return ignoredFolders.Contains(relative);
        }

        private static string FormatStats(ProcessingStats stats, string prefix)
        {
            return $"{prefix}：扫描文件 {stats.ScannedFiles} 个，移除文件只读属性 {stats.ModifiedFiles} 个，" +
                   $"移除文件夹只读属性 {stats.ModifiedDirectories} 个，跳过 {stats.SkippedFiles} 个，失败 {stats.FailedFiles} 个。";
        }

        private void UpdateStatsUi(ProcessingStats stats, ref long lastUiUpdate)
        {
            var now = Environment.TickCount64;
            if (now - lastUiUpdate < 200)
            {
                return;
            }
            lastUiUpdate = now;
            Dispatcher.BeginInvoke(() => StatsTextBlock.Text = FormatStats(stats, "正在处理"));
        }

        private void AppendLog(string message)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (LogListBox.Items.Count >= MaxLogLines)
                {
                    if (!_logTruncated)
                    {
                        _logTruncated = true;
                        LogListBox.Items.Add($"[日志条数已达 {MaxLogLines} 条，后续日志不再显示]");
                    }
                    return;
                }

                LogListBox.Items.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
                LogListBox.ScrollIntoView(LogListBox.Items[^1]);
            });
        }

        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    ApplySettings(new AppSettings());
                    return;
                }

                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                ApplySettings(settings);
            }
            catch
            {
                ApplySettings(new AppSettings());
            }
        }

        private void ApplySettings(AppSettings settings)
        {
            FolderPathTextBox.Text = settings.TargetFolder;
            IgnoredFoldersTextBox.Text = string.Join(Environment.NewLine, settings.IgnoredFolders);
            IgnoredExtensionsTextBox.Text = string.Join(Environment.NewLine, settings.IgnoredExtensions);
            IncludeSubdirectoriesCheckBox.IsChecked = settings.IncludeSubdirectories;
            ProcessDirectoriesCheckBox.IsChecked = settings.ProcessDirectories;
            OnlyLogChangesCheckBox.IsChecked = settings.OnlyLogChanges;
        }

        private void SaveSettings()
        {
            try
            {
                var settings = new AppSettings
                {
                    TargetFolder = FolderPathTextBox.Text.Trim(),
                    IgnoredFolders = ParseEntries(IgnoredFoldersTextBox.Text).ToList(),
                    IgnoredExtensions = ParseEntries(IgnoredExtensionsTextBox.Text).ToList(),
                    IncludeSubdirectories = IncludeSubdirectoriesCheckBox.IsChecked == true,
                    ProcessDirectories = ProcessDirectoriesCheckBox.IsChecked == true,
                    OnlyLogChanges = OnlyLogChangesCheckBox.IsChecked == true
                };

                Directory.CreateDirectory(SettingsFolderPath);
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch
            {
                // 设置保存失败不影响主流程
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            SaveSettings();
            base.OnClosing(e);
        }
    }
}
