using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnturnedImages.Module.Config;
using UnturnedImages.Module.Workshop;

namespace UnturnedImages.Module.Images
{
    public static class ImageUtils
    {
        internal static string GenerateIdRanges(List<ushort> ids)
        {
            ids.Sort();

            var rangesBuilder = new StringBuilder();

            var startedRange = false;
            int? prevId = null;

            foreach (var id in ids)
            {
                if (!prevId.HasValue)
                {
                    rangesBuilder.Append(id);
                    prevId = id;

                    continue;
                }

                if (prevId.Value == id - 1)
                {
                    if (!startedRange)
                    {
                        rangesBuilder.Append('-');
                        startedRange = true;
                    }
                }
                else
                {
                    rangesBuilder.Append(prevId.Value);
                    rangesBuilder.Append(';');
                    rangesBuilder.Append(id);

                    startedRange = false;
                }

                prevId = id;
            }

            if (startedRange && prevId.HasValue)
            {
                rangesBuilder.Append(prevId.Value);
            }

            return rangesBuilder.ToString();
        }

        private static bool PassesFilters(Asset asset, ExportSettings settings)
        {
            var workshop = WorkshopHelper.IsWorkshop(asset);

            if (!settings.ExportOfficialAssets && !workshop)
            {
                return false;
            }

            if (!settings.ExportWorkshopAssets && workshop)
            {
                return false;
            }

            if (settings.WorkshopModIdFilter != 0 && workshop &&
                WorkshopHelper.GetWorkshopId(asset) != settings.WorkshopModIdFilter)
            {
                return false;
            }

            return true;
        }

        private static string ModFolderSection(Asset asset)
        {
            if (WorkshopHelper.IsWorkshop(asset))
            {
                var modId = WorkshopHelper.GetWorkshopId(asset);
                return Path.Combine(ImageExportPaths.WorkshopSegment, modId.ToString());
            }

            return ImageExportPaths.OfficialSegment;
        }

        /// <summary>
        /// Root: Unturned/Extras/UnturnedImagesGenerator/{category}/{Official|Workshop/id}/{name}.png
        /// </summary>
        private static void CaptureAssets<TAsset>(
            IEnumerable<TAsset> assets,
            ExportSettings settings,
            string categoryFolder,
            string yamlAssetCategoryKey,
            Action<TAsset, string> exportAction) where TAsset : Asset
        {
            var root = string.IsNullOrWhiteSpace(settings.OutputDirectoryOverride)
                ? Path.Combine(ReadWrite.PATH, "Extras", ImageExportPaths.RootFolderName)
                : settings.OutputDirectoryOverride;
            var basePath = Path.Combine(root, categoryFolder);
            var filteredAssets = assets.Where(asset => PassesFilters(asset, settings)).ToList();

            ExportProgressTracker.AddQueued($"Export {categoryFolder}", filteredAssets.Count);

            var modAssets = new Dictionary<uint, List<ushort>>();

            foreach (var asset in filteredAssets)
            {
                var modPathSection = ModFolderSection(asset);
                var fileBase = AssetNaming.GetFileBaseName(asset, settings.NamingMode);
                var fullPath = Path.Combine(basePath, modPathSection, fileBase);

                exportAction(asset, fullPath);

                if (!WorkshopHelper.IsWorkshop(asset))
                {
                    continue;
                }

                var modId = WorkshopHelper.GetWorkshopId(asset);

                if (!modAssets.TryGetValue(modId, out var assetList))
                {
                    assetList = new List<ushort>();
                    modAssets[modId] = assetList;
                }

                assetList.Add(asset.id);
            }

            if (!settings.WriteUnturnedImagesYamlHints)
            {
                return;
            }

            // One paste-ready YAML per mod (root/_Overrides/<modId>.yaml) accumulating item and
            // vehicle ID ranges across both export passes.
            var isVehicle = yamlAssetCategoryKey == "vehicles";
            foreach (var pair in modAssets)
            {
                OverrideHintWriter.SetCategory(pair.Key, isVehicle, pair.Value);
                OverrideHintWriter.Write(pair.Key, root, settings.NamingMode);
            }
        }

