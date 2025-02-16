using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace MyPluginNamespace
{
    public class MyVideoSplitForm : Form
    {
        private Button btnSelectVideo;
        private TextBox txtVideoPath;
        private Label lblInterval;
        private NumericUpDown numInterval;
        private Button btnExtract;

        // 供外部存取所選影片路徑
        public string SelectedVideoPath => txtVideoPath.Text;

        // 供外部存取輸入的間隔秒數
        public int IntervalSeconds => (int)numInterval.Value;

        public MyVideoSplitForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            // 初始化各種控制項
            this.btnSelectVideo = new Button();
            this.txtVideoPath   = new TextBox();
            this.lblInterval    = new Label();
            this.numInterval    = new NumericUpDown();
            this.btnExtract     = new Button();

            // 
            // btnSelectVideo
            // 
            this.btnSelectVideo.Text = "選擇影片檔...";
            this.btnSelectVideo.Location = new Point(20, 20);
            this.btnSelectVideo.Size = new Size(110, 30);
            this.btnSelectVideo.Click += BtnSelectVideo_Click;
            // 
            // txtVideoPath
            // 
            this.txtVideoPath.Location = new Point(140, 20);
            this.txtVideoPath.Size = new Size(250, 23);
            this.txtVideoPath.ReadOnly = true;
            // 
            // lblInterval
            // 
            this.lblInterval.Location = new Point(20, 70);
            this.lblInterval.AutoSize = true;
            this.lblInterval.Text = "切割間隔(秒):";
            // 
            // numInterval
            // 
            this.numInterval.Location = new Point(120, 67);
            this.numInterval.Size = new Size(80, 23);
            this.numInterval.Minimum = 1;   // 不允許 0 或負數
            this.numInterval.Maximum = 999; // 自行設定上限
            this.numInterval.Value = 30;    // 預設值
            // 
            // btnExtract
            // 
            this.btnExtract.Location = new Point(320, 65);
            this.btnExtract.Size = new Size(70, 30);
            this.btnExtract.Text = "擷取";
            this.btnExtract.Click += BtnExtract_Click;

            // 設定 Form 基本屬性
            this.Text = "影片擷取範例";
            this.StartPosition = FormStartPosition.CenterParent;
            this.ClientSize = new Size(420, 120);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            // 將控制項加入 Form
            this.Controls.Add(this.btnSelectVideo);
            this.Controls.Add(this.txtVideoPath);
            this.Controls.Add(this.lblInterval);
            this.Controls.Add(this.numInterval);
            this.Controls.Add(this.btnExtract);
        }

        /// <summary>
        /// 按下「選擇影片檔...」按鈕，彈出對話框讓使用者選檔
        /// </summary>
        private void BtnSelectVideo_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "MP4 Files|*.mp4|All Files|*.*";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtVideoPath.Text = ofd.FileName;
                }
            }
        }

        /// <summary>
        /// 按下「擷取」按鈕後的動作。
        /// 這裡僅示範彈出一個訊息框，實務上可呼叫 VideoSplitPlugin 進行擷取。
        /// </summary>
        private void BtnExtract_Click(object sender, EventArgs e)
        {
            // 取得使用者選擇的路徑與輸入的間隔秒數
            string videoPath = this.SelectedVideoPath;
            int interval = this.IntervalSeconds;

            if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
            {
                MessageBox.Show("請先選擇正確的影片檔案！", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            MessageBox.Show($"影片: {videoPath}\r\n間隔: {interval} 秒", "擷取示範");

            // === 實際調用你的 VideoSplitPlugin ===
            // 例如：
            // var plugin = new VideoSplitPlugin();
            // plugin.Initialize(null);
            // plugin.SetVideoFile(videoPath);   // 假設你實作了 SetVideoFile() 之類的
            // plugin.SetInterval(interval);
            // plugin.DoExtract();               // 假設你實作了 DoExtract() 之類的
            // plugin.Close();
        }
    }
}
