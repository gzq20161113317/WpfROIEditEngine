using RoiEditor.Core.Attributes;
using RoiEditor.Enums;
using RoiEditor.Models;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RoiEditor.Core.Interaction
{
    [RoiTool(ROIOperationMode.ROI_OS_ROI_Pen)]
    public class PenTool : ToolBase
    {
        #region Fields
        private ROIRegion _newItem; // 当前正在绘制的多边形
        private List<Point> _points; // 临时点集合
        private bool _isDrawing; // 是否正在绘制中
        private const double MIN_POINT_DISTANCE = 5.0; // 最小点间距（屏幕像素）
        private const double DOUBLE_CLICK_THRESHOLD = 10.0; // 双击判定距离
        private Point _lastClickPoint; // 上一次点击的位置
        private DateTime _lastClickTime; // 上一次点击的时间
        #endregion

        public PenTool(Controls.RoiEditorCanvas canvas) : base(canvas) { }

        #region Cursor & UI
        public override UIElement GetCustomCursorView() => CreatePenCursorUI();

        private Grid CreatePenCursorUI()
        {
            var grid = new Grid { Margin = new Thickness(-8, -8, 0, 0) };

            // 钢笔光标：小圆点 + 十字
            var pathDataStr = "M8,0 L8,16 M0,8 L16,8 M8,8 m-2,0 a2,2 0 1,0 4,0 a2,2 0 1,0 -4,0";
            var geometry = Geometry.Parse(pathDataStr);
            if (geometry.CanFreeze) geometry.Freeze();

            var path = new Path
            {
                Data = geometry,
                Stroke = Brushes.White,
                StrokeThickness = 1.5,
                SnapsToDevicePixels = true,
                IsHitTestVisible = false
            };

            grid.Children.Add(path);
            return grid;
        }
        #endregion

        #region Mouse Interactions
        public override void OnMouseDown(MouseButtonEventArgs e)
        {
            // 右键完成绘制
            if (e.RightButton == MouseButtonState.Pressed)
            {
                if (_isDrawing)
                {
                    FinishDrawing();
                }
                return;
            }

            if (e.LeftButton != MouseButtonState.Pressed) return;

            // 校验环境
            if (_canvas.ActiveROI == null)
            {
                MessageBox.Show("Please select or create an ROI first.", "Warning");
                return;
            }

            var wPos = GetWorldPosition(e);

            // 校验有效区
            if (!_canvas.ValidRegion.IsEmpty && !_canvas.ValidRegion.Contains(wPos))
                return;

            // 检测双击（完成绘制）
            if (_isDrawing && IsDoubleClick(wPos))
            {
                FinishDrawing();
                return;
            }

            // 记录点击信息
            _lastClickPoint = wPos;
            _lastClickTime = DateTime.Now;

            // 如果还没开始绘制，初始化
            if (!_isDrawing)
            {
                StartDrawing(wPos);
            }
            else
            {
                // 添加新点
                AddPoint(wPos);
            }
        }

        public override void OnMouseMove(MouseEventArgs e)
        {
            if (!_isDrawing || _newItem == null) return;

            var wPos = GetWorldPosition(e);
            wPos = _canvas.ClampToValidRegion(wPos);

            // 更新最后一个点（预览位置）
            if (_points.Count > 0)
            {
                _newItem.Points[_newItem.Points.Count - 1] = wPos;
                _newItem.NotifyOfPropertyChange("Points");
                _canvas.RedrawEditorLayer();
            }
        }

        public override void OnMouseUp(MouseButtonEventArgs e)
        {
            // Pen 工具主要使用点击，不需要处理 MouseUp
        }

        public override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // ESC 取消绘制
            if (e.Key == Key.Escape && _isDrawing)
            {
                CancelDrawing();
            }
            // Enter 完成绘制
            else if (e.Key == Key.Enter && _isDrawing)
            {
                FinishDrawing();
            }
        }
        #endregion

        #region Drawing Logic
        private void StartDrawing(Point startPoint)
        {
            _points = new List<Point> { startPoint, startPoint }; // 第一个点 + 预览点

            _newItem = new ROIRegion
            {
                Id = Guid.NewGuid(),
                Name = $"Polygon {_canvas.ActiveROI.Regions.Count + 1}",
                Type = ROIRegionType.Polygon,
                Points = new List<Point>(_points),
                IsSelected = true,
                Parent = _canvas.ActiveROI
            };

            _isDrawing = true;

            try
            {
                _canvas.ActiveROI.Regions.Add(_newItem);
                _canvas.SelectROIRegion(_newItem);
            }
            catch (Exception ex)
            {
                _isDrawing = false;
                _newItem = null;
                _points = null;
                MessageBox.Show($"Error creating region: {ex.Message}");
            }
        }

        private void AddPoint(Point point)
        {
            if (_points == null || _newItem == null) return;

            // 检查与上一个点的距离（避免点太密集）
            if (_points.Count > 1)
            {
                Point lastPoint = _points[_points.Count - 2]; // 倒数第二个点（最后一个是预览点）
                Vector diff = point - lastPoint;

                // 转换为屏幕坐标计算距离
                Point screenLast = _canvas.WorldToScreen(lastPoint);
                Point screenCurrent = _canvas.WorldToScreen(point);
                double screenDistance = (screenCurrent - screenLast).Length;

                if (screenDistance < MIN_POINT_DISTANCE)
                {
                    return; // 距离太近，忽略
                }
            }

            // 插入新点（在预览点之前）
            _points.Insert(_points.Count - 1, point);
            _newItem.Points = new List<Point>(_points);
            _newItem.NotifyOfPropertyChange("Points");
            _canvas.RedrawEditorLayer();
        }

        private void FinishDrawing()
        {
            if (!_isDrawing || _newItem == null || _points == null) return;

            // 移除预览点
            if (_points.Count > 1)
            {
                _points.RemoveAt(_points.Count - 1);
            }

            // 至少需要 3 个点才能形成多边形
            if (_points.Count < 3)
            {
                MessageBox.Show("A polygon requires at least 3 points.", "Invalid Polygon", MessageBoxButton.OK, MessageBoxImage.Warning);
                CancelDrawing();
                return;
            }

            // 更新最终点集
            _newItem.Points = new List<Point>(_points);
            _newItem.NotifyOfPropertyChange("Points");

            // 重建空间索引
            _canvas.RebuildSpatialIndex();
            _canvas.RedrawEditorLayer();

            // 清理状态
            _isDrawing = false;
            _newItem = null;
            _points = null;
        }

        private void CancelDrawing()
        {
            if (!_isDrawing || _newItem == null) return;

            // 从 ROI 中移除
            if (_canvas.ActiveROI != null)
            {
                _canvas.ActiveROI.Regions.Remove(_newItem);
            }

            _canvas.SelectROIRegion(null);
            _canvas.RedrawEditorLayer();

            // 清理状态
            _isDrawing = false;
            _newItem = null;
            _points = null;
        }

        private bool IsDoubleClick(Point currentPoint)
        {
            if (_lastClickTime == default) return false;

            // 检查时间间隔（300ms 内）
            TimeSpan timeDiff = DateTime.Now - _lastClickTime;
            if (timeDiff.TotalMilliseconds > 300) return false;

            // 检查距离（屏幕坐标）
            Point screenLast = _canvas.WorldToScreen(_lastClickPoint);
            Point screenCurrent = _canvas.WorldToScreen(currentPoint);
            double distance = (screenCurrent - screenLast).Length;

            return distance < DOUBLE_CLICK_THRESHOLD;
        }
        #endregion

        protected override void OnDeactivated()
        {
            base.OnDeactivated();
            // 切换工具时，如果正在绘制，自动完成
            if (_isDrawing)
            {
                FinishDrawing();
            }
        }
    }
}
