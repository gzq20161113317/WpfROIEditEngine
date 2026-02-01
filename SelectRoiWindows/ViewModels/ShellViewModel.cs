using Caliburn.Micro;
using RoiEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;

namespace SelectRoiWindows.ViewModels
{
    public class ShellViewModel : Conductor<object>
    {
        public RoiMainViewModel RoiEditorView { get; set; }
        private readonly IEventAggregator _events;
        private string _statusMessage = "Ready";

        public ShellViewModel(IEventAggregator events,IWindowManager windowManager)
        {
            RoiEditorView = new RoiMainViewModel(events, windowManager);
            _events = events;
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                NotifyOfPropertyChange(() => StatusMessage);
            }
        }

        // === CM 3.x 生命周期 (同步) ===
        protected override void OnInitialize()
        {
            base.OnInitialize();
            DisplayName = "Wafer Inspection Workstation (.NET 4.7.2)";
        }

        // === 动作 ===
        public void LoadMap()
        {
            string selectedPath = null;

            // 1. 询问用户意图：是加载单张大图，还是瓦片文件夹？
            // (这是原生 WPF/WinForms 下最稳健的“二选一”方案)
            var result = System.Windows.MessageBox.Show(
                "是否加载单张图片文件？\n\n[是] - 选择图片文件 (单图模式)\n[否] - 选择瓦片文件夹 (瓦片模式)\n[取消] - 返回",
                "选择地图类型",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel) return;

            if (result == MessageBoxResult.Yes)
            {
                // === 模式 A: 选择单张文件 (OpenFileDialog) ===
                // 使用 WPF 原生的 OpenFileDialog (Microsoft.Win32)
                var dialog = new Microsoft.Win32.OpenFileDialog();
                dialog.Title = "Select Wafer Map Image";
                dialog.Filter = "Image Files|*.bmp;*.jpg;*.jpeg;*.png;*.tif;*.tiff|All Files|*.*";
                dialog.CheckFileExists = true;

                if (dialog.ShowDialog() == true)
                {
                    selectedPath = dialog.FileName;
                }
            }
            else
            {
                // === 模式 B: 选择文件夹 (FolderBrowserDialog) ===
                // 继续使用 WinForms 的文件夹选择框
                using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
                {
                    dialog.Description = "Select Wafer Map Folder (Tile Level Folder)";
                    dialog.ShowNewFolderButton = false;

                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        selectedPath = dialog.SelectedPath;
                    }
                }
            }

            // 2. 统一触发加载逻辑
            // 只要路径有效，MapService.LoadMap 会自动根据是文件还是文件夹来切换模式
            if (!string.IsNullOrEmpty(selectedPath))
            {
                StatusMessage = $"Loading: {selectedPath}";

                // 核心加载调用
                RoiEditorView.LoadMap(selectedPath);

                StatusMessage = $"Ready - {selectedPath}";
            }
        }

        public void Exit()
        {
            TryClose();
        }
    }
}
