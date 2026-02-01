using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;

namespace RoiEditor.Core.IO
{
    /// <summary>
    /// MapService
    /// 约定：World Space = Level0 的像素坐标（最粗层像素）。
    /// Level 越大越粗（瓦片世界尺寸 = 512 * 2^level）。
    /// </summary>
    public class MapService
    {
        public const int TILE_SIZE = 512;

        private string _mapPath;
        private int _maxLevel;
        private bool _isSingleFileMode;//标记是否是单图模式
        private Size _worldSize;//世界尺寸
        
        //有效区域
        public Rect EffectiveRegion { get; private set; } = Rect.Empty;

        private struct LevelInfo
        {
            public int MaxRow;
            public int MaxCol;

            public LevelInfo(int maxRow, int maxCol)
            {
                MaxRow = maxRow;
                MaxCol = maxCol;
            }
        }

        // Level -> (MaxRow, MaxCol)
        private readonly Dictionary<int,LevelInfo> _levelInfoCache = new Dictionary<int, LevelInfo>();

        public int MaxLevel => _maxLevel;
        public string MapPath => _mapPath;
        public bool IsSingleFileMode => _isSingleFileMode;

        public void LoadMap(string path)
        {
            _mapPath = path;
            _levelInfoCache.Clear();
            _maxLevel = 0;
            _isSingleFileMode = false;
            _worldSize = new Size(0, 0);
            EffectiveRegion = Rect.Empty; // 重置有效区

            if (string.IsNullOrEmpty(path))
                return;

            if(File.Exists(path))
            {
                // ===单图模式===
                _isSingleFileMode = true;
                try
                {
                    // 只读头信息获取尺寸，不加载像素，速度极快
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
                    {
                        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                        if (decoder.Frames.Count > 0)
                        {
                            var frame = decoder.Frames[0];
                            _worldSize = new Size(frame.PixelWidth, frame.PixelHeight);

                            // 默认有效区 = 整张图
                            EffectiveRegion = new Rect(0, 0, _worldSize.Width, _worldSize.Height);
                        }
                    }
                }
                catch { _worldSize = new Size(0, 0); }
            }
            // 2. 切片文件夹模式
            else if (Directory.Exists(path))
            {
                // === 切片逻辑 (原有逻辑) ===
                try
                {
                    var dirs = Directory.GetDirectories(path);
                    if (dirs.Length > 0)
                    {
                        _maxLevel = dirs.Select(d =>
                        {
                            int.TryParse(Path.GetFileName(d), out int lvl);
                            return lvl;
                        }).Max();
                    }

                    // 计算世界尺寸
                    if (GetLevelBoundsLegacy(0, out int maxRow, out int maxCol))
                    {
                        _worldSize = new Size((maxCol + 1) * TILE_SIZE, (maxRow + 1) * TILE_SIZE);
                        EffectiveRegion = new Rect(0, 0, _worldSize.Width, _worldSize.Height);
                    }
                }
                catch
                {
                    _maxLevel = 0;
                }
            }     
        }

        //允许外部手动设置有效区（如只允许在晶圆内部操作）
        public void SetValidRegion(Rect region)
        {
            EffectiveRegion = region;
        }

        public bool GetLevelBounds(int level,out int maxRow,out int maxCol)
        {
            if (_isSingleFileMode)
            {
                // 单图模式下，模拟成只有一张 (0,0) 的瓦片
                // 虽然是大图，但我们假装它是第 0 行第 0 列的一张图
                maxRow = 0;
                maxCol = 0;
                return true;
            }

            return GetLevelBoundsLegacy(level, out maxRow, out maxCol);
        }

        public bool GetLevelBoundsLegacy(int level, out int maxRow, out int maxCol)
        {
            maxRow = maxCol = -1;
            if (string.IsNullOrEmpty(_mapPath)) return false;

            if (_levelInfoCache.TryGetValue(level, out var info))
            {
                maxRow = info.MaxRow;
                maxCol = info.MaxCol;
                return (maxRow >= 0 && maxCol >= 0);
            }

            string levelDir = Path.Combine(_mapPath, level.ToString());
            if (!Directory.Exists(levelDir))
            {
                _levelInfoCache[level] = new LevelInfo(-1,-1);
                return false;
            }

            int rMax = -1, cMax = -1;
            try
            {
                foreach (var file in Directory.EnumerateFiles(levelDir, "*.png"))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    var parts = name.Split('_'); // row_col
                    if (parts.Length != 2) continue;

                    if (int.TryParse(parts[0], out int r) && int.TryParse(parts[1], out int c))
                    {
                        if (r > rMax) rMax = r;
                        if (c > cMax) cMax = c;
                    }
                }
            }
            catch
            {
                // ignore
            }

            _levelInfoCache[level] = new LevelInfo(rMax, cMax);
            maxRow = rMax;
            maxCol = cMax;

            return (rMax >= 0 && cMax >= 0);
        }

        /// <summary>
        /// World Space = Level0 像素坐标：用 Level0 的行列数计算世界尺寸。
        /// </summary>
        public bool GetWorldSize(out double width, out double height)
        {
            width = _worldSize.Width;
            height = _worldSize.Height;
            return width > 0 && height > 0;
        }

        public string GetTilePath(int level, int row, int col)
        {
            if (_isSingleFileMode) return _mapPath; // 单图模式直接返回文件绝对路径
            if (string.IsNullOrEmpty(_mapPath)) return null;
            return Path.Combine(_mapPath, level.ToString(), $"{row}_{col}.png");
        }
    }
}
