using Microsoft.Win32;

using Newtonsoft.Json;

using Serilog;
using Serilog.Core;

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
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using static System.Windows.Forms.VisualStyles.VisualStyleElement;

using Clipboard = System.Windows.Clipboard;
using Path = System.IO.Path;
using Window = System.Windows.Window;

namespace CopyFiles
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string _historyConfig = "history.json";
        Logger _logger = new LoggerConfiguration()
            .WriteTo.File("log.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();
        public MainWindow()
        {
            InitializeComponent();
            //init Serilog to write file log


        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var path = System.Reflection.Assembly.GetExecutingAssembly().Location;
            _historyConfig = Path.Combine(Path.GetDirectoryName(path), "history.json");
            if (File.Exists(_historyConfig))
            {
                try
                {
                    var history = Newtonsoft.Json.JsonConvert.DeserializeObject<History>(File.ReadAllText(_historyConfig));
                    txtInput.Text = history.InputFolder;
                    txtOutFolder.Text = history.OutputFolder;
                    txtInputFileNames.Text = history.InputFileNames;
                }
                catch (Exception ex)
                {
                    File.Delete(_historyConfig);
                    SaveHistory();
                }
            }
            else
            {
                SaveHistory();
            }
        }

        private void btnInput_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                txtInput.Text = dialog.SelectedPath;
                txtOutFolder.Text = dialog.SelectedPath + "_Copy";
                SaveHistory();
            }
        }

        private void btnOutputSelect_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                txtOutFolder.Text = dialog.SelectedPath;
                SaveHistory();
            }
        }

        private void btnCopy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var inputFolder = (txtInput.Text ?? "").Trim();
                var outputFolder = (txtOutFolder.Text ?? "").Trim();
                if (!Directory.Exists(outputFolder))
                    Directory.CreateDirectory(outputFolder);

                //源文件夹填写的是一个文件夹,则拷贝输入框中输入的文件名称列表
                if ((File.GetAttributes(inputFolder) & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    var input = txtInputFileNames.Text ?? "";
                    Dictionary<string, List<string>> dicFiles = new Dictionary<string, List<string>>();
                    List<string> fileList = new List<string>();
                    if (cbx_EnableSubDir.IsChecked == true)
                    {
                        var subDirs = Directory.GetDirectories(inputFolder, "*", SearchOption.AllDirectories);
                        foreach (var subDir in subDirs)
                        {
                            var subDirName = subDir.Substring(inputFolder.Length + 1);
                            if (subDirName.ToLowerInvariant().Equals(".git"))
                            {
                                continue;
                            }
                            var subDirFiles = Directory.GetFiles(subDir);
                            fileList.AddRange(subDirFiles);
                        }
                    }
                    else
                    {
                        fileList = Directory.GetFiles(inputFolder, "*", SearchOption.TopDirectoryOnly).ToList();
                    }
                    fileList = fileList.Select(t => t.ToLowerInvariant()).ToList();
                    fileList.GroupBy(g => Path.GetExtension(g)).ToList()
                            .ForEach(g => dicFiles.Add(g.Key, g.ToList()));

                    var lines = input.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var item in lines)
                    {
                        var fName = item.Trim().ToLowerInvariant();
                        bool isWildcard = false;
                        if (cbx_EnableWildcard.IsChecked.Value && fName.Contains("*"))
                        {
                            isWildcard = true;
                            //判断通配符是否在文件的扩展名中
                            var ext = Path.GetExtension(fName);
                            var matchedFiles = new List<string>();
                            var wildcard = "";
                            if (ext.Contains("*"))
                            {
                                if (ext == ".*")
                                {
                                    if (fName.Replace(".*", "") == "*")//表示所有文件
                                    {
                                        wildcard = "*.*";
                                        //deep copy fileList to matchedFiles
                                        matchedFiles = fileList.ToList();
                                    }
                                    else//只匹配文件名,不匹配扩展名
                                    {
                                        var toMatchName = Path.GetFileNameWithoutExtension(fName);
                                        wildcard = toMatchName + ".*";
                                        matchedFiles = fileList.Where(t => Path.GetFileNameWithoutExtension(t).Contains(toMatchName)).ToList();
                                    }
                                }

                                else
                                {
                                    //TODO: 扩展名中包含通配符,暂时不支持,分为开头,中间,结尾三种情况
                                    var toMatchExt = ext;
                                    wildcard = ext;
                                    if (ext.EndsWith("*"))
                                    {
                                        //matchedFiles = fileList.Where(t => Path.GetExtension(t).EndsWith(toMatchExt)).ToList();
                                        dicFiles.Keys.Where(t => t.EndsWith(toMatchExt)).ToList()
                                        .ForEach(d => matchedFiles.AddRange(dicFiles[d]));
                                    }
                                    else if (toMatchExt.StartsWith("*"))
                                    {
                                        //matchedFiles = fileList.Where(t => Path.GetExtension(t).StartsWith(toMatchExt)).ToList();
                                        dicFiles.Keys.Where(t => t.StartsWith(toMatchExt)).ToList()
                                       .ForEach(d => matchedFiles.AddRange(dicFiles[d]));
                                    }
                                    else
                                    {
                                        var toMatchExts = toMatchExt.Split("*", options: StringSplitOptions.RemoveEmptyEntries);
                                        //matchedFiles = fileList.Where(t => Path.GetExtension(t).Contains(toMatchExts[0]) && Path.GetExtension(t).Contains(toMatchExts[1])).ToList();

                                        dicFiles.Keys.Where(t => t.Contains(toMatchExts[0]) && t.Contains(toMatchExts[1])).ToList()
                                      .ForEach(d => matchedFiles.AddRange(dicFiles[d]));
                                    }
                                }
                                //var matchedFiles = fileList.Where(t => t.Contains(ext.Replace("*", "")));
                                foreach (var matchedFile in matchedFiles)
                                {
                                    var fileName = Path.GetFileName(matchedFile);
                                    var outPath = Path.Combine(outputFolder, fileName);
                                    File.Copy(matchedFile, outPath, true);
                                    txtOutput.Text += $"{Path.GetFileName(matchedFile)} => {Path.GetFileName(outPath)}  ✔ 通过扩展名通配符{ext}" + Environment.NewLine;
                                    _logger.Information($"{matchedFile} => {outPath}  ✔ 通过扩展名通配符{ext}");
                                }
                            }
                            else
                            {
                                var extent = Path.GetExtension(fName).ToLowerInvariant();
                                var tFileList = dicFiles[extent];
                                var toMatchName = Path.GetFileNameWithoutExtension(fName);
                                if (toMatchName.EndsWith("*"))
                                {
                                    toMatchName = toMatchName.Replace("*", "");
                                    matchedFiles = tFileList.Where(t => Path.GetFileName(t).StartsWith(toMatchName)).ToList();
                                }
                                else if (toMatchName.StartsWith("*"))
                                {
                                    toMatchName = toMatchName.Replace("*", "");
                                    matchedFiles = tFileList.Where(t => Path.GetFileName(t).EndsWith(toMatchName)).ToList();
                                }
                                else
                                {
                                    var toMatchNames = toMatchName.Split("*", options: StringSplitOptions.RemoveEmptyEntries);
                                    matchedFiles = tFileList.Where(t => Path.GetFileName(t).Contains(toMatchNames[0]) && Path.GetFileName(t).Contains(toMatchNames[1])).ToList();
                                }
                                //var matchedFiles = fileList.Where(t => t.Contains(ext.Replace("*", "")));
                                foreach (var matchedFile in matchedFiles)
                                {
                                    var fileName = Path.GetFileName(matchedFile);
                                    var outPath = Path.Combine(outputFolder, fileName);
                                    File.Copy(matchedFile, outPath, true);
                                    txtOutput.Text += $"{Path.GetFileName(matchedFile)} => {outPath}  ✔ 通过文件名通配符:{Path.GetFileName(toMatchName)}" + Environment.NewLine;
                                    _logger.Information($"{matchedFile} => {outPath}  ✔ 通过文件名通配符:{toMatchName}");
                                }
                            }

                        }
                        if (!isWildcard)
                        {
                            try
                            {
                                var fileName = item.Trim();
                                var inputFile = Path.Combine(txtInput.Text, fileName);
                                if (File.Exists(inputFile))
                                {
                                    var inputFileLower = inputFile.ToLowerInvariant();
                                    var matchedFiles = fileList.Where(t => t == inputFileLower);
                                    if (matchedFiles.Count() > 1)
                                    {
                                        if (rb_UseNewFile.IsChecked.Value)
                                        {
                                            inputFile = matchedFiles.OrderByDescending(t => File.GetLastWriteTime(t)).First();
                                        }
                                        else
                                        {
                                            inputFile = matchedFiles.OrderBy(t => File.GetLastWriteTime(t)).First();
                                        }
                                    }
                                    var outPath = Path.Combine(outputFolder, fileName);
                                    File.Copy(inputFile, outPath, true);
                                    txtOutput.Text += $"{Path.GetFileName(inputFile)} => {Path.GetFileName(outPath)}  ✔" + Environment.NewLine;
                                    _logger.Information($"{inputFile} => {outPath}  ✔");
                                }
                                else
                                {
                                    txtOutput.Text += $"{fileName}不存在 ✘" + Environment.NewLine;
                                }
                            }
                            catch (Exception ex)
                            {
                                txtOutput.Text += $"{item}拷贝过程出现异常 ✘:{ex.Message}" + Environment.NewLine;
                            }

                        }
                    }
                }
                //源文件夹填写的是一个文件,则拷贝这个文件本身
                else
                {
                    var inputFile = inputFolder;
                    var fileName = Path.GetFileName(inputFile);
                    var outPath = Path.Combine(outputFolder, fileName);

                    if (File.Exists(inputFile))
                    {
                        File.Copy(inputFile, outPath, true);
                        txtOutput.Text += $"{Path.GetFileName(inputFile)} => {Path.GetFileName(outPath)}  ✔" + Environment.NewLine;
                        _logger.Information($"{inputFile} => {outPath}  ✔");
                    }
                    else
                    {
                        txtOutput.Text += $"拷贝{fileName}失败 ✘" + Environment.NewLine;
                    }
                }
                SaveHistory();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveHistory();
        }

        private void txtInput_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (Clipboard.ContainsText())
            {
                e.Handled = true; // Prevent standard text input from occurring
                txtInput.Text = Clipboard.GetText();
                // Trigger custom event to handle pasted content here.
            }
        }


        private void SaveHistory()
        {
            File.WriteAllText(_historyConfig, Newtonsoft.Json.JsonConvert.SerializeObject(new History()
            {
                InputFolder = txtInput.Text,
                OutputFolder = txtOutFolder.Text,
                InputFileNames = txtInputFileNames.Text
            }));
        }

        private void txtInputFileNames_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }
}
