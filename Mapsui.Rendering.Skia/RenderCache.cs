using System;
using System.Collections.Generic;
using System.Linq;

namespace Mapsui.Rendering.Skia
{
    public class RenderCache
    {
        private const int TilesToKeepMultiplier = 3;
        private const int MinimumTilesToKeep = 32;

        public SymbolCache SymbolCache { get; } = new SymbolCache();
        public IDictionary<object, BitmapInfo> TileCache { get; } = new Dictionary<object, BitmapInfo>(new MapRenderer.IdentityComparer<object>());

        public long CurrentIteration = 0;

        public void RemovedUnusedBitmapsFromCache()
        {
            var tilesUsedInCurrentIteration =
                TileCache.Values.Count(i => i.IterationUsed == CurrentIteration);
            var tilesToKeep = tilesUsedInCurrentIteration * TilesToKeepMultiplier;
            tilesToKeep = Math.Max(tilesToKeep, MinimumTilesToKeep);
            var tilesToRemove = TileCache.Keys.Count - tilesToKeep;

            if (tilesToRemove > 0) RemoveOldBitmaps(TileCache, tilesToRemove);
        }

        public static void RemoveOldBitmaps(IDictionary<object, BitmapInfo> tileCache, int numberToRemove)
        {
            var counter = 0;
            var orderedKeys = tileCache.OrderBy(kvp => kvp.Value.IterationUsed).Select(kvp => kvp.Key).ToList();
            foreach (var key in orderedKeys)
            {
                if (counter >= numberToRemove) break;
                var textureInfo = tileCache[key];
                tileCache.Remove(key);
                textureInfo.Bitmap.Dispose();
                counter++;
            }
        }
    }
}