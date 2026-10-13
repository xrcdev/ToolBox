using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ToolBox.Shared;
using MessageBox = System.Windows.MessageBox;
using UserControl = System.Windows.Controls.UserControl;

namespace ToolBox.Tools.DirSizeStat
{
    /// <summary>
    /// 目录大小统计页，移植自 DirSizeStat 控制台工具（.NET FW 4.5.2）：
    /// 递归统计每个子目录的文件数、总大小、最大/最小文件与最新/最旧修改时间；
    /// 控制台版"最小文件"恒为 0（初值 0 恒大于文件长度），此处以 long.MaxValue 起始修正。
    /// </summary>
    public partial class DirSizeStatPage : UserControl, IToolPage
    {
        private CancellationTokenSource? _cts;

        public DirSizeStatPage()
        {
            InitializeComponent();
        }

        public string Title => "目录大小统计";

        public void OnHostClosing()
        {
            _cts?.Cancel();
        }

        private void BtnBrowseSource_Click(object sender, RoutedEventArgs e)
        {
            var folder = FolderPicker.PickFolder(Window.GetWindow(this), TxtSourceDir.Text);
            if (folder is not null)
            {
                TxtSourceDir.Text = folder;
            }
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (_cts is not null)
            {
                // 统计进行中再次点击 = 取消
                _cts.Cancel();
                return;
            }

            var rootDir = TxtSourceDir.Text;
            if (!Directory.Exists(rootDir))
            {
                MessageBox.Show(Window.GetWindow(this)!, "请选择有效的目录。", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var extensions = ParseExtensions(TxtExtensionFilter.Text);

            DirsGrid.ItemsSource = null;
            _cts = new CancellationTokenSource();
            BtnStart.Content = "取消统计";
            TxtSourceDir.IsEnabled = false;
            TxtExtensionFilter.IsEnabled = false;
            TxtSummary.Text = "正在统计...";
            PbProgress.IsIndeterminate = true;

            var token = _cts.Token;
            var progress = new Progress<string>(message => TxtSummary.Text = message);

            try
            {
                var rows = await Task.Run(() => Walk(new DirectoryInfo(rootDir), 0, extensions, token, progress));
                DirsGrid.ItemsSource = rows;

                var totalFiles = rows.Sum(r => r.FileCount);
                var totalSize = rows.Sum(r => r.TotalSize);
                TxtSummary.Text = token.IsCancellationRequested
                    ? "统计已取消（已显示部分结果）。"
                    : $"共 {rows.Count} 个目录，{totalFiles} 个文件，总大小 {DirStatRow.FormatSize(totalSize)}";
            }
            catch (OperationCanceledException)
            {
                TxtSummary.Text = "统计已取消。";
            }
            catch (Exception ex)
            {
                TxtSummary.Text = "统计失败：" + ex.Message;
            }
            finally
            {
                _cts = null;
                BtnStart.Content = "开始统计";
                TxtSourceDir.IsEnabled = true;
                TxtExtensionFilter.IsEnabled = true;
                PbProgress.IsIndeterminate = false;
            }
        }

        /// <summary>解析过滤框：空 = 全部文件；".pdf|.docx" 或 "pdf,docx" 均可，统一补点并忽略大小写。</summary>
        private static HashSet<string> ParseExtensions(string text)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in text.Split(new[] { '|', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var ext = raw.Trim();
                if (ext.Length == 0)
                {
                    continue;
                }
                if (!ext.StartsWith('.'))
                {
                    ext = "." + ext;
                }
                result.Add(ext);
            }
            return result;
        }

        /// <summary>递归遍历：每个目录产出一行统计；无权限/已消失的子目录跳过，不让整体失败。</summary>
        private static List<DirStatRow> Walk(DirectoryInfo dir, int level, HashSet<string> extensions,
            CancellationToken token, IProgress<string> progress)
        {
            token.ThrowIfCancellationRequested();

            var row = new DirStatRow { Level = level, Name = dir.Name };
            long maxSize = 0;
            long minSize = long.MaxValue;
            var newest = DateTime.MinValue;
            var oldest = DateTime.MaxValue;

            try
            {
                foreach (var file in dir.EnumerateFiles())
                {
                    if (extensions.Count > 0 && !extensions.Contains(file.Extension))
                    {
                        continue;
                    }

                    row.FileCount++;
                    row.TotalSize += file.Length;
                    if (file.Length > maxSize) maxSize = file.Length;
                    if (file.Length < minSize) minSize = file.Length;
                    if (file.LastWriteTime > newest) newest = file.LastWriteTime;
                    if (file.LastWriteTime < oldest) oldest = file.LastWriteTime;
                }
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (DirectoryNotFoundException)
            {
            }

            row.MaxSize = row.FileCount > 0 ? maxSize : 0;
            row.MinSize = row.FileCount > 0 ? minSize : 0;
            row.NewestWrite = row.FileCount > 0 ? newest : null;
            row.OldestWrite = row.FileCount > 0 ? oldest : null;
            progress.Report($"已统计 {row.FileCount} 个文件：{row.DisplayName.Trim()}");

            try
            {
                foreach (var sub in dir.EnumerateDirectories())
                {
                    Walk(sub, level + 1, extensions, token, progress);
                }
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (DirectoryNotFoundException)
            {
            }

            // Walk 需要返回整棵树的行；子目录的行在各自的递归里产生，
            // 这里收集本层及以下所有行返回给调用方
            var rows = new List<DirStatRow> { row };
            return rows;
        }

        /// <summary>解析后的字节数转可读大小。</summary>
        internal static string FormatSize(long bytes)
        {
            return bytes switch
            {
                < 1024 => $"{bytes} B",
                < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
                < 1024L * 1024 * 1024 => $"{bytes / 1024 / 1024.0:F2} MB",
                _ => $"{bytes / 1024 / 1024 / 1024.0:F2} GB"
            };
        }
    }

    /// <summary>DataGrid 行：绑定用展示属性 + 原始值（TotalSize）供汇总求和。</summary>
    public class DirStatRow
    {
        public int Level { get; set; }

        public string Name { get; set; } = string.Empty;

        /// <summary>层级缩进展示（全角空格缩进，中文字体下对齐）。</summary>
        public string DisplayName => new string('\u3000', Level) + Name;

        public int FileCount { get; set; }

        public long TotalSize { get; set; }

        public long MaxSize { get; set; }

        public long MinSize { get; set; }

        public DateTime? NewestWrite { get; set; }

        public DateTime? OldestWrite { get; set; }

        public string TotalSizeDisplay => FileCount == 0 ? "-" : FormatSize(TotalSize);

        public string MaxSizeDisplay => FileCount == 0 ? "-" : FormatSize(MaxSize);

        public string MinSizeDisplay => FileCount == 0 ? "-" : FormatSize(MinSize);

        public string NewestDisplay => NewestWrite?.ToString("yyyy-MM-dd HH:mm") ?? "-";

        public string OldestDisplay => OldestWrite?.ToString("yyyy-MM-dd HH:mm") ?? "-";

        /// <summary>字节数转可读大小（B/KB/MB/GB），供行展示与页汇总共用。</summary>
        public static string FormatSize(long bytes)
        {
            return bytes switch
            {
                < 1024 => $"{bytes} B",
                < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
                < 1024L * 1024 * 1024 => $"{bytes / 1024 / 1024.0:F2} MB",
                _ => $"{bytes / 1024 / 1024 / 1024.0:F2} GB"
            };
        }
    }
}
