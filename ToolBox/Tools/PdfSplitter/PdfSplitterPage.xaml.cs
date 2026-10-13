using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.Win32;
using ToolBox.Shared;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using UserControl = System.Windows.Controls.UserControl;

namespace ToolBox.Tools.PdfSplitter
{
    /// <summary>
    /// PDF 拆分/合并页，移植自 PdfSplitter 控制台工具（iTextSharp 5）：
    /// 拆分按总页数均分成 N 份，余数页从第 1 份起每份多 1 页（控制台版四舍五入会丢掉末尾的页，此处已修正）；
    /// 合并按列表顺序把多个 PDF 拼接为一个文件。
    /// </summary>
    public partial class PdfSplitterPage : UserControl, IToolPage
    {
        private readonly ObservableCollection<string> _mergeFiles = new();

        public PdfSplitterPage()
        {
            InitializeComponent();
            MergeFilesList.ItemsSource = _mergeFiles;
        }

        public string Title => "PDF 拆分合并";

        public void OnHostClosing()
        {
        }

        #region 拆分

        private void BtnBrowseSplitSource_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filter = "PDF 文件 (*.pdf)|*.pdf|所有文件 (*.*)|*.*" };
            if (dialog.ShowDialog(Window.GetWindow(this)) == true)
            {
                TxtSplitSourceFile.Text = dialog.FileName;
                if (string.IsNullOrEmpty(TxtSplitOutputDir.Text))
                {
                    TxtSplitOutputDir.Text = Path.GetDirectoryName(dialog.FileName);
                }
            }
        }

        private void BtnBrowseSplitOutput_Click(object sender, RoutedEventArgs e)
        {
            var folder = FolderPicker.PickFolder(Window.GetWindow(this), TxtSplitOutputDir.Text);
            if (folder is not null)
            {
                TxtSplitOutputDir.Text = folder;
            }
        }

        private async void BtnStartSplit_Click(object sender, RoutedEventArgs e)
        {
            var sourceFile = TxtSplitSourceFile.Text;
            var outputDir = TxtSplitOutputDir.Text;

            if (!File.Exists(sourceFile))
            {
                MessageBox.Show(Window.GetWindow(this)!, "请选择有效的 PDF 文件。", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!Directory.Exists(outputDir))
            {
                MessageBox.Show(Window.GetWindow(this)!, "请选择有效的输出目录。", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!int.TryParse(TxtSplitCount.Text, out int splitCount) || splitCount <= 0)
            {
                MessageBox.Show(Window.GetWindow(this)!, "请输入有效的拆分份数（正整数）。", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            SetSplitUiEnabled(false);
            PbSplit.Value = 0;
            TxtSplitStatus.Text = "正在拆分...";

            try
            {
                var outputs = await Task.Run(() => SplitPdf(sourceFile, outputDir, splitCount));
                PbSplit.Value = 100;
                TxtSplitStatus.Text = $"拆分完成：共 {outputs.Count} 个文件，输出到 {outputDir}";
                MessageBox.Show(Window.GetWindow(this)!, $"拆分完成，共生成 {outputs.Count} 个文件。", "完成",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                TxtSplitStatus.Text = "拆分失败：" + ex.Message;
                MessageBox.Show(Window.GetWindow(this)!, "拆分过程中发生错误：" + ex.Message, "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetSplitUiEnabled(true);
            }
        }

        private void SetSplitUiEnabled(bool enabled)
        {
            BtnStartSplit.IsEnabled = enabled;
            TxtSplitCount.IsEnabled = enabled;
            TxtSplitSourceFile.IsEnabled = enabled;
            TxtSplitOutputDir.IsEnabled = enabled;
        }

        /// <summary>
        /// 按总页数均分：每份基础 totalPages/parts 页，余数页从第 1 份起每份多 1 页，保证不丢页。
        /// 控制台版用四舍五入算每份页数，totalPages=100、split=3 时只会写出 99 页，这里按余数分配修正。
        /// </summary>
        private static List<string> SplitPdf(string sourceFile, string outputDir, int splitCount)
        {
            var outputs = new List<string>();
            var baseName = Path.GetFileNameWithoutExtension(sourceFile);

            using (var reader = new PdfReader(sourceFile))
            {
                var totalPages = reader.NumberOfPages;
                var parts = Math.Min(splitCount, totalPages);
                var basePages = totalPages / parts;
                var remainder = totalPages % parts;
                var currentPage = 1;

                for (var part = 1; part <= parts; part++)
                {
                    var pagesInPart = basePages + (part <= remainder ? 1 : 0);
                    var outputPath = Path.Combine(outputDir, $"{baseName}_{part}.pdf");

                    using (var stream = new FileStream(outputPath, FileMode.Create))
                    {
                        var document = new Document();
                        var copy = new PdfCopy(document, stream);
                        document.Open();

                        for (var i = 0; i < pagesInPart; i++)
                        {
                            copy.AddPage(copy.GetImportedPage(reader, currentPage));
                            currentPage++;
                        }

                        document.Close();
                    }

                    outputs.Add(outputPath);
                }
            }

            return outputs;
        }

        #endregion

        #region 合并

        private void BtnAddMergeFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "PDF 文件 (*.pdf)|*.pdf|所有文件 (*.*)|*.*",
                Multiselect = true,
                Title = "选择要合并的 PDF"
            };
            if (dialog.ShowDialog(Window.GetWindow(this)) == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    if (!_mergeFiles.Contains(file))
                    {
                        _mergeFiles.Add(file);
                    }
                }
            }
        }

        private void BtnRemoveMergeFile_Click(object sender, RoutedEventArgs e)
        {
            var selected = MergeFilesList.SelectedItems.Cast<string>().ToList();
            foreach (var file in selected)
            {
                _mergeFiles.Remove(file);
            }
        }

        private void BtnClearMergeFiles_Click(object sender, RoutedEventArgs e)
        {
            _mergeFiles.Clear();
        }

        private void BtnBrowseMergeOutput_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog { Filter = "PDF 文件 (*.pdf)|*.pdf", FileName = "merged.pdf" };
            if (dialog.ShowDialog(Window.GetWindow(this)) == true)
            {
                TxtMergeOutputFile.Text = dialog.FileName;
            }
        }

        private async void BtnStartMerge_Click(object sender, RoutedEventArgs e)
        {
            if (_mergeFiles.Count == 0)
            {
                MessageBox.Show(Window.GetWindow(this)!, "请先添加要合并的 PDF 文件。", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var outputFile = TxtMergeOutputFile.Text;
            if (string.IsNullOrEmpty(outputFile))
            {
                MessageBox.Show(Window.GetWindow(this)!, "请指定输出文件。", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            SetMergeUiEnabled(false);
            PbMerge.Value = 0;
            TxtMergeStatus.Text = "正在合并...";

            try
            {
                var files = _mergeFiles.ToList();
                await Task.Run(() => MergePdf(files, outputFile));
                PbMerge.Value = 100;
                TxtMergeStatus.Text = $"合并完成：{files.Count} 个文件 → {outputFile}";
                MessageBox.Show(Window.GetWindow(this)!, "合并完成！", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                TxtMergeStatus.Text = "合并失败：" + ex.Message;
                MessageBox.Show(Window.GetWindow(this)!, "合并过程中发生错误：" + ex.Message, "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetMergeUiEnabled(true);
            }
        }

        private void SetMergeUiEnabled(bool enabled)
        {
            BtnStartMerge.IsEnabled = enabled;
            MergeFilesList.IsEnabled = enabled;
            TxtMergeOutputFile.IsEnabled = enabled;
        }

        /// <summary>按列表顺序把多个 PDF 拼接为一个文件（PdfCopy 逐页导入）。</summary>
        private static void MergePdf(IReadOnlyList<string> files, string outputFile)
        {
            using (var stream = new FileStream(outputFile, FileMode.Create))
            {
                var document = new Document();
                var copy = new PdfCopy(document, stream);
                document.Open();

                foreach (var file in files)
                {
                    using (var reader = new PdfReader(file))
                    {
                        for (var page = 1; page <= reader.NumberOfPages; page++)
                        {
                            copy.AddPage(copy.GetImportedPage(reader, page));
                        }
                    }
                }

                document.Close();
            }
        }

        #endregion
    }
}
