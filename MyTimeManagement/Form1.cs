// file: E:\Code\My\MyTimeManagement\MyTimeManagement\Form1.cs
using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Drawing;

namespace MyTimeManagement
{
    public partial class Form1 : Form
    {
        private readonly Timer _halfHourTimer;
        private readonly Timer _alarmTimer;
        private bool _isAlarmSet = false;
        private Icon _trayIcon;
        private IntPtr _trayIconHandle = IntPtr.Zero;

        // Win32 用于前置窗口
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private const int SW_RESTORE = 9;

        public Form1()
        {
            InitializeComponent();

            _trayIcon = CreateTrayIcon();
            notifyIcon1.Icon = _trayIcon;
            notifyIcon1.ContextMenuStrip = trayMenu;
            this.Icon = _trayIcon;
            _halfHourTimer = new Timer();
            _halfHourTimer.Interval = 30 * 60 * 1000; // 30分钟
            //_halfHourTimer.Interval = 3* 1000; // 30分钟
            _halfHourTimer.Tick += HalfHourTimer_Tick;

            _alarmTimer = new Timer();
            _alarmTimer.Interval = 1000; // Check every second
            _alarmTimer.Tick += AlarmTimer_Tick;
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

        private void btnToggleAlarm_Click(object sender, EventArgs e)
        {
            if (_isAlarmSet)
            {
                _alarmTimer.Stop();
                _isAlarmSet = false;
                btnToggleAlarm.Text = "开启提醒";
                lblAlarmStatus.Text = "当前状态: 未开启";
                lblAlarmStatus.ForeColor = System.Drawing.Color.Gray;
                dtpAlarmTime.Enabled = true;
            }
            else
            {
                _alarmTimer.Start();
                _isAlarmSet = true;
                btnToggleAlarm.Text = "停止提醒";
                lblAlarmStatus.Text = $"当前状态: 已开启 (将在 {dtpAlarmTime.Value:HH:mm:ss} 提醒)";
                lblAlarmStatus.ForeColor = System.Drawing.Color.Green;
                dtpAlarmTime.Enabled = false;
            }
        }

        private void AlarmTimer_Tick(object sender, EventArgs e)
        {
            var now = DateTime.Now;
            var target = dtpAlarmTime.Value;

            // 检查时分秒是否匹配 (假设秒数为0触发)
            if (now.Hour == target.Hour && now.Minute == target.Minute && now.Second == 0)
            {
                BringAppToFront();
                
                // 切换到提醒选项卡
                if (tabControl1.SelectedTab != tabPage2)
                {
                    tabControl1.SelectedTab = tabPage2;
                }

                var alarmContent = txtAlarmContent.Text.Trim();
                var message = string.IsNullOrWhiteSpace(alarmContent)
                    ? "预设的时间到了！"
                    : "预设的时间到了！" + Environment.NewLine + Environment.NewLine + "提醒内容：" + alarmContent;

                MessageBox.Show(this, message, "提醒", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
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
                var dir = @"E:\Doc\obnote\7老码农的日常\"+DateTime.Now.Year.ToString();
                Directory.CreateDirectory(dir);

                // 文件名：2025{yyyy-MM-dd}.txt —— 按你的要求拼接 2026年2月26日
                var fileName = DateTime.Now.ToString("yyyy-M-d") + ".md";
                var filePath = Path.Combine(dir, fileName);

                using (var sw = new StreamWriter(filePath, true, Encoding.UTF8))
                {
                    sw.WriteLine(Environment.NewLine);
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

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            notifyIcon1.Visible = false;
            if (_trayIconHandle != IntPtr.Zero)
            {
                DestroyIcon(_trayIconHandle);
                _trayIconHandle = IntPtr.Zero;
            }
            if (_trayIcon != null)
            {
                _trayIcon.Dispose();
                _trayIcon = null;
            }
            base.OnFormClosed(e);
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
                notifyIcon1.BalloonTipTitle = "MyTimeManagement";
                notifyIcon1.BalloonTipText = "已最小化到托盘";
                notifyIcon1.ShowBalloonTip(1000);
            }
        }

        private void notifyIcon1_DoubleClick(object sender, EventArgs e)
        {
            ShowFromTray();
        }

        private void trayMenuOpen_Click(object sender, EventArgs e)
        {
            ShowFromTray();
        }

        private void trayMenuExit_Click(object sender, EventArgs e)
        {
            notifyIcon1.Visible = false;
            Close();
        }

        private void ShowFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            BringAppToFront();
        }

        private Icon CreateTrayIcon()
        {
            using (var bitmap = new Bitmap(16, 16))
            using (var g = Graphics.FromImage(bitmap))
            using (var faceBrush = new SolidBrush(Color.White))
            using (var borderPen = new Pen(Color.FromArgb(0, 120, 215), 2))
            using (var handPen = new Pen(Color.FromArgb(0, 120, 215), 2))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                g.FillEllipse(faceBrush, 2, 2, 12, 12);
                g.DrawEllipse(borderPen, 2, 2, 12, 12);

                g.DrawLine(handPen, 8, 8, 8, 4);
                g.DrawLine(handPen, 8, 8, 11, 9);

                _trayIconHandle = bitmap.GetHicon();
                bitmap.Save("tray_icon.png"); // 可选：保存生成的图标以供调试查看
                return Icon.FromHandle(_trayIconHandle);
            }
        }
    }
}