namespace MyTimeManagement
{
    partial class Form1
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.btnStart = new System.Windows.Forms.Button();
            this.txtInput = new System.Windows.Forms.TextBox();
            this.btnSave = new System.Windows.Forms.Button();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.dtpAlarmTime = new System.Windows.Forms.DateTimePicker();
            this.btnToggleAlarm = new System.Windows.Forms.Button();
            this.lblAlarmStatus = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(584, 368);
            this.tabControl1.TabIndex = 3;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.btnStart);
            this.tabPage1.Controls.Add(this.txtInput);
            this.tabPage1.Controls.Add(this.btnSave);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(576, 342);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "日常记录";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // btnStart
            // 
            this.btnStart.Location = new System.Drawing.Point(12, 12);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(120, 32);
            this.btnStart.TabIndex = 0;
            this.btnStart.Text = "开始计时(30分钟)";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            // 
            // txtInput
            // 
            this.txtInput.AcceptsReturn = true;
            this.txtInput.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtInput.Location = new System.Drawing.Point(12, 60);
            this.txtInput.Multiline = true;
            this.txtInput.Name = "txtInput";
            this.txtInput.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtInput.Size = new System.Drawing.Size(550, 230);
            this.txtInput.TabIndex = 1;
            this.txtInput.TextChanged += new System.EventHandler(this.TxtChange);
            // 
            // btnSave
            // 
            this.btnSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSave.Location = new System.Drawing.Point(440, 300);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(120, 32);
            this.btnSave.TabIndex = 2;
            this.btnSave.Text = "保存到文件";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.lblAlarmStatus);
            this.tabPage2.Controls.Add(this.btnToggleAlarm);
            this.tabPage2.Controls.Add(this.dtpAlarmTime);
            this.tabPage2.Controls.Add(this.label1);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(576, 342);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "定时提醒";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(40, 40);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(83, 12);
            this.label1.TabIndex = 0;
            this.label1.Text = "设置提醒时间:";
            // 
            // dtpAlarmTime
            // 
            this.dtpAlarmTime.Format = System.Windows.Forms.DateTimePickerFormat.Time;
            this.dtpAlarmTime.Location = new System.Drawing.Point(130, 36);
            this.dtpAlarmTime.Name = "dtpAlarmTime";
            this.dtpAlarmTime.ShowUpDown = true;
            this.dtpAlarmTime.Size = new System.Drawing.Size(100, 21);
            this.dtpAlarmTime.TabIndex = 1;
            // 
            // btnToggleAlarm
            // 
            this.btnToggleAlarm.Location = new System.Drawing.Point(250, 34);
            this.btnToggleAlarm.Name = "btnToggleAlarm";
            this.btnToggleAlarm.Size = new System.Drawing.Size(100, 25);
            this.btnToggleAlarm.TabIndex = 2;
            this.btnToggleAlarm.Text = "开启提醒";
            this.btnToggleAlarm.UseVisualStyleBackColor = true;
            this.btnToggleAlarm.Click += new System.EventHandler(this.btnToggleAlarm_Click);
            // 
            // lblAlarmStatus
            // 
            this.lblAlarmStatus.AutoSize = true;
            this.lblAlarmStatus.ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            this.lblAlarmStatus.Location = new System.Drawing.Point(40, 80);
            this.lblAlarmStatus.Name = "lblAlarmStatus";
            this.lblAlarmStatus.Size = new System.Drawing.Size(101, 12);
            this.lblAlarmStatus.TabIndex = 3;
            this.lblAlarmStatus.Text = "当前状态: 未开启";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(584, 368);
            this.Controls.Add(this.tabControl1);
            this.Name = "Form1";
            this.Text = "MyTimeManagement";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage1.PerformLayout();
            this.tabPage2.ResumeLayout(false);
            this.tabPage2.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.TextBox txtInput;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.DateTimePicker dtpAlarmTime;
        private System.Windows.Forms.Button btnToggleAlarm;
        private System.Windows.Forms.Label lblAlarmStatus;
        private System.Windows.Forms.Label label1;
    }
}