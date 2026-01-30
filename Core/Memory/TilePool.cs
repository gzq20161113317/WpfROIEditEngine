using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RoiEditor.Core.Memory
{
    /// <summary>
    /// TilePool (对象池组件)
    /// 职责：管理 Image 控件的复用，防止 UI 元素无限增殖导致性能崩溃。
    /// 自动维护控件在 Canvas 上的生命周期。
    /// </summary>
    public class TilePool
    {
        private const int MAX_POOL_SIZE = 500; // 最大控件数防线
        private readonly Stack<Image> _pool = new Stack<Image>();
        private readonly Canvas _targetCanvas; // 绑定的画布
        private int _totalCreated = 0;

        /// <summary>
        /// 初始化对象池
        /// </summary>
        /// <param name="targetCanvas">瓦片将要被添加到的目标画布</param>
        /// <param name="initialCount">预热数量 (提前创建好，避免运行时卡顿)</param>
        public TilePool(Canvas targetCanvas, int initialCount = 50)
        {
            _targetCanvas = targetCanvas;

            // 预热池子
            for (int i = 0; i < initialCount; i++)
            {
                var img = CreateNewTile();
                _targetCanvas.Children.Add(img);
                _pool.Push(img);
                _totalCreated++;
            }
        }

        /// <summary>
        /// 借出一个 Image 控件
        /// </summary>
        public Image Rent()
        {
            // 1. 如果池子里有存货，直接给
            if (_pool.Count > 0)
                return _pool.Pop();

            // 2. 如果池子空了，但还没到上限，造一个新的
            if (_totalCreated < MAX_POOL_SIZE)
            {
                var img = CreateNewTile();
                _targetCanvas.Children.Add(img);
                _totalCreated++;
                return img;
            }

            // 3. 达到上限，返回 null (调用者需要处理这种情况，通常是暂时不显示)
            return null;
        }

        /// <summary>
        /// 归还一个 Image 控件
        /// </summary>
        public void Return(Image img)
        {
            if (img == null) return;

            // 重置状态
            img.Source = null;
            img.Visibility = Visibility.Collapsed;

            // 实际上不用从 Canvas.Children 移除，只需要隐藏即可，性能更好

            _pool.Push(img);
        }

        // 工厂方法
        private Image CreateNewTile()
        {
            var img = new Image
            {
                Stretch = Stretch.Fill,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false // 瓦片不参与命中测试，提升性能
            };

            // 像素风渲染 (适合晶圆图，放大不模糊而是显示像素格)
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor);

            // 2. 关闭边缘抗锯齿 (Alias = 有锯齿/硬边)
            // 这告诉 GPU：边缘不要做半透明混合，要么是图，要么不是。
            RenderOptions.SetEdgeMode(img, EdgeMode.Aliased);

            return img;
        }

        /// <summary>
        /// 清空池子（通常不需要调用，除非整个控件销毁）
        /// </summary>
        public void Clear()
        {
            _pool.Clear();
            _targetCanvas.Children.Clear();
            _totalCreated = 0;
        }
    }
}