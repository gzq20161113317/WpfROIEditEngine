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
    public class RoiItem : PropertyChangedBase
    {
        private bool _isSelected;
        private List<Point> _points;
        private Color _color = Colors.Yellow;

        public int Id { get; set; }
        public string Name { get; set; }
        public RoiType Type { get; set; }

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
            set { _isSelected = value; NotifyOfPropertyChange(() => IsSelected); }
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
