using System;
using System.Windows.Forms;
using uIP.Lib.Script;

namespace MyPluginNamespace
{
    public partial class MainForm : Form
    {
        private VideoSplitPlugin _plugin;

        private Button _btnConfig;
        private Button _btnRun;

        public MainForm()
        {
            InitializeComponent();
            // 建立並初始化 Plugin
            _plugin = new VideoSplitPlugin();
            _plugin.Initialize(null);

            // 可以在這裡額外設定 plugin 的語系或權限 (若有需求)
            // _plugin.ChangeLanguage("zh-TW");
            // _plugin.ChangeAccessLvl(1, 5);
        }

        private void InitializeComponent()
        {
            this._btnConfig = new Button() { Text = "設定路徑/間隔", Left = 30, Top = 30, Width = 130 };
            this._btnRun    = new Button() { Text = "執行擷取", Left = 180, Top = 30, Width = 130 };

            this._btnConfig.Click += BtnConfig_Click;
            this._btnRun.Click    += BtnRun_Click;

            this.Controls.Add(_btnConfig);
            this.Controls.Add(_btnRun);

            this.Text = "Video Split Demo";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ClientSize = new System.Drawing.Size(360, 120);
        }

        // 按鈕：彈出設定視窗，讓使用者選擇資料夾與擷取間隔
        private void BtnConfig_Click(object sender, EventArgs e)
        {
            // 直接呼叫 plugin 的設定方法
            _plugin.PopupVideoSplitConfig();
        }

        // 按鈕：建立並執行 Macro (SplitVideos)
        private void BtnRun_Click(object sender, EventArgs e)
        {
            // 1. 透過 Plugin 建立對應的 Macro 實例
            //    這裡 methodName 要和 Plugin 裡面設的常數 "SplitVideos" 相同
            var macroInstance = _plugin.CreateMacroInstance(
                new UDataCarrier[] { new UDataCarrier(typeof(string), "SplitVideos") },
                null,
                null
            );

            if (macroInstance == null)
            {
                MessageBox.Show("建立宏實例失敗！");
                return;
            }

            // 2. 呼叫 fpHandler 執行
            object retValue = null;
            eCallReturn callErr;
            bool success = macroInstance.fpHandler(macroInstance, null, ref retValue, out callErr);

            if (success)
                MessageBox.Show("擷取完成");
            else
                MessageBox.Show($"執行失敗, 錯誤代碼: {callErr}");
        }

        // 視窗關閉時，記得呼叫 Plugin.Close() 釋放資源
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            // 若 plugin 還需要回收或清理動作，可在這裡做
            _plugin.Close();
        }
    }
}
