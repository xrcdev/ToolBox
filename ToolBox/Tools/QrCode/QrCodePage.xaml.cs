using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using QRCoder;
using MessageBox = System.Windows.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using UserControl = System.Windows.Controls.UserControl;

namespace ToolBox.Tools.QrCode
{
    /// <summary>
    /// 二维码生成页，移植自 QrCodeGen 控制台工具（QRCoder 1.6）：
    /// 文本 + 纠错级别（L/M/Q/H）+ 模块大小 → PNG 预览，可导出 PNG 文件。
    /// PngByteQRCode 不依赖 System.Drawing，WPF 工程下无类型歧义。
    /// </summary>
    public partial class QrCodePage : UserControl, IToolPage
    {
        private byte[]? _pngBytes;

        public QrCodePage()
        {
            InitializeComponent();
        }

        public string Title => "二维码生成";

        public void OnHostClosing()
        {
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            var content = TxtContent.Text;
            if (content.Length == 0)
            {
                TxtStatus.Text = "请输入二维码内容。";
                return;
            }

            if (!int.TryParse(TxtModuleSize.Text, out var moduleSize) || moduleSize < 2 || moduleSize > 20)
            {
                TxtStatus.Text = "模块大小需在 2-20 之间。";
                return;
            }

            var eccLevel = CbEccLevel.SelectedIndex switch
            {
                0 => QRCodeGenerator.ECCLevel.L,
                2 => QRCodeGenerator.ECCLevel.Q,
                3 => QRCodeGenerator.ECCLevel.H,
                _ => QRCodeGenerator.ECCLevel.M
            };

            try
            {
                using var generator = new QRCodeGenerator();
                using var data = generator.CreateQrCode(content, eccLevel);
                using var qrCode = new PngByteQRCode(data);
                _pngBytes = qrCode.GetGraphic(moduleSize);

                var image = new BitmapImage();
                using (var stream = new MemoryStream(_pngBytes))
                {
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = stream;
                    image.EndInit();
                }
                image.Freeze();
                ImgQr.Source = image;

                BtnSavePng.IsEnabled = true;
                TxtStatus.Text = $"已生成 {_pngBytes.Length / 1024.0:F1} KB，点「保存 PNG」导出。";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "生成失败：" + ex.Message;
            }
        }

        private void BtnSavePng_Click(object sender, RoutedEventArgs e)
        {
            if (_pngBytes is null || _pngBytes.Length == 0)
            {
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "PNG 图片 (*.png)|*.png",
                FileName = "qrcode.png"
            };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true)
            {
                return;
            }

            try
            {
                File.WriteAllBytes(dialog.FileName, _pngBytes);
                TxtStatus.Text = "已保存：" + dialog.FileName;
            }
            catch (Exception ex)
            {
                MessageBox.Show(Window.GetWindow(this)!, "保存失败：" + ex.Message, "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
