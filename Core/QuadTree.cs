using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace RoiEditor.Core
{
    /// <summary>
    /// 高性能四叉树实现 (专为 WPF 命中测试优化)
    /// </summary>
    /// <typeparam name="T">存储的对象类型 (如 RoiItem)</typeparam>
    public class QuadTree<T>
    {
        private readonly Rect _bounds; // 当前节点的空间范围
        private readonly int _maxObjects; // 节点分裂阈值
        private readonly int _maxLevels; // 最大深度
        private readonly int _level; // 当前深度

        private List<T> _objects; // 当前节点存储的物体
        private List<Rect> _objectRects; // 缓存物体的 Rect，避免重复计算
        private QuadTree<T>[] _nodes; // 子节点 (NW, NE, SW, SE)

        // 委托：用于从泛型 T 中获取包围盒，避免 T 必须继承特定接口
        private readonly Func<T, Rect> _getBounds;

        public QuadTree(Rect bounds, Func<T, Rect> getBounds, int maxObjects = 10, int maxLevels = 5, int level = 0)
        {
            _bounds = bounds;
            _getBounds = getBounds;
            _maxObjects = maxObjects;
            _maxLevels = maxLevels;
            _level = level;
            _objects = new List<T>();
            _objectRects = new List<Rect>();
        }

        /// <summary>
        /// 清空树
        /// </summary>
        public void Clear()
        {
            _objects.Clear();
            _objectRects.Clear();

            if (_nodes != null)
            {
                for (int i = 0; i < _nodes.Length; i++)
                {
                    _nodes[i]?.Clear();
                    _nodes[i] = null;
                }
                _nodes = null;
            }
        }

        /// <summary>
        /// 插入物体
        /// </summary>
        public void Insert(T item)
        {
            Rect itemRect = _getBounds(item);

            // 如果有子节点，尝试插入子节点
            if (_nodes != null)
            {
                int index = GetIndex(itemRect);
                if (index != -1)
                {
                    _nodes[index].Insert(item);
                    return;
                }
            }

            // 否则存入当前节点
            _objects.Add(item);
            _objectRects.Add(itemRect);

            // 检查是否需要分裂
            if (_objects.Count > _maxObjects && _level < _maxLevels)
            {
                if (_nodes == null)
                    Split();

                int i = 0;
                while (i < _objects.Count)
                {
                    int index = GetIndex(_objectRects[i]);
                    if (index != -1)
                    {
                        T obj = _objects[i];
                        _objects.RemoveAt(i);
                        _objectRects.RemoveAt(i);
                        _nodes[index].Insert(obj);
                    }
                    else
                    {
                        i++;
                    }
                }
            }
        }

        /// <summary>
        /// 查询某个点附近的物体
        /// </summary>
        /// <param name="p">鼠标世界坐标</param>
        /// <param name="returnList">结果列表</param>
        public void Query(Point p, List<T> returnList)
        {
            // 如果点不在本节点范围内，直接退出（剪枝）
            if (!_bounds.Contains(p)) return;

            // 如果有子节点，递归查询
            if (_nodes != null)
            {
                int index = GetIndex(p);
                if (index != -1)
                {
                    _nodes[index].Query(p, returnList);
                }
            }

            // 添加本节点的物体（因为它们跨越了边界或位于叶子节点）
            // 这里可以做一个简单的 Bounds 过滤，进一步提升性能
            for (int i = 0; i < _objects.Count; i++)
            {
                if (_objectRects[i].Contains(p))
                {
                    returnList.Add(_objects[i]);
                }
            }
        }

        // 分裂为 4 个子节点
        private void Split()
        {
            double subWidth = _bounds.Width / 2;
            double subHeight = _bounds.Height / 2;
            double x = _bounds.X;
            double y = _bounds.Y;

            _nodes = new QuadTree<T>[4];
            _nodes[0] = new QuadTree<T>(new Rect(x + subWidth, y, subWidth, subHeight), _getBounds, _maxObjects, _maxLevels, _level + 1); // NE
            _nodes[1] = new QuadTree<T>(new Rect(x, y, subWidth, subHeight), _getBounds, _maxObjects, _maxLevels, _level + 1); // NW
            _nodes[2] = new QuadTree<T>(new Rect(x, y + subHeight, subWidth, subHeight), _getBounds, _maxObjects, _maxLevels, _level + 1); // SW
            _nodes[3] = new QuadTree<T>(new Rect(x + subWidth, y + subHeight, subWidth, subHeight), _getBounds, _maxObjects, _maxLevels, _level + 1); // SE
        }

        // 获取物体属于哪个象限 (-1 表示跨越多个象限，需存父节点)
        private int GetIndex(Rect pRect)
        {
            double midX = _bounds.X + (_bounds.Width / 2);
            double midY = _bounds.Y + (_bounds.Height / 2);

            bool top = (pRect.Y < midY && pRect.Y + pRect.Height < midY);
            bool bottom = (pRect.Y > midY);
            bool left = (pRect.X < midX && pRect.X + pRect.Width < midX);
            bool right = (pRect.X > midX);

            if (left)
            {
                if (top) return 1; // NW
                if (bottom) return 2; // SW
            }
            else if (right)
            {
                if (top) return 0; // NE
                if (bottom) return 3; // SE
            }

            return -1; // 跨越边界
        }

        // 获取点属于哪个象限
        private int GetIndex(Point p)
        {
            double midX = _bounds.X + (_bounds.Width / 2);
            double midY = _bounds.Y + (_bounds.Height / 2);

            bool top = (p.Y < midY);
            bool bottom = (p.Y >= midY);
            bool left = (p.X < midX);
            bool right = (p.X >= midX);

            if (left)
            {
                if (top) return 1;
                if (bottom) return 2;
            }
            else if (right)
            {
                if (top) return 0;
                if (bottom) return 3;
            }
            return -1;
        }
    }
}