        public static void CaptureAllItemImages(ExportSettings settings)
        {
            var items = Assets.find(EAssetType.ITEM).OfType<ItemAsset>();

            CaptureAssets(items, settings, ImageExportPaths.ItemsCategory, "items", (asset, path) =>
            {
                if (settings.ItemIconMode == ItemIconExportMode.VanillaUiIcon)
                {
                    var item = new Item(asset.id, EItemOrigin.ADMIN);
                    CustomItemTool.QueueVanillaIcon(new CustomItemTool.VanillaIconJob(asset, item, path, false, null));
                }
                else
                {
                    CustomItemTool.QueueRenderedIcon(new CustomItemTool.RenderedIconJob(asset, settings.ItemSkinId,
                        path, settings.ImageSize, settings.ImageSize, settings.ItemExtraEulerDegrees, false, null,
                        settings.SupersamplingScale, settings.ToRenderOptions()));
                }
            });
        }

        public static void CaptureAllVehicleImages(ExportSettings settings)
        {
            var vehicles = Assets.find(EAssetType.VEHICLE).OfType<VehicleAsset>();

            CaptureAssets(vehicles, settings, ImageExportPaths.VehiclesCategory, "vehicles", (asset, path) =>
            {
                CustomVehicleTool.QueueVehicleIcon(asset, path, settings.ImageSize, settings.ImageSize,
                    settings.VehicleEulerDegrees, settings.ToRenderOptions(), false, null);
            });
        }

        public static void CaptureItemsForWorkshopMod(uint workshopModId, ExportSettings settings)
        {
            var items = Assets.find(EAssetType.ITEM).OfType<ItemAsset>()
                .Where(a => WorkshopHelper.IsWorkshop(a) && WorkshopHelper.GetWorkshopId(a) == workshopModId);

            CaptureAssets(items, settings, ImageExportPaths.ItemsCategory, "items", (asset, path) =>
            {
                if (settings.ItemIconMode == ItemIconExportMode.VanillaUiIcon)
                {
                    var item = new Item(asset.id, EItemOrigin.ADMIN);
                    CustomItemTool.QueueVanillaIcon(new CustomItemTool.VanillaIconJob(asset, item, path, false, null));
                }
                else
                {
                    CustomItemTool.QueueRenderedIcon(new CustomItemTool.RenderedIconJob(asset, settings.ItemSkinId,
                        path, settings.ImageSize, settings.ImageSize, settings.ItemExtraEulerDegrees, false, null,
                        settings.SupersamplingScale, settings.ToRenderOptions()));
                }
            });
        }

        public static void CaptureVehiclesForWorkshopMod(uint workshopModId, ExportSettings settings)
        {
            var vehicles = Assets.find(EAssetType.VEHICLE).OfType<VehicleAsset>()
                .Where(a => WorkshopHelper.IsWorkshop(a) && WorkshopHelper.GetWorkshopId(a) == workshopModId);

            CaptureAssets(vehicles, settings, ImageExportPaths.VehiclesCategory, "vehicles", (asset, path) =>
            {
                CustomVehicleTool.QueueVehicleIcon(asset, path, settings.ImageSize, settings.ImageSize,
                    settings.VehicleEulerDegrees, settings.ToRenderOptions(), false, null);
            });
        }

        internal static void EnsureExportDirectories()
        {
            try
            {
                Directory.CreateDirectory(Path.Combine(ReadWrite.PATH, "Extras"));
                Directory.CreateDirectory(Path.Combine(ReadWrite.PATH, "Extras", ImageExportPaths.RootFolderName));
            }
            catch (Exception ex)
            {
                UnturnedLog.error("UnturnedImagesGenerator: failed to create Extras folders: " + ex.Message);
            }
        }

