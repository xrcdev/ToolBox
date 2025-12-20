// file: E:\Code\My\MyTimeManagement\MyTimeManagement\Form1.cs
using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace MyTimeManagement
{
    public partial class Form1 : Form
    {
        private readonly Timer _halfHourTimer;

        // Win32 用于前置窗口
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        public Form1()
        {
            InitializeComponent();

            _halfHourTimer = new Timer();
            _halfHourTimer.Interval = 30 * 60 * 1000; // 30分钟
            //_halfHourTimer.Interval = 3* 1000; // 30分钟
            _halfHourTimer.Tick += HalfHourTimer_Tick;
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            _halfHourTimer.Stop();
            _halfHourTimer.Start();
            btnStart.Enabled = false;
            this.Text = "计时中...(30分钟后提醒)";
        }

        private void HalfHourTimer_Tick(object sender, EventArgs e)
        {
            _halfHourTimer.Stop();
            btnStart.Enabled = true;
            this.Text = "MyTimeManagement";

            BringAppToFront();

            MessageBox.Show(this,
                "时间到：已过30分钟！",
                "提醒",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void BringAppToFront()
        {
            try
            {
                if (this.WindowState == FormWindowState.Minimized)
                {
                    ShowWindow(this.Handle, SW_RESTORE);
                }

                // 先置顶再取消置顶以获得前置焦点
                this.TopMost = true;
                this.TopMost = false;

                // 激活并前置
                this.Activate();
                this.BringToFront();
                SetForegroundWindow(this.Handle);
                this.Focus();
            }
            catch
            {
                // 忽略前置失败的异常，避免影响后续提示
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            var text = txtInput.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                MessageBox.Show(this, "请输入要保存的内容。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                txtInput.Focus();
                return;
            }

            try
            {
                var dir = @"E:\Doc\obnote\7老码农的日常\2025";
                Directory.CreateDirectory(dir);

                // 文件名：2025{yyyy-MM-dd}.txt —— 按你的要求拼接
                var fileName = DateTime.Now.ToString("yyyy-MM-dd") + ".md";
                var filePath = Path.Combine(dir, fileName);

                using (var sw = new StreamWriter(filePath, true, Encoding.UTF8))
                {
                    sw.WriteLine("【{0:yyyy-MM-dd HH:mm:ss}】 \r\n {1}", DateTime.Now, text.Trim());
                }

                txtInput.Clear();
                txtInput.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "保存失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        string tempFilePath = Path.Combine(Path.GetTempPath(), "MyTimeManagement_Temp.txt");
        private void TxtChange(object sender, EventArgs e)
        {
            //将文本框的内容,写到临时文件中(覆盖写入)
            try
            {
                File.WriteAllText(tempFilePath, txtInput.Text, Encoding.UTF8);
            }
            catch
            {
                //忽略写入失败的异常
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            //读取临时文件的内容,恢复到文本框中
            try
            {
                if (File.Exists(tempFilePath))
                {
                    var content = File.ReadAllText(tempFilePath, Encoding.UTF8);
                    txtInput.Text = content;
                }
            }
            catch
            {
                //忽略读取失败的异常
            }
        }
    }
}