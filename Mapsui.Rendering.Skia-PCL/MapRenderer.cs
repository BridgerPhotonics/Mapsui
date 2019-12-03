using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mapsui.Geometries;
using Mapsui.Layers;
using Mapsui.Logging;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.Widgets;
using SkiaSharp;

namespace Mapsui.Rendering.Skia
{
    public class MapRenderer : IRenderer
    {
        private const int TilesToKeepMultiplier = 3;
        private const int MinimumTilesToKeep = 32;
        private readonly SymbolCache _symbolCache = new SymbolCache();

        private readonly IDictionary<object, BitmapInfo> _tileCache =
            new Dictionary<object, BitmapInfo>(new IdentityComparer<object>());

        private long _currentIteration;

        public ISymbolCache SymbolCache => _symbolCache;

        static MapRenderer()
        {
            DefaultRendererFactory.Create = () => new MapRenderer();
        }

        public void Render(object target, IViewport viewport, IEnumerable<ILayer> layers,
            IEnumerable<IWidget> widgets, Color background = null)
        {
            var allWidgets = layers.Select(l => l.Attribution).ToList().Concat(widgets);
            Render((SKCanvas)target, viewport, layers, allWidgets, background);
        }

        private void Render(SKCanvas canvas, IViewport viewport, IEnumerable<ILayer> layers,
            IEnumerable<IWidget> widgets, Color background = null)
        {
            if (background != null) canvas.Clear(background.ToSkia(1));
            if (viewport.Initialized) Render(canvas, viewport, layers);
            Render(canvas, viewport, widgets, 1);
        }

