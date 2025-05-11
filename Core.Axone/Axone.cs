using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Formats.Tar;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Axone.Engine;
using Core.VGV.Extend;
using Extend;
using Extend.GuiFlow;
using OpenTK.GLControl;
using SLPush;

namespace Core.VGV
{
    public partial class Axone : Form
    {

        public ToggleMenu SettingMenu;
        private ViewEngine _engine = new();

        public Axone()
        {
            InitializeComponent();
            BS.BindCloseButton(this, BtnClose);
            BS.BindBackground(this, Pn_Background);
            BS.BindDraggable(this, PnSidebar);

            BtnClose.Font = new Font("Segoe MDL2 Assets", 14F);
            BtnClose.Text = "\uE106";

            SL.BindControl(RTBxLogs);

            SettingMenu = new ToggleMenu(PnSettingBox);
        }

        PipeServer? pipe;

        private void VGVEngine_Load(object sender, EventArgs e)
        {
            // ① 解析命令行参数中的 --pipe
            string[] args = Environment.GetCommandLineArgs();
            int idx = Array.IndexOf(args, "--pipe");
            if (idx >= 0 && idx + 1 < args.Length)
            {
                string pipeName = args[idx + 1];
                pipe = new PipeServer(pipeName);
                pipe.OnMessage += HandlePipeMessage;

                // ✅ 日志
                SL.SendLog("✅ Pipe server ready: " + pipeName);
            }

            CBxModel.SelectedIndex = 0;
            _engine.BindControl(GLViewMain);

            var interactor = new GLInteractor(_engine, GLViewMain);

            BtnOnColor.BindSlider(TBxOnColor);
        }

        private void HandlePipeMessage(string json)
        {
            // 🔄 根据 cmd 执行动作
            try
            {
                var doc = JsonDocument.Parse(json);
                string cmd = doc.RootElement.GetProperty("cmd").GetString()!;

                switch (cmd)
                {
                    case "Ping":
                        SL.SendLog("🔄 收到 Ping");
                        break;

                    default:
                        SL.SendLog("⚠️ 未识别指令: " + cmd);
                        break;
                }
            }
            catch (Exception ex)
            {
                SL.SendLog("❌解析失败: " + ex.Message);
            }
        }

        private async void btnOpen_Click(object sender, EventArgs e)
        {
            SimFild fild = new SimFild();
            var mat = FieldSeed.BuildCircleField(fild);
            var face = MassMorph.SimulateThreePointSag(mat, fild);
            var innerData = face.MatDoubleTo3F_Circular();

            face.GetFeatures().Dump();

            await _engine.BuildAsync(innerData);
        }

        void bar_Scroll(object sender, EventArgs e)
        {
            float coarse = TBarCoarse.Value * 10;          // 整数 0-10
            float fine = TBarFine.Value * 0.1f;    // 0-10.00
            _engine.ZMultiplier = coarse + fine;

            SL.SendCleanLog($"📦 ZMultiplier: {_engine.ZMultiplier:F2}");
        }

        [DllImport("kernel32")]
        static extern bool IsProcessorFeaturePresent(uint feature);

        const uint PF_AVX2_INSTRUCTIONS_AVAILABLE = 10;   // Win8 及以后
        private void BtnNone01_Click(object sender, EventArgs e)
        {
            Debug.WriteLine("AVX2 supported? " + IsProcessorFeaturePresent(PF_AVX2_INSTRUCTIONS_AVAILABLE));
        }

        private void btnMenuG01_Click(object sender, EventArgs e)
        {
            SettingMenu.Toggle();
        }
    }
}
