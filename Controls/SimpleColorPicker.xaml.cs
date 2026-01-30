using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RoiEditor.Controls
{
    public partial class SimpleColorPicker : UserControl
    {
        public SimpleColorPicker()
        {
            InitializeComponent();

            // 初始化默认颜色列表
            ColorList.ItemsSource = GenerateDefaultColors();

            // 监听 ListBox 选择变化
            ColorList.SelectionChanged += (s, e) =>
            {
                if (ColorList.SelectedItem is Color c)
                {
                    SelectedColor = c;
                }
            };
        }

        // 依赖属性：SelectedColor (双向绑定)
        public static readonly DependencyProperty SelectedColorProperty =
            DependencyProperty.Register(nameof(SelectedColor), typeof(Color), typeof(SimpleColorPicker),
                new FrameworkPropertyMetadata(Colors.Red, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedColorChanged));

        public Color SelectedColor
        {
            get => (Color)GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
        }

        // 当外部改变 SelectedColor 时，同步 ListBox 的选中项
        private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SimpleColorPicker picker && e.NewValue is Color newColor)
            {
                picker.ColorList.SelectedItem = newColor;
            }
        }

        // 生成一套好看的预设颜色
        private List<Color> GenerateDefaultColors()
        {
            return new List<Color>
            {
                // 经典色
                Colors.Red, Colors.Lime, Colors.Blue,
                Colors.Yellow, Colors.Cyan, Colors.Magenta,
                // 常用亮色
                Colors.Orange, Colors.Purple, Colors.Pink, Colors.Teal,
                Colors.Gold, Colors.DeepSkyBlue, Colors.SpringGreen, Colors.Violet,
                // 深色/工业风
                Colors.Brown, Colors.Olive, Colors.Navy, Colors.Maroon,
                Colors.Gray, Colors.White
            };
        }
    }
}