using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using uIP.Lib.Utility;
using uIP.Lib.Script;
// 如果你安裝的是 OpenCvSharp4，以下命名空間視版本而定
using OpenCvSharp;

namespace MyPluginNamespace
{
    public class VideoSplitPlugin : UMacroMethodProviderPlugin
    {
        private const string METHOD_SPLIT_VIDEOS = "SplitVideos";

        // 使用者設定參數
        private string _videoFolderPath = string.Empty;
        private int _intervalSeconds = 30;

        public override bool Initialize(UDataCarrier[] param)
        {
            // 建立一個可執行的 Macro，並加入 m_UserQueryOpenedMethods
            var macro = new UMacro(
                methodName: METHOD_SPLIT_VIDEOS,
                fpHandler: SplitVideosHandler,
                immutableParamTypeDesc: null,
                variableParamTypeDesc: null,
                prevPropagationParamTypeDesc: null,
                retPropagationParamTypeDesc: null,
                invisible: false
            )
            {
                ConfigFirst = true,
                ConfigDone = false
            };

            m_UserQueryOpenedMethods.Add(macro);

            // 標記插件已初始化完成
            m_bOpened = true;
            return true;
        }

        // (可選) 如果需要在所有外掛載入完後做進階設定，可在這兩個虛擬方法裡操作
        public override void InitializedDone1stChance(List<UMacroMethodProviderPlugin> envLoadedClasses)
        {
            base.InitializedDone1stChance(envLoadedClasses);
        }
        public override void InitializedDone2ndChance()
        {
            base.InitializedDone2ndChance();
        }

        // 核心執行函式
        private bool SplitVideosHandler(
            UMacro whichMacro,
            object previousRetValue,
            ref object retValue,
            out eCallReturn callErr)
        {
            callErr = eCallReturn.NONE; // 預設成功
            try
            {
                // 1. 檢查資料夾是否存在
                if (!Directory.Exists(_videoFolderPath))
                {
                    callErr = eCallReturn.ERROR;
                    return false;
                }

                // 2. 取得所有 mp4 檔案 (可視需求改成其他格式)
                var videoFiles = Directory.GetFiles(_videoFolderPath, "*.mp4");

                // 3. 逐檔取樣
                foreach (var videoPath in videoFiles)
                {
                    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(videoPath);
                    string outputDir = Path.Combine(_videoFolderPath, fileNameWithoutExt);
                    Directory.CreateDirectory(outputDir);

                    // 用 OpenCvSharp 取得影片總秒數
                    int totalSeconds = GetVideoLengthSeconds(videoPath);

                    // 4. 依間隔秒數擷取
                    for (int currentSec = 0; currentSec < totalSeconds; currentSec += _intervalSeconds)
                    {
                        string outputPhoto = Path.Combine(outputDir, $"{fileNameWithoutExt}_{currentSec}.jpg");
                        ExtractFrameAtSecond(videoPath, currentSec, outputPhoto);
                    }
                }
            }
            catch (Exception ex)
            {
                // 這裡可加入 log 紀錄
                callErr = eCallReturn.ERROR;
                return false;
            }

            return true;
        }

        // 彈窗讓使用者選擇資料夾 & 設定秒數
        public void PopupVideoSplitConfig()
        {
            using (var frm = new FormVideoSplitConfig())
            {
                if (frm.ShowDialog() == DialogResult.OK)
                {
                    _videoFolderPath = frm.SelectedFolderPath;
                    _intervalSeconds = frm.IntervalSeconds;
                }
            }
        }

        public override void Close()
        {
            // 如果有需要關閉其他資源，可在這裡進行
            base.Close();
        }

        /// <summary>
        /// 用 OpenCvSharp 取得影片總秒數 (透過 FrameCount / FPS)
        /// </summary>
        private int GetVideoLengthSeconds(string videoPath)
        {
            // 使用 "VideoCapture" 開檔
            using var capture = new VideoCapture(videoPath);
            // FrameCount 可能是 double；Fps 也是 double 型別
            double frameCount = capture.FrameCount;
            double fps        = capture.Fps;

            if (fps <= 0.1)
            {
                // 如果解析失敗 (某些檔案格式可能找不到 FPS)，可自行預設或改用其他方法
                return 0;
            }

            // 計算總秒數
            double lengthSec = frameCount / fps;
            return (int)Math.Floor(lengthSec);
        }

        /// <summary>
        /// 用 OpenCvSharp 抓指定秒數的畫面，並輸出成 JPG
        /// </summary>
        private void ExtractFrameAtSecond(string videoPath, int second, string outputImagePath)
        {
            // 注意：打開同一檔案多次效率不佳，若要擷取非常多張圖，可考慮在外部用同一個 capture 反覆讀取
            using var capture = new VideoCapture(videoPath);

            double fps = capture.Fps;
            double frameIndex = fps * second;

            // 調整到指定的影格位置
            capture.Set(VideoCaptureProperties.PosFrames, frameIndex);

            using Mat frame = new Mat();
            capture.Read(frame);

            if (!frame.Empty())
            {
                // 寫出 jpg 圖檔
                Cv2.ImWrite(outputImagePath, frame);
            }
        }
    }
}
