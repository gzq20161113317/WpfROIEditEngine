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

        public ShellViewModel(IEventAggregator events)
        {
            RoiEditorView = new RoiMainViewModel(events);
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
            // 使用 WinForms 的文件夹选择框
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select Wafer Map Folder";
                dialog.ShowNewFolderButton = false;

                // WinForms 的 ShowDialog 返回 DialogResult.OK
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    string selectedPath = dialog.SelectedPath;

                    StatusMessage = $"Loading: {selectedPath}";

                    // 赋值给 Lib 的 VM，触发加载
                    RoiEditorView.LoadMap(selectedPath);

                    StatusMessage = $"Ready - {selectedPath}";
                }
            }
        }

        public void Exit()
        {
            TryClose();
        }
    }
}
