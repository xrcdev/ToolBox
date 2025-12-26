using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FileSplitterAndMerge
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
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
                    TxtSplitOutputDir.Text = System.IO.Path.GetDirectoryName(openFileDialog.FileName);
                }
            }
        }

        private void BtnBrowseSplitOutput_Click(object sender, RoutedEventArgs e)
        {
            // 使用 SaveFileDialog 来选择目录（变通方法）
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Title = "选择输出位置 (文件名将被忽略，仅使用目录)";
            saveFileDialog.FileName = "Select_Folder";
            if (saveFileDialog.ShowDialog() == true)
            {
                TxtSplitOutputDir.Text = System.IO.Path.GetDirectoryName(saveFileDialog.FileName);
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
                        string baseFileName = System.IO.Path.GetFileName(sourceFile);

                        // 计算总块数
                        int totalChunks = (int)Math.Ceiling((double)totalBytes / chunkSizeBytes);

                        while (bytesReadTotal < totalBytes)
                        {
                            string partFileName = System.IO.Path.Combine(outputDir, $"{baseFileName}.{fileIndex:D3}");
                            
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
                                        TxtSplitStatus.Text = $"正在写入: {System.IO.Path.GetFileName(partFileName)} ({progress:F1}%)";
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
            string dir = System.IO.Path.GetDirectoryName(firstPartFile);
            string fileName = System.IO.Path.GetFileName(firstPartFile);
            // 假设格式是 name.ext.001, name.ext.002 ...
            // 基础名称是去掉 .001
            string baseNamePattern = fileName.Substring(0, fileName.Length - 3); // 包括最后的点，例如 "file.txt."

            // 简单的查找逻辑：从 .001 开始递增查找
            List<string> parts = new List<string>();
            int index = 1;
            while (true)
            {
                string partPath = System.IO.Path.Combine(dir, $"{baseNamePattern}{index:D3}");
                // 也要考虑如果不带点的 baseNamePattern，或者用户手动改名了
                // 这里假设严格遵循 .001, .002 格式
                
                // 如果找不到 .001 (虽然用户选了)，或者找不到后续，就停止
                // 但用户选的文件可能不是 .001 结尾，如果用户选错了文件怎么办？
                // 我们以用户选的文件为起点。
                if (index == 1 && partPath != firstPartFile)
                {
                    // 用户选的文件名不符合 .001 规则，或者我们推断错了
                    // 尝试直接用用户选的文件作为第一个
                    partPath = firstPartFile;
                    // 重新计算 baseNamePattern
                    if (firstPartFile.EndsWith(".001"))
                    {
                         // 正常情况
                    }
                    else
                    {
                        // 也许是 .part1 格式？
                        // 这里为了简单，强制要求用户选择 .001，或者我们只支持 .001 序列
                        // 如果用户选了 file.txt.001，我们找 file.txt.002
                    }
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
                                TxtMergeStatus.Text = $"正在合并: {System.IO.Path.GetFileName(partPath)}";
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
