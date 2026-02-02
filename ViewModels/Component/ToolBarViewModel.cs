using Caliburn.Micro;
using RoiEditor.Enums;
using RoiEditor.Events;
using RoiEditor.Models;
using RoiEditor.Resources;
using System.Linq;
using System.Windows.Media;

namespace RoiEditor.ViewModels.Component
{

    public class ToolGroupViewModel:PropertyChangedBase
    {
        public string Header { get; set; }
        public BindableCollection<ToolItemViewModel> Items { get; set; }

        // 控制整组的显示/隐藏
        private bool _isVisible = true;
        public bool IsVisible
        {
            get { return _isVisible; }
            set { _isVisible = value; NotifyOfPropertyChange(() => IsVisible); }
        }
    }

    public class ToolBarViewModel : Screen,
        IHandle<ActiveROIChangedEvent>,
        IHandle<ROIOperationModeChangedEvent>
    {
        private readonly IEventAggregator _eventAggregator;
        private ROIOperationMode _currentOperationMode = ROIOperationMode.ROI_OS_Pan;
        private ROIDrawMode _currentDrawMode = ROIDrawMode.ROI_DS_Union;
        private ToolGroupViewModel _booleanGroup;

        private ROIOperationMode _lastActiveStateMode = ROIOperationMode.ROI_OS_Pan;

        public BindableCollection<ToolGroupViewModel> Groups { get; set; } = new BindableCollection<ToolGroupViewModel>();

        #region Prop
        public ROIOperationMode CurrentOperationMode
        {
            get => _currentOperationMode;
            set
            {
                if (_currentOperationMode == value) return;
                _currentOperationMode = value;
                NotifyOfPropertyChange(() => CurrentOperationMode);
                _eventAggregator.PublishOnUIThreadAsync(new ROIOperationModeChangedEvent(_currentOperationMode));
                UpdateBooleanGroupVisibility();
            }
        }
        #endregion

        public ToolBarViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            InitializeTools();
            UpdateToolVisualState(ROIOperationMode.ROI_OS_Pan);
        }