        internal static Texture2D DuplicateReadable(Texture2D source)
        {
            var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            Graphics.Blit(source, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var dup = new Texture2D(source.width, source.height, TextureFormat.ARGB32, false);
            dup.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            dup.Apply(false, false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return dup;
        }

        internal static Texture2D DownscaleReadable(Texture2D source, int width, int height)
        {
            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            Graphics.Blit(source, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var dup = new Texture2D(width, height, TextureFormat.ARGB32, false)
            {
                filterMode = FilterMode.Bilinear
            };
            dup.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            dup.Apply(false, false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return dup;
        }

        /// <summary>
        /// Applies optional transparent-margin trimming and solid background fill.
        /// Returns the texture to export — may be the same instance as <paramref name="source"/>
        /// (modified in place) or a new one. Callers should destroy a returned texture that
        /// differs from <paramref name="source"/> separately.
        /// </summary>
        internal static Texture2D ApplyPostProcessing(Texture2D source, IconRenderOptions options, bool readableOnCPU)
        {
            var doTrim = options.TrimTransparentPadding && source.width == source.height;
            var doBackground = options.UseSolidBackground;

            if (!doTrim && !doBackground)
            {
                return source;
            }

            var working = doTrim ? TrimToPadding(source, options.TrimPaddingFraction) : source;

            if (doBackground)
            {
                FillBackground(working, options.BackgroundColor);
            }

            working.Apply(false, !readableOnCPU);
            return working;
        }

        private static Texture2D TrimToPadding(Texture2D source, float paddingFraction)
        {
            var width = source.width;
            var height = source.height;
            var pixels = source.GetPixels32();

            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (var y = 0; y < height; y++)
            {
                var rowOffset = y * width;
                for (var x = 0; x < width; x++)
                {
                    if (pixels[rowOffset + x].a <= 8)
                    {
                        continue;
                    }

                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < minX || maxY < minY)
            {
                // Nothing opaque to trim around.
                return source;
            }

            float contentWidth = maxX - minX + 1;
            float contentHeight = maxY - minY + 1;
            var centerX = (minX + maxX + 1) * 0.5f;
            var centerY = (minY + maxY + 1) * 0.5f;

            var side = Mathf.Max(contentWidth, contentHeight);
            var frac = Mathf.Clamp(paddingFraction, 0f, 0.45f);
            var sampleSide = side / Mathf.Max(0.1f, 1f - 2f * frac);

            var scale = new Vector2(sampleSide / width, sampleSide / height);
            var offset = new Vector2((centerX - sampleSide * 0.5f) / width, (centerY - sampleSide * 0.5f) / height);

            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            // Clamp so a model touching the frame edge does not wrap in from the opposite side.
            source.wrapMode = TextureWrapMode.Clamp;
            Graphics.Blit(source, rt, scale, offset);
            RenderTexture.active = rt;
            var result = new Texture2D(width, height, TextureFormat.ARGB32, false)
            {
                filterMode = FilterMode.Bilinear
            };
            result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return result;
        }

        private static void FillBackground(Texture2D texture, Color background)
        {
            var pixels = texture.GetPixels32();
            var bgR = Mathf.Clamp01(background.r);
            var bgG = Mathf.Clamp01(background.g);
            var bgB = Mathf.Clamp01(background.b);

            for (var i = 0; i < pixels.Length; i++)
            {
                var a = pixels[i].a / 255f;
                pixels[i].r = (byte)Mathf.RoundToInt(Mathf.Clamp01(bgR * (1f - a) + pixels[i].r / 255f * a) * 255f);
                pixels[i].g = (byte)Mathf.RoundToInt(Mathf.Clamp01(bgG * (1f - a) + pixels[i].g / 255f * a) * 255f);
                pixels[i].b = (byte)Mathf.RoundToInt(Mathf.Clamp01(bgB * (1f - a) + pixels[i].b / 255f * a) * 255f);
                pixels[i].a = 255;
            }

            texture.SetPixels32(pixels);
        }
    }
}
