using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoiEditor.Core.Memory
{
    /// <summary>
    /// 线程安全的高性能LRU缓存
    /// TKey:瓦片路径或Key，TValue：BitmapSource
    /// 优化：使用节点缓存，Remove操作从O(n)优化到O(1)
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class LruCache<TKey,TValue>
    {
        private readonly int _capacity;
        private readonly Dictionary<TKey, LinkedListNode<TKey>> _nodeMap; // 缓存节点引用，实现O(1)删除
        private readonly Dictionary<TKey, TValue> _valueMap; // 存储实际值
        private readonly LinkedList<TKey> _lruList;// 维护访问顺序，头部最近使用，尾部最久未使用
        private readonly object _lock = new object();

        public LruCache(int capacity)
        {
            _capacity = capacity;
            _nodeMap = new Dictionary<TKey, LinkedListNode<TKey>>(capacity);
            _valueMap = new Dictionary<TKey, TValue>(capacity);
            _lruList = new LinkedList<TKey>();
        }

        public bool TryGet(TKey key,out TValue value)
        {
            lock(_lock)
            {
                if(_nodeMap.TryGetValue(key, out var node))
                {
                    //命中！将其移到链表头部 (最近使用) - O(1)操作
                    _lruList.Remove(node);
                    _lruList.AddFirst(node);
                    value = _valueMap[key];
                    return true;
                }
            }
            value = default;
            return false;
        }

        public void Add(TKey key,TValue value)
        {
            lock(_lock)
            {
                if(_nodeMap.ContainsKey(key))
                {
                    //如果已存在，更新值并移到头部
                    var node = _nodeMap[key];
                    _lruList.Remove(node);
                    _lruList.AddFirst(node);
                    _valueMap[key] = value;
                    return;
                }

                //如果满了，移除尾部(最久未使用)
                if(_nodeMap.Count >= _capacity)
                {
                    var lastNode = _lruList.Last;
                    var lastKey = lastNode.Value;

                    _nodeMap.Remove(lastKey);
                    _valueMap.Remove(lastKey);
                    _lruList.RemoveLast();
                }

                //添加新项到头部
                var newNode = _lruList.AddFirst(key);
                _nodeMap.Add(key, newNode);
                _valueMap.Add(key, value);
            }
        }

        //清空缓存(ReloadMap时可能需要)
        public void Clear()
        {
            lock(_lock)
            {
                _nodeMap.Clear();
                _valueMap.Clear();
                _lruList.Clear();
            }
        }
    }
}