        private void InitializeTools()
        {
            // ================== Region 分组 ==================
            var regionItems = new BindableCollection<ToolItemViewModel>();
            // 1. Draw (下拉菜单)
            var drawTools = new SubToolItem[] {
                new SubToolItem { Name="Region_Draw_Rect", ToolType=ROIOperationMode.ROI_OS_ROI_Shape_Rectangle, IconData = Icons.Region_Draw_Rect},
                new SubToolItem { Name="Region_Draw_Ellipse", ToolType=ROIOperationMode.ROI_OS_ROI_Shape_Ellipse, IconData=Icons.Region_Draw_Ellipse},
                new SubToolItem { Name="Region_Draw_TwoPoint", ToolType=ROIOperationMode.ROI_OS_ROI_Shape_TwoPoint, IconData=Icons.Region_Draw_TwoPoint}
            };
            regionItems.Add(CreateTool("Region_Draw", ROIOperationMode.ROI_OS_ROI_Shape_Rectangle, drawTools[0].IconData.ToString(), false, drawTools));
            // 2. Pen (画笔) - 补全了完整路径
            regionItems.Add(CreateTool("Region_Draw_Pen", ROIOperationMode.ROI_OS_ROI_Pen, Icons.Region_Draw_Pen,
                showSeparator:true));
            //3.AutoRegion
            regionItems.Add(CreateTool("Region_Alg_Auto",ROIOperationMode.ROI_OS_AutoRegion,Icons.Region_Alg_Auto));
            //4.FindRegion
            regionItems.Add(CreateTool("Region_Alg_Find", ROIOperationMode.ROI_OS_FindRegion,Icons.Region_Alg_Find,showSeparator:true));
            // 5. Delete (删除) - 补全了完整路径
            regionItems.Add(CreateTool("Region_Draw_Delete", ROIOperationMode.ROI_OS_Delete_Range, Icons.Region_Draw_Delete, 
                showSeparator:false));
            Groups.Add(new ToolGroupViewModel { Header = "REGION", Items = regionItems });

            // ================== Operation 分组 ==================
            var opItems = new BindableCollection<ToolItemViewModel>();
            // 1. Pan (平移/手掌) - 补全了完整路径
            var panTool = CreateTool("Operation_Pan", ROIOperationMode.ROI_OS_Pan, Icons.Operation_Pan, false);
            opItems.Add(panTool);
            // 2. Select (选择箭头) - 补全了完整路径
            opItems.Add(CreateTool("Operation_Select", ROIOperationMode.ROI_OS_Select, Icons.Operation_Select, false));
            // 3. Zoom (下拉菜单)
            var zoomTools = new SubToolItem[] {
                new SubToolItem { Name="Operation_Zoom_In", ToolType=ROIOperationMode.ROI_OS_Zoom_In, IconData = Icons.Operation_Zoom_In},
                new SubToolItem { Name="Operation_Zoom_Out", ToolType=ROIOperationMode.ROI_OS_Zoom_Out, IconData=Icons.Operation_Zoom_Out},
                new SubToolItem { Name="Operation_Zoom_Resume", ToolType=ROIOperationMode.ROI_OS_Zoom_Resume, IconData=Icons.Operation_Zoom_Resume}
            };
            opItems.Add(CreateTool("Operation_Zoom", ROIOperationMode.ROI_OS_Zoom_In, zoomTools[0].IconData.ToString(), true, zoomTools));
            //4.Undo
            opItems.Add(CreateTool("Operation_Undo", ROIOperationMode.ROI_OS_Undo, Icons.Operation_Undo,true));
            Groups.Add(new ToolGroupViewModel { Header = "OPERATION", Items = opItems });

            // ================== ROIS 分组 ==================
            var roisItems = new BindableCollection<ToolItemViewModel>();
            //1.SplitROI
            roisItems.Add(CreateTool("ROIS_SplitROI", ROIOperationMode.ROI_OS_Zoom_Out, Icons.Operation_SplitROI, true));
            //2.ClearROIS
            roisItems.Add(CreateTool("ROIS_SplitROI", ROIOperationMode.ROI_OS_ClearROIS, Icons.Operation_ClearROIS, true));
            //3.SaveROIS
            roisItems.Add(CreateTool("ROIS_SaveROIS", ROIOperationMode.ROI_OS_SaveROIS, Icons.Operation_SaveROIS, true));
            Groups.Add(new ToolGroupViewModel { Header = "ROIS", Items = roisItems });

            // ================== UNION/EXCLUDE 分组 ==================
            var boolItems = new BindableCollection<ToolItemViewModel>();
            var unionTool = new ToolItemViewModel("Union", ROIOperationMode.ROI_OS_None, Icons.Boolean_Union);
            // 手动绑定点击命令 -> 切换 DrawMode
            unionTool.ClickCommand = new RelayCommand(_ => SetDrawMode(ROIDrawMode.ROI_DS_Union));
            boolItems.Add(unionTool);
            var excludeTool = new ToolItemViewModel("Exclude", ROIOperationMode.ROI_OS_None, Icons.Boolean_Exclude);
            excludeTool.ClickCommand = new RelayCommand(_ => SetDrawMode(ROIDrawMode.ROI_DS_Exclude));
            boolItems.Add(excludeTool);
            _booleanGroup = new ToolGroupViewModel { Header = "BOOLEAN", Items = boolItems, IsVisible = false }; // 默认隐藏
            Groups.Add(_booleanGroup);
            // 初始化高亮状态
            UpdateDrawModeHighlight();

        }

