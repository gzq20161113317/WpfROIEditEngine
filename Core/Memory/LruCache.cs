using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoiEditor.Core.Memory
{
    /// <summary>
    /// 简单的线程安全LRU缓存
    /// TKey:瓦片路径或Key，TValue：BitmapSource
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class LruCache<TKey,TValue>
    {
        private readonly int _capacity;
        private readonly Dictionary<TKey, TValue> _cacheMap;
        private readonly LinkedList<TKey> _lruList;// 维护访问顺序，头部最近使用，尾部最久未使用
        private readonly object _lock = new object();

        public LruCache(int capacity)
        {
            _capacity = capacity;
            _cacheMap = new Dictionary<TKey, TValue>(capacity);
            _lruList = new LinkedList<TKey>();
        }

        public bool TryGet(TKey key,out TValue value)
        {
            lock(_lock)
            {
                if(_cacheMap.TryGetValue(key,out value))
                {
                    //命中！将其移到链表头部 (最近使用)
                    _lruList.Remove(key);
                    _lruList.AddFirst(key);
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
                if(_cacheMap.ContainsKey(key))
                {
                    //如果已存在，更新值并移到头部
                    _lruList.Remove(key);
                    _lruList.AddFirst(key);
                    _cacheMap[key] = value;
                    return;
                }

                //如果满了，移除尾部(最久未使用)
                if(_cacheMap.Count >= _capacity)
                {
                    var lastKey = _lruList.Last.Value;
                    _cacheMap.Remove(lastKey);
                    _lruList.RemoveLast();
                }

                //添加新项到头部
                _lruList.AddFirst(key);
                _cacheMap.Add(key,value);
            }
        }

        //清空缓存(ReloadMap时可能需要)
        public void Clear()
        {
            lock(_lock)
            {
                _cacheMap.Clear();
                _lruList.Clear();
            }
        }
    }
}