        public MemoryStream RenderToBitmapStream(IViewport viewport, IEnumerable<ILayer> layers, Color background = null)
        {
            try
            {
                using (var surface = SKSurface.Create(
                    (int)viewport.Width, (int)viewport.Height, SKImageInfo.PlatformColorType, SKAlphaType.Unpremul))
                {
                    if (surface == null) return null;
                    // Not sure if this is needed here:
                    if (background != null) surface.Canvas.Clear(background.ToSkia(1));
                    Render(surface.Canvas, viewport, layers);
                    using (var image = surface.Snapshot())
                    {
                        using (var data = image.Encode())
                        {
                            var memoryStream = new MemoryStream();
                            data.SaveTo(memoryStream);
                            return memoryStream;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, ex.Message);
                return null;
            }
        }

        public void Render(SKCanvas canvas, IViewport viewport, IEnumerable<ILayer> layers)
        {
            try
            {
                layers = layers.ToList();

                VisibleFeatureIterator.IterateLayers(viewport, layers, (v, l, s, o) => { RenderFeature(canvas, v, l, s, o); });

                RemovedUnusedBitmapsFromCache();

                _currentIteration++;
            }
            catch (Exception exception)
            {
                Logger.Log(LogLevel.Error, "Unexpected error in skia renderer", exception);
            }
        }

        private void RemovedUnusedBitmapsFromCache()
        {
            var tilesUsedInCurrentIteration =
                _tileCache.Values.Count(i => i.IterationUsed == _currentIteration);
            var tilesToKeep = tilesUsedInCurrentIteration * TilesToKeepMultiplier;
            tilesToKeep = Math.Max(tilesToKeep, MinimumTilesToKeep);
            var tilesToRemove = _tileCache.Keys.Count - tilesToKeep;

            if (tilesToRemove > 0) RemoveOldBitmaps(_tileCache, tilesToRemove);
        }

        private static void RemoveOldBitmaps(IDictionary<object, BitmapInfo> tileCache, int numberToRemove)
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

        public static MemoryStream RenderFeaturesToBitmapStream(IViewport viewport, IEnumerable<IFeature> featureList, RenderCache cache = null, Color background = null)
        {
            try
            {
                using (var surface = SKSurface.Create(
                    (int)viewport.Width, (int)viewport.Height, SKImageInfo.PlatformColorType, SKAlphaType.Unpremul))
                {
                    if (surface == null) return null;
                    // Not sure if this is needed here:
                    if (background != null) surface.Canvas.Clear(background.ToSkia(1));
                    RenderFeaturesRasterize(surface.Canvas, viewport, featureList, cache);
                    using (var image = surface.Snapshot())
                    {
                        using (var data = image.Encode())
                        {
                            var memoryStream = new MemoryStream();
                            data.SaveTo(memoryStream);
                            return memoryStream;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("RenderFeatureToBitmapStream exception: " + e.Message);
                return null;
            }
        }


        public static void RenderFeaturesRasterize(SKCanvas canvas, IViewport viewport, IEnumerable<IFeature> featureList, RenderCache cache = null)
        {
            try
            {
                if (cache == null) cache = new RenderCache();
                foreach (var feature in featureList)
                {
                    var featureStyleList = feature.Styles ?? Enumerable.Empty<IStyle>();
                    foreach (var style in featureStyleList)
                    {
                        if (style == null || !style.Enabled || style.MinVisible > viewport.Resolution || style.MaxVisible < viewport.Resolution) continue;

                        RenderFeatureRasterize(canvas, viewport, style, feature, cache, 1.0f);
                    }
                }

                cache.RemovedUnusedBitmapsFromCache();
            }
            catch (Exception exception)
            {
                Logger.Log(LogLevel.Error, "Unexpected error in skia renderer", exception);
            }
        }

        public static void RenderFeatureRasterize(SKCanvas canvas, IViewport viewport, IStyle style, IFeature feature, RenderCache cache, float layerOpacity = 1.0f)
        {
            if (feature.Geometry is Point)
                lock (style)
                    PointRenderer.Draw(canvas, viewport, style, feature, feature.Geometry, cache.SymbolCache, layerOpacity * style.Opacity);
            else if (feature.Geometry is MultiPoint)
                lock (style)
                    MultiPointRenderer.Draw(canvas, viewport, style, feature, feature.Geometry, cache.SymbolCache, layerOpacity * style.Opacity);
            else if (feature.Geometry is LineString)
                LineStringRenderer.Draw(canvas, viewport, style, feature, feature.Geometry, layerOpacity * style.Opacity);
            else if (feature.Geometry is MultiLineString)
                MultiLineStringRenderer.Draw(canvas, viewport, style, feature, feature.Geometry, layerOpacity * style.Opacity);
            else if (feature.Geometry is Polygon)
                PolygonRenderer.Draw(canvas, viewport, style, feature, feature.Geometry, layerOpacity * style.Opacity, cache.SymbolCache);
            else if (feature.Geometry is MultiPolygon)
                MultiPolygonRenderer.Draw(canvas, viewport, style, feature, feature.Geometry, layerOpacity * style.Opacity, cache.SymbolCache);
            else if (feature.Geometry is IRaster)
                lock (feature)
                    RasterRenderer.Draw(canvas, viewport, style, feature, layerOpacity * style.Opacity, cache.TileCache, cache.CurrentIteration);
        }

        public void RenderFeature(SKCanvas canvas, IViewport viewport, IStyle style, IFeature feature, float layerOpacity)
        {
            if (feature.Geometry is Point)
                PointRenderer.Draw(canvas, viewport, style, feature, feature.Geometry, _symbolCache, layerOpacity * style.Opacity);
            else if (feature.Geometry is MultiPoint)
                MultiPointRenderer.Draw(canvas, viewport, style, feature, feature.Geometry, _symbolCache, layerOpacity * style.Opacity);
            else if (feature.Geometry is LineString)
                LineStringRenderer.Draw(canvas, viewport, style, feature, feature.Geometry, layerOpacity * style.Opacity);
            else if (feature.Geometry is MultiLineString)
                MultiLineStringRenderer.Draw(canvas, viewport, style, feature, feature.Geometry, layerOpacity * style.Opacity);
            else if (feature.Geometry is Polygon)
                PolygonRenderer.Draw(canvas, viewport, style, feature, feature.Geometry, layerOpacity * style.Opacity, _symbolCache);
            else if (feature.Geometry is MultiPolygon)
                MultiPolygonRenderer.Draw(canvas, viewport, style, feature, feature.Geometry, layerOpacity * style.Opacity, _symbolCache);
            else if (feature.Geometry is IRaster)
                RasterRenderer.Draw(canvas, viewport, style, feature, layerOpacity * style.Opacity, _tileCache, _currentIteration);
        }

        private void Render(object canvas, IViewport viewport, IEnumerable<IWidget> widgets, float layerOpacity)
        {
            WidgetRenderer.Render(canvas, viewport.Width, viewport.Height, widgets, layerOpacity);
        }
    }

    public class IdentityComparer<T> : IEqualityComparer<T> where T : class
    {
        public bool Equals(T obj, T otherObj)
        {
            return obj == otherObj;
        }

        public int GetHashCode(T obj)
        {
            return obj.GetHashCode();
        }
    }
}