        // 4. 关键辅助方法：创建并建立父子关系
        private ToolItemViewModel CreateTool(string name, ROIOperationMode type, string icon, bool isActionOnly = false, SubToolItem[] subs = null,bool showSeparator = false)
        {
            var tool = new ToolItemViewModel(name, type, icon, isActionOnly, subs);
            tool.Parent = this;
            tool.IsSeparatorVisible = showSeparator;
            if(tool.SubTools == null || tool.SubTools.Count == 0)
            {
                 tool.ClickCommand = new RelayCommand(_ => HandleToolSelection(tool));
            }
            return tool;
        }

        public void HandleToolSelection(ToolItemViewModel selected)
        {
            if (selected == null) return;

            // 逻辑处理：如果是纯动作（如删除），执行特定逻辑；否则切换模式
            if (selected.IsActionOnly)
            {
                /// 1. 【发射信号】强制通知 Canvas 执行动作
                CurrentOperationMode = selected.ToolType;

                // 2. 【状态回弹】马上把 ViewModel 的状态切回刚才的工具
                CurrentOperationMode = _lastActiveStateMode;
            }
            else
            {
                // 1. 记录这个模式，方便下次“回弹”回来
                _lastActiveStateMode = selected.ToolType;

                // 2. 通知 Canvas 切换
                CurrentOperationMode = selected.ToolType;

                //3.互斥高亮
                UpdateToolVisualState(selected.ToolType);
            }
        }

        private void UpdateToolVisualState(ROIOperationMode mode)
        {
            foreach (var group in Groups)
            {
                if (group == _booleanGroup) continue;

                foreach (var item in group.Items)
                {
                    // 1. 检查主工具类型是否匹配
                    bool isMatch = (item.ToolType == mode);

                    // 2. 如果主工具不匹配，检查一下子工具 (防止模式是 "Zoom_Out" 但按钮还显示 "Zoom_In")
                    // 如果系统强行切换到一个隐藏在下拉菜单里的模式，我们需要把父按钮更新并点亮
                    if (!isMatch && item.SubTools != null)
                    {
                        var matchSub = item.SubTools.FirstOrDefault(s => s.ToolType == mode);
                        if (matchSub != null)
                        {
                            // 找到了！更新父按钮的外观
                            item.Name = matchSub.Name;
                            item.IconData = matchSub.IconData;
                            item.ToolType = matchSub.ToolType;
                            isMatch = true;
                        }
                    }

                    item.IsActive = isMatch;
                }
            }
        }

        // ===========================================
        // 核心逻辑 2：布尔组显隐控制
        // ===========================================
        private void UpdateBooleanGroupVisibility()
        {
            if (_booleanGroup == null) return;

            // 定义哪些模式下需要显示布尔组
            bool shouldShow =
                _currentOperationMode == ROIOperationMode.ROI_OS_ROI_Shape_Rectangle ||
                _currentOperationMode == ROIOperationMode.ROI_OS_ROI_Shape_Ellipse ||
                _currentOperationMode == ROIOperationMode.ROI_OS_ROI_Shape_TwoPoint ||
                _currentOperationMode == ROIOperationMode.ROI_OS_ROI_Pen;

            _booleanGroup.IsVisible = shouldShow;
        }

        // ===========================================
        // 核心逻辑 3：布尔模式切换 (独立状态)
        // ===========================================
        public void SetDrawMode(ROIDrawMode mode)
        {
            _currentDrawMode = mode;
            // 发送事件 (如果需要)
            _eventAggregator.PublishOnUIThreadAsync(new ROIDrawModeChangedEvent(_currentDrawMode));

            UpdateDrawModeHighlight();
        }

        private void UpdateDrawModeHighlight()
        {
            // 专门处理布尔组的高亮
            if (_booleanGroup == null) return;

            // 假设第一个是 Union，第二个是 Exclude (根据 InitializeTools 的添加顺序)
            // 更严谨的做法是判断 ToolItem 的 Tag 或 Name，这里简化处理
            _booleanGroup.Items[0].IsActive = (_currentDrawMode == ROIDrawMode.ROI_DS_Union);
            _booleanGroup.Items[1].IsActive = (_currentDrawMode == ROIDrawMode.ROI_DS_Exclude);
        }

