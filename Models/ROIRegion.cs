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
        private Guid _id;
        private string _name;
        private ROIRegionType _type;
        private List<Point> _points;
        private Color _color = Colors.Yellow;
        private bool _isSelected;
        private bool _isEditing;
        private double _lineWidth = 1.0;//默认1px

        public ROI Parent { get; set; }

        public Guid Id
        {
            get => _id;
            set { _id = value; NotifyOfPropertyChange(() => Id); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; NotifyOfPropertyChange(() => Name); }
        }

        public ROIRegionType Type
        {
            get => _type;
            set { _type = value; NotifyOfPropertyChange(() => Type); }
        }

        public List<Point> Points
        {
            get => _points;
            set
            {
                _points = value;
                NotifyOfPropertyChange(() => Points);
                NotifyOfPropertyChange(() => Center);
            }
        }

        public Color Color
        {
            get => _color;
            set { _color = value; NotifyOfPropertyChange(() => Color); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; NotifyOfPropertyChange(() => IsSelected); }
        }

        public bool IsEditing
        {
            get => _isEditing;
            set { _isEditing = value; NotifyOfPropertyChange(() => IsEditing); }
        }

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

        //线宽属性
        public double LineWidth
        {
            get => _lineWidth;
            set { _lineWidth = value; NotifyOfPropertyChange(() => LineWidth);  }
        }



        public ROIRegion()
        {
            Id = Guid.NewGuid();
            Points = new List<Point>();
        }

        public void UpdateColorFromParent()
        {
            if (Parent != null) Color = Parent.Color;
        }
    }
}
