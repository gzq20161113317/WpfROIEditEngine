using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

        // Level -> (MaxRow, MaxCol)
        private readonly Dictionary<int, (int rows, int cols)> _levelInfoCache = new Dictionary<int, (int, int)>();

        public int MaxLevel => _maxLevel;
        public string MapPath => _mapPath;

        public void LoadMap(string path)
        {
            _mapPath = path;
            _levelInfoCache.Clear();
            _maxLevel = 0;

            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return;

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
            }
            catch
            {
                _maxLevel = 0;
            }
        }

        public bool GetLevelBounds(int level, out int maxRow, out int maxCol)
        {
            maxRow = maxCol = -1;
            if (string.IsNullOrEmpty(_mapPath)) return false;

            if (_levelInfoCache.TryGetValue(level, out var info))
            {
                maxRow = info.rows;
                maxCol = info.cols;
                return (maxRow >= 0 && maxCol >= 0);
            }

            string levelDir = Path.Combine(_mapPath, level.ToString());
            if (!Directory.Exists(levelDir))
            {
                _levelInfoCache[level] = (-1, -1);
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

            _levelInfoCache[level] = (rMax, cMax);
            maxRow = rMax;
            maxCol = cMax;

            return (rMax >= 0 && cMax >= 0);
        }

        /// <summary>
        /// World Space = Level0 像素坐标：用 Level0 的行列数计算世界尺寸。
        /// </summary>
        public bool GetWorldSize(out double width, out double height)
        {
            width = height = 0;

            if (GetLevelBounds(0, out int maxRow, out int maxCol))
            {
                width = (maxCol + 1) * TILE_SIZE;
                height = (maxRow + 1) * TILE_SIZE;
                return true;
            }

            return false;
        }

        public string GetTilePath(int level, int row, int col)
        {
            if (string.IsNullOrEmpty(_mapPath)) return null;
            return Path.Combine(_mapPath, level.ToString(), $"{row}_{col}.png");
        }
    }
}
