using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace RoiEditor.Models
{
    public class ROI:PropertyChangedBase
    {
        private string _name;
        private Color _color;
        private bool _isSelected;
        private bool _isVisible = true;
        private ObservableCollection<ROIRegion> _regions;
        public string Name
        {
            get => _name;
            set { _name = value; NotifyOfPropertyChange(() => Name); }
        }

        public Color Color
        {
            get => _color;
            set
            {
                _color = value;
                NotifyOfPropertyChange(() => Color);
                // 颜色变更同步给子元素
                foreach (var region in Regions) region.UpdateColorFromParent();
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; NotifyOfPropertyChange(() => IsSelected); }
        }

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible != value)
                {
                    _isVisible = value;
                    NotifyOfPropertyChange(() => IsVisible);
                    // 这里未来可以触发 Canvas 重绘或隐藏逻辑
                }
            }
        }

        public ObservableCollection<ROIRegion> Regions
        {
            get => _regions;
            set 
            {
                if (_regions == value) return;
                // A. 解绑旧列表的事件（防止内存泄漏）
                if (_regions != null)
                {
                    _regions.CollectionChanged -= Regions_CollectionChanged;
                }

                _regions = value;

                // B. 绑定新列表的事件
                if (_regions != null)
                {
                    _regions.CollectionChanged += Regions_CollectionChanged;
                }

                NotifyOfPropertyChange(() => Regions);
                // C. 列表整体替换时，立即更新数量
                NotifyOfPropertyChange(() => RegionCount);
            }
        }

        public int RegionCount => Regions?.Count ?? 0;

        public ROI()
        {
            Regions = new ObservableCollection<ROIRegion>();
        }

        /// <summary>
        /// ROI的Regions列表的1号委托
        /// 用于确认Region的归属ROI，以及给Region的Color赋值
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Regions_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (ROIRegion item in e.NewItems)
                {
                    item.Parent = this;
                    item.UpdateColorFromParent();
                }

            if (e.OldItems != null)
                foreach (ROIRegion item in e.OldItems) item.Parent = null;

            NotifyOfPropertyChange(() => RegionCount);
        }
    }
}
