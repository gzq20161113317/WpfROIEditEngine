using RoiEditor.Core.Memory;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RoiEditor.Core.IO
{
    /// <summary>
    /// TileLoader
    /// - LRU 内存缓存
    /// - 并发门控
    /// - 支持 CancellationToken（真正减负）
    /// </summary>
    public class TileLoader
    {
        private readonly LruCache<string, BitmapSource> _memoryCache = new LruCache<string, BitmapSource>(2000);
        private readonly SemaphoreSlim _loadGate;

        public TileLoader(int maxConcurrency = 8)
        {
            _loadGate = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        }

        public async Task<ImageSource> LoadAsync(string path, CancellationToken token)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;

            BitmapSource cached;
            if (_memoryCache.TryGet(path, out cached))
            {
                System.Diagnostics.Debug.WriteLine($"[TileLoader] CACHE HIT {Path.GetFileName(path)}");
                return cached;
            }


            var swWait = System.Diagnostics.Stopwatch.StartNew();

            bool entered = false;
            try
            {
                await _loadGate.WaitAsync(token).ConfigureAwait(false);
                entered = true;

                swWait.Stop();

                System.Diagnostics.Debug.WriteLine(
                    $"[TileLoader] GATE OK {Path.GetFileName(path)} wait={swWait.ElapsedMilliseconds}ms");

                // double-check：避免并发下重复解码
                BitmapSource cached2;
                if (_memoryCache.TryGet(path, out cached2))
                {
                    System.Diagnostics.Debug.WriteLine($"[TileLoader] CACHE HIT(after wait) {Path.GetFileName(path)}");
                    return cached2;
                }

                var swDecode = System.Diagnostics.Stopwatch.StartNew();

                // 后台解码（支持 token）
                BitmapSource bmp = await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();

                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        token.ThrowIfCancellationRequested();

                        var decoder = BitmapDecoder.Create(
                            fs,
                            BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.IgnoreImageCache,
                            BitmapCacheOption.OnLoad);

                        token.ThrowIfCancellationRequested();

                        var frame = decoder.Frames[0];
                        frame.Freeze();
                        return (BitmapSource)frame;
                    }
                }, token).ConfigureAwait(false);

                swDecode.Stop();
                System.Diagnostics.Debug.WriteLine(
                    $"[TileLoader] DECODE OK {Path.GetFileName(path)} decode={swDecode.ElapsedMilliseconds}ms");


                // 写回 LRU
                _memoryCache.Add(path, bmp);
                return bmp;
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[TileLoader] CANCELED {Path.GetFileName(path)}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("[TileLoader Error] {0}: {1}", path, ex.Message));
                return null;
            }
            finally
            {
                if (entered)
                {
                    try { _loadGate.Release(); } catch { }
                }
            }
        }

        public void ClearCache()
        {
            _memoryCache.Clear();
        }
    }
}
