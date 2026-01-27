using Caliburn.Micro;
using RoiEditor.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace RoiEditor.Models
{
    public class ROIRegion : PropertyChangedBase
    {
        private bool _isSelected;
        private bool _isEditing;
        private List<Point> _points;
        private Color _color = Colors.Yellow;

        public int Id { get; set; }
        public string Name { get; set; }

        private ROIRegionType _type;

        public ROIRegionType Type
        {
            get { return _type; }
            set 
            { 
                _type = value;
                NotifyOfPropertyChange(() => Type);
            }
        }


        public Color Color
        {
            get => _color;
            set { _color = value; NotifyOfPropertyChange(() => Color); }
        }

        // 物理坐标集合
        public List<Point> Points
        {
            get => _points;
            set { _points = value; NotifyOfPropertyChange(() => Points); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set 
            { 
                _isSelected = value;
                if (!_isSelected) IsEditing = false;
                NotifyOfPropertyChange(() => IsSelected);
            }
        }

        public bool IsEditing
        {
            get { return _isEditing; }
            set 
            { 
                _isEditing = value;
                NotifyOfPropertyChange(() => IsEditing);
            }
        }


        // 辅助：获取中心点
        public Point Center
        {
            get
            {
                if (Points == null || Points.Count == 0) return new Point(0, 0);
                double x = 0, y = 0;
                foreach (var p in Points) { x += p.X; y += p.Y; }
                return new Point(x / Points.Count, y / Points.Count);
            }
        }
    }
}
