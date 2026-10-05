using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ToolBox.Shared;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using UserControl = System.Windows.Controls.UserControl;

namespace ToolBox.Tools.FileSplitterAndMerge
{
    /// <summary>
    /// 移植自 FileSplitterAndMerge.MainWindow（.NET Framework 4.6.2 老项目，迁移到 net10.0-windows）：
    /// 大文件按固定大小分割为 .001/.002 分块，以及把分块合并还原。
    /// 原项目用 SaveFileDialog 变通选择目录，此处改用共享 FolderPicker。
    /// </summary>
    public partial class FileSplitterPage : UserControl, IToolPage
    {
        public FileSplitterPage()
        {
            InitializeComponent();
        }

        public string Title => "文件分割合并";

        public void OnHostClosing()
        {
        }

        #region Split Logic

        private void BtnBrowseSplitSource_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                TxtSplitSourceFile.Text = openFileDialog.FileName;
                // 自动设置默认输出目录为源文件所在目录
                if (string.IsNullOrEmpty(TxtSplitOutputDir.Text))
                {
                    TxtSplitOutputDir.Text = Path.GetDirectoryName(openFileDialog.FileName);
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
            string sourceFile = TxtSplitSourceFile.Text;
            string outputDir = TxtSplitOutputDir.Text;

            if (!File.Exists(sourceFile))
            {
                MessageBox.Show("请选择有效的源文件。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!Directory.Exists(outputDir))
            {
                MessageBox.Show("请选择有效的输出目录。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!int.TryParse(TxtChunkSize.Text, out int chunkSizeMB) || chunkSizeMB <= 0)
            {
                MessageBox.Show("请输入有效的块大小 (MB)。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            long chunkSizeBytes = (long)chunkSizeMB * 1024 * 1024;
            byte[] buffer = new byte[1024 * 1024]; // 1MB buffer for reading

            BtnStartSplit_Click_SetUI(false);
            TxtSplitStatus.Text = "正在准备分割...";
            PbSplit.Value = 0;

            try
            {
                await Task.Run(() =>
                {
                    using (FileStream fsRead = new FileStream(sourceFile, FileMode.Open, FileAccess.Read))
                    {
                        long totalBytes = fsRead.Length;
                        long bytesReadTotal = 0;
                        int fileIndex = 1;
                        string baseFileName = Path.GetFileName(sourceFile);

                        // 计算总块数
                        int totalChunks = (int)Math.Ceiling((double)totalBytes / chunkSizeBytes);

                        while (bytesReadTotal < totalBytes)
                        {
                            string partFileName = Path.Combine(outputDir, $"{baseFileName}.{fileIndex:D3}");

                            using (FileStream fsWrite = new FileStream(partFileName, FileMode.Create, FileAccess.Write))
                            {
                                long bytesWrittenForCurrentPart = 0;
                                while (bytesWrittenForCurrentPart < chunkSizeBytes && bytesReadTotal < totalBytes)
                                {
                                    int bytesToRead = (int)Math.Min(buffer.Length, chunkSizeBytes - bytesWrittenForCurrentPart);
                                    bytesToRead = (int)Math.Min(bytesToRead, totalBytes - bytesReadTotal);

                                    int read = fsRead.Read(buffer, 0, bytesToRead);
                                    if (read == 0) break;

                                    fsWrite.Write(buffer, 0, read);
                                    bytesReadTotal += read;
                                    bytesWrittenForCurrentPart += read;

                                    // 更新进度
                                    Dispatcher.Invoke(() =>
                                    {
                                        double progress = (double)bytesReadTotal / totalBytes * 100;
                                        PbSplit.Value = progress;
                                        TxtSplitStatus.Text = $"正在写入: {Path.GetFileName(partFileName)} ({progress:F1}%)";
                                    });
                                }
                            }
                            fileIndex++;
                        }
                    }
                });

                TxtSplitStatus.Text = "分割完成！";
                MessageBox.Show("文件分割成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                TxtSplitStatus.Text = "发生错误: " + ex.Message;
                MessageBox.Show($"分割过程中发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnStartSplit_Click_SetUI(true);
            }
        }

        private void BtnStartSplit_Click_SetUI(bool isEnabled)
        {
            BtnStartSplit.IsEnabled = isEnabled;
            TxtSplitSourceFile.IsEnabled = isEnabled;
            TxtSplitOutputDir.IsEnabled = isEnabled;
            TxtChunkSize.IsEnabled = isEnabled;
        }

        #endregion

        #region Merge Logic

        private void BtnBrowseMergeSource_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Part files (*.001)|*.001|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                TxtMergeSourceFile.Text = openFileDialog.FileName;

                // 尝试推断输出文件名
                string part1 = openFileDialog.FileName;
                if (part1.EndsWith(".001"))
                {
                    string originalName = part1.Substring(0, part1.Length - 4);
                    TxtMergeOutputFile.Text = originalName;
                }
            }
        }

        private void BtnBrowseMergeOutput_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            if (saveFileDialog.ShowDialog() == true)
            {
                TxtMergeOutputFile.Text = saveFileDialog.FileName;
            }
        }

        private async void BtnStartMerge_Click(object sender, RoutedEventArgs e)
        {
            string firstPartFile = TxtMergeSourceFile.Text;
            string outputFile = TxtMergeOutputFile.Text;

            if (!File.Exists(firstPartFile))
            {
                MessageBox.Show("请选择有效的首个分块文件 (.001)。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrEmpty(outputFile))
            {
                MessageBox.Show("请指定输出文件路径。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 查找所有部分
            string dir = Path.GetDirectoryName(firstPartFile);
            string fileName = Path.GetFileName(firstPartFile);
            // 假设格式是 name.ext.001, name.ext.002 ...
            // 基础名称是去掉 .001
            string baseNamePattern = fileName.Substring(0, fileName.Length - 3); // 包括最后的点，例如 "file.txt."

            // 简单的查找逻辑：从 .001 开始递增查找
            List<string> parts = new List<string>();
            int index = 1;
            while (true)
            {
                string partPath = Path.Combine(dir, $"{baseNamePattern}{index:D3}");

                // 如果用户选的文件名不符合 .001 规则，或者我们推断错了，
                // 尝试直接用用户选的文件作为第一个分块
                if (index == 1 && partPath != firstPartFile)
                {
                    partPath = firstPartFile;
                }

                if (File.Exists(partPath))
                {
                    parts.Add(partPath);
                    index++;
                }
                else
                {
                    break;
                }
            }

            if (parts.Count == 0)
            {
                MessageBox.Show("未找到任何分块文件。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            byte[] buffer = new byte[1024 * 1024]; // 1MB buffer

            BtnStartMerge_Click_SetUI(false);
            TxtMergeStatus.Text = "正在准备合并...";
            PbMerge.Value = 0;

            try
            {
                await Task.Run(() =>
                {
                    using (FileStream fsWrite = new FileStream(outputFile, FileMode.Create, FileAccess.Write))
                    {
                        long totalBytes = 0;
                        // 预先计算总大小用于进度条（可选，会增加IO）
                        foreach (var part in parts) totalBytes += new FileInfo(part).Length;

                        long bytesWrittenTotal = 0;

                        foreach (var partPath in parts)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                TxtMergeStatus.Text = $"正在合并: {Path.GetFileName(partPath)}";
                            });

                            using (FileStream fsRead = new FileStream(partPath, FileMode.Open, FileAccess.Read))
                            {
                                int read;
                                while ((read = fsRead.Read(buffer, 0, buffer.Length)) > 0)
                                {
                                    fsWrite.Write(buffer, 0, read);
                                    bytesWrittenTotal += read;

                                    Dispatcher.Invoke(() =>
                                    {
                                        PbMerge.Value = (double)bytesWrittenTotal / totalBytes * 100;
                                    });
                                }
                            }
                        }
                    }
                });

                TxtMergeStatus.Text = "合并完成！";
                MessageBox.Show("文件合并成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                TxtMergeStatus.Text = "发生错误: " + ex.Message;
                MessageBox.Show($"合并过程中发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnStartMerge_Click_SetUI(true);
            }
        }

        private void BtnStartMerge_Click_SetUI(bool isEnabled)
        {
            BtnStartMerge.IsEnabled = isEnabled;
            TxtMergeSourceFile.IsEnabled = isEnabled;
            TxtMergeOutputFile.IsEnabled = isEnabled;
        }

        #endregion
    }
}
