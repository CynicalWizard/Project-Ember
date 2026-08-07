using System;
using System.Collections.Generic;
using System.Linq;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using static Robust.UnitTesting.RobustIntegrationTest;
using Color = Robust.Shared.Maths.Color;

namespace Content.MapRenderer.Painters
{
    public sealed class TilePainter
    {
        public const int TileImageSize = EyeManager.PixelsPerMeter;

        private readonly ITileDefinitionManager _sTileDefinitionManager;
        private readonly SharedMapSystem _sMapSystem;
        private readonly IResourceManager _resManager;

        public TilePainter(ClientIntegrationInstance client, ServerIntegrationInstance server)
        {
            _sTileDefinitionManager = server.ResolveDependency<ITileDefinitionManager>();
            _resManager = client.ResolveDependency<IResourceManager>();
            var esm = server.ResolveDependency<IEntitySystemManager>();
            _sMapSystem = esm.GetEntitySystem<SharedMapSystem>();
        }

        public void Run(Image gridCanvas, EntityUid gridUid, MapGridComponent grid)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var bounds = grid.LocalAABB;
            var xOffset = -bounds.Left;
            var yOffset = -bounds.Bottom;
            var tileSize = grid.TileSize * TileImageSize;

            var images = GetTileImages(_sTileDefinitionManager, _resManager, tileSize);
            var i = 0;

            _sMapSystem.GetAllTiles(gridUid, grid).AsParallel().ForAll(tile =>
            {
                var definition = _sTileDefinitionManager[tile.Tile.TypeId];

                if (!images.TryGetValue(definition.ID, out var variants))
                    return;

                var x = (int) (tile.X + xOffset);
                var y = (int) (tile.Y + yOffset);
                var image = variants[tile.Tile.Variant];

                gridCanvas.Mutate(o => o.DrawImage(image, new Point(x * tileSize, y * tileSize), 1));

                i++;
            });

            Console.WriteLine($"{nameof(TilePainter)} painted {i} tiles on grid {gridUid} in {(int) stopwatch.Elapsed.TotalMilliseconds} ms");
        }

        private Dictionary<string, List<Image>> GetTileImages(
            ITileDefinitionManager tileDefinitionManager,
            IResourceManager resManager,
            int tileSize)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var images = new Dictionary<string, List<Image>>();

            foreach (var definition in tileDefinitionManager)
            {
                var path = definition.Sprite.ToString();

                if (string.IsNullOrWhiteSpace(path))
                    continue;

                var variants = new List<Image>(definition.Variants);
                images[definition.ID] = variants;

                using var stream = resManager.ContentFileRead(path);
                var tileSheet = Image.Load<Rgba32>(stream);

                Tint(tileSheet, definition.Color);

                if (tileSheet.Width != tileSize * definition.Variants || tileSheet.Height != tileSize)
                {
                    throw new NotSupportedException($"Unable to use tiles with a dimension other than {tileSize}x{tileSize}.");
                }

                for (var i = 0; i < definition.Variants; i++)
                {
                    var index = i;
                    var tileImage = tileSheet.Clone(o => o.Crop(new Rectangle(tileSize * index, 0, 32, 32)));
                    variants.Add(tileImage);
                }
            }

            Console.WriteLine($"Indexed all tile images in {(int) stopwatch.Elapsed.TotalMilliseconds} ms");

            return images;
        }

        /// <summary>
        /// Mirrors what the engine does when it bakes the tile atlas, so a floor whose colour comes from its
        /// definition is painted the same here as it is drawn in game.
        /// </summary>
        private static void Tint(Image<Rgba32> image, Color color)
        {
            if (color == Color.White)
                return;

            var (r, g, b, a) = (color.RByte, color.GByte, color.BByte, color.AByte);

            image.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);

                    for (var x = 0; x < row.Length; x++)
                    {
                        ref var pixel = ref row[x];

                        pixel.R = (byte) (pixel.R * r / 255);
                        pixel.G = (byte) (pixel.G * g / 255);
                        pixel.B = (byte) (pixel.B * b / 255);
                        pixel.A = (byte) (pixel.A * a / 255);
                    }
                }
            });
        }
    }
}
