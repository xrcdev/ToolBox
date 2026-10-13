using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ToolBox.Shared;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using UserControl = System.Windows.Controls.UserControl;

namespace ToolBox.Tools.BatchRename
{
    /// <summary>
    /// 批量重命名页：替换 / 加序号 / 加日期三种模式，先出预览再执行；
    /// 冲突（目标已存在、批内重名、批内交换）的行标状态后跳过，不让整体失败。
    /// </summary>
    public partial class BatchRenamePage : UserControl, IToolPage
    {
        private readonly ObservableCollection<string> _files = new();
        private readonly ObservableCollection<RenameRow> _preview = new();

        public BatchRenamePage()
        {
            InitializeComponent();
            FilesList.ItemsSource = _files;
            PreviewGrid.ItemsSource = _preview;
        }

        public string Title => "批量重命名";

        public void OnHostClosing()
        {
        }

        private void BtnAddFiles_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "所有文件 (*.*)|*.*",
                Multiselect = true,
                Title = "选择要重命名的文件"
            };
            if (dialog.ShowDialog(Window.GetWindow(this)) == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    if (!_files.Contains(file))
                    {
                        _files.Add(file);
                    }
                }
                RefreshPreview();
            }
        }

        private void BtnAddFolder_Click(object sender, RoutedEventArgs e)
        {
            var folder = FolderPicker.PickFolder(Window.GetWindow(this), string.Empty);
            if (folder is null)
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(folder))
            {
                if (!_files.Contains(file))
                {
                    _files.Add(file);
                }
            }
            RefreshPreview();
        }

        private void BtnRemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = FilesList.SelectedItems.Cast<string>().ToList();
            foreach (var file in selected)
            {
                _files.Remove(file);
            }
            RefreshPreview();
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            _files.Clear();
            RefreshPreview();
        }

        private void Mode_Changed(object sender, RoutedEventArgs e)
        {
            // XAML 解析期间 IsChecked=True 就触发，此时面板还没建好，直接返回
            if (PnlReplace is null || PnlSequence is null || PnlDate is null)
            {
                return;
            }

            PnlReplace.Visibility = RbReplace.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PnlSequence.Visibility = RbSequence.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PnlDate.Visibility = RbDate.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            RefreshPreview();
        }

        private void Setting_Changed(object sender, RoutedEventArgs e)
        {
            if (PreviewGrid is null)
            {
                return;
            }
            RefreshPreview();
        }

        // [MODE_REGION]

        /// <summary>按当前模式生成预览：新名为空/目标已存在/批内重名/无变化的行标状态，执行时跳过。</summary>
        private void RefreshPreview()
        {
            _preview.Clear();
            if (_files.Count == 0)
            {
                TxtStatus.Text = "添加文件后自动生成预览。";
                return;
            }

            var dir = Path.GetDirectoryName(_files[0]) ?? string.Empty;
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var start = 1;
            if (!int.TryParse(TxtStart.Text, out var parsed) || parsed < 1)
            {
                parsed = 1;
            }
            start = parsed;

            foreach (var file in _files)
            {
                var oldName = Path.GetFileName(file);
                var baseName = Path.GetFileNameWithoutExtension(file);
                var ext = Path.GetExtension(file);
                string newName;

                if (RbReplace.IsChecked == true)
                {
                    // 替换模式：忽略大小写走正则（Regex.Escape 先转义查找串）
                    var find = TxtFind.Text;
                    var replace = TxtReplace.Text;
                    newName = find.Length == 0
                        ? baseName
                        : CbIgnoreCase.IsChecked == true
                            ? Regex.Replace(baseName, Regex.Escape(find), replace, RegexOptions.IgnoreCase)
                            : baseName.Replace(find, replace);
                }
                else if (RbSequence.IsChecked == true)
                {
                    // 序号模式：{n} 从起始序号起 3 位补零，{name} 原名
                    var template = string.IsNullOrWhiteSpace(TxtTemplate.Text) ? "{name}_{n}" : TxtTemplate.Text;
                    newName = template
                        .Replace("{name}", baseName)
                        .Replace("{n}", start.ToString().PadLeft(3, '0'));
                    start++;
                }
                else
                {
                    // 日期模式：格式无效回退 yyyyMMdd；日期加在名称前/后
                    var format = string.IsNullOrWhiteSpace(TxtDateFormat.Text) ? "yyyyMMdd" : TxtDateFormat.Text;
                    string stamp;
                    try { stamp = DateTime.Now.ToString(format); }
                    catch (FormatException) { stamp = DateTime.Now.ToString("yyyyMMdd"); }
                    newName = CbDateAfter.IsChecked == true ? $"{baseName}_{stamp}" : $"{stamp}_{baseName}";
                }

                newName += ext;
                var status = string.Empty;
                var newPath = Path.Combine(dir, newName);

                if (newName.Length == ext.Length)
                {
                    status = "新名称为空";
                }
                else if (string.Equals(file, newPath, StringComparison.OrdinalIgnoreCase))
                {
                    status = "无变化";
                }
                else if (File.Exists(newPath))
                {
                    status = "目标已存在";
                }
                else if (!used.Add(newName))
                {
                    status = "批内重名";
                }

                _preview.Add(new RenameRow
                {
                    OldName = oldName,
                    NewName = newName,
                    Status = status,
                    OldPath = file,
                    NewPath = newPath
                });
            }

            var ready = _preview.Count(r => r.Status.Length == 0);
            TxtStatus.Text = $"就绪 {ready} / 共 {_preview.Count}（标状态的行执行时跳过）";
        }

        private void BtnExecuteRename_Click(object sender, RoutedEventArgs e)
        {
            var done = 0;
            var skipped = 0;
            var failed = 0;

            foreach (var row in _preview.ToList())
            {
                if (row.Status.Length > 0)
                {
                    skipped++;
                    continue;
                }

                try
                {
                    File.Move(row.OldPath, row.NewPath);
                    row.Status = "已完成";
                    done++;
                }
                catch (Exception ex)
                {
                    row.Status = "失败：" + ex.Message;
                    failed++;
                }
            }

            // 重建文件列表：已完成的行换新路径，其余保持旧路径
            _files.Clear();
            foreach (var row in _preview)
            {
                _files.Add(row.Status == "已完成" ? row.NewPath : row.OldPath);
            }

            TxtStatus.Text = $"完成 {done}，跳过 {skipped}，失败 {failed}";
            RefreshPreview();
        }
    }

    /// <summary>预览行：旧名/新名/状态；状态非空的行执行时跳过。</summary>
    public class RenameRow
    {
        public string OldName { get; set; } = string.Empty;

        public string NewName { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        internal string OldPath { get; set; } = string.Empty;

        internal string NewPath { get; set; } = string.Empty;
    }
}