        #region Handle

        // 处理事件：当 ROI 选中状态改变时
        public void Handle(ActiveROIChangedEvent message)
        {
            UpdateToolStates(message.ActiveROI);
        }
        // 处理模式变更事件 (解决删除 ROI 后 UI 不同步的问题)
        public void Handle(ROIOperationModeChangedEvent message)
        {
            // 防止死循环 (如果是自己发出的，就不处理，或者处理也没关系，因为值一样)
            if (_currentOperationMode == message.CurrentOperationMode) return;

            // 1. 更新内部数据 (不触发 Setter 里的 Publish，防止循环广播)
            _currentOperationMode = message.CurrentOperationMode;
            NotifyOfPropertyChange(() => CurrentOperationMode);

            // 2. 记得同步更新 Boolean 组的显隐
            UpdateBooleanGroupVisibility();

            // 3. 【关键】同步更新 UI 高亮
            UpdateToolVisualState(_currentOperationMode);

            // 4. 更新记录的状态
            if (!IsActionOnlyMode(_currentOperationMode))
            {
                _lastActiveStateMode = _currentOperationMode;
            }
        }

        // 辅助判断是否是瞬时动作
        private bool IsActionOnlyMode(ROIOperationMode mode)
        {
            return mode == ROIOperationMode.ROI_OS_Delete_Range ||
                   mode == ROIOperationMode.ROI_OS_Zoom_In ||
                   mode == ROIOperationMode.ROI_OS_Zoom_Out ||
                   mode == ROIOperationMode.ROI_OS_Zoom_Resume ||
                   mode == ROIOperationMode.ROI_OS_Undo;
        }

        // 【核心逻辑】根据是否有 ActiveROI 更新按钮可用性
        private void UpdateToolStates(ROI activeRoi)
        {
            // 只有当 activeRoi 不为空时，允许绘图
            bool canDraw = (activeRoi != null);

            foreach (var group in Groups)
            {
                foreach (var tool in group.Items)
                {
                    // 判断该工具是否是“创建类”工具
                    if (IsCreationTool(tool.ToolType))
                    {
                        tool.IsEnabled = canDraw;
                    }
                    else
                    {
                        // 像 Pan(平移), Zoom(缩放), Select(选择) 这种工具，永远可用
                        tool.IsEnabled = true;
                    }

                    // 处理下拉菜单里的工具
                    if (tool.SubTools != null)
                    {
                        foreach (var sub in tool.SubTools)
                        {
                            if (IsCreationTool(sub.ToolType))
                                sub.IsEnabled = canDraw;
                        }
                    }
                }
            }
        }

        // 辅助方法：定义哪些是需要 ROI 才能用的工具
        private bool IsCreationTool(ROIOperationMode mode)
        {
            return mode == ROIOperationMode.ROI_OS_ROI_Shape_Rectangle
                || mode == ROIOperationMode.ROI_OS_ROI_Shape_Ellipse
                || mode == ROIOperationMode.ROI_OS_ROI_Shape_TwoPoint
                || mode == ROIOperationMode.ROI_OS_ROI_Pen
                || mode == ROIOperationMode.ROI_OS_AutoRegion
                || mode == ROIOperationMode.ROI_OS_FindRegion
                || mode == ROIOperationMode.ROI_OS_Delete_Range
                || mode == ROIOperationMode.ROI_OS_Select
                || mode == ROIOperationMode.ROI_OS_MoveDrag
                || mode == ROIOperationMode.ROI_OS_MoveShape
                || mode == ROIOperationMode.ROI_OS_MultiMove;
        }

        #endregion

        protected override void OnActivate()
        {
            base.OnActivate();
            _eventAggregator.Subscribe(this);
        }

        protected override void OnDeactivate(bool close)
        {
            base.OnDeactivate(close);
            _eventAggregator.Unsubscribe(this);
        }
    }
}