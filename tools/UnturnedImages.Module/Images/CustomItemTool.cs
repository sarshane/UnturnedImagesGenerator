using SDG.Provider;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnturnedImages.Module.Images
{
    public sealed class CustomItemTool : MonoBehaviour
    {
        public sealed class RenderedIconJob
        {
            public RenderedIconJob(ItemAsset asset, ushort skinId, string outputPathBase, int width, int height,
                Vector3 extraEulerDegrees, bool previewOnly, Action<Texture2D>? onPreviewTexture,
                int supersamplingScale = 1, IconRenderOptions? renderOptions = null)
            {
                Asset = asset;
                SkinId = skinId;
                OutputPathBase = outputPathBase;
                Width = width;
                Height = height;
                ExtraEulerDegrees = extraEulerDegrees;
                PreviewOnly = previewOnly;
                OnPreviewTexture = onPreviewTexture;
                SupersamplingScale = Mathf.Clamp(supersamplingScale, 1, 4);
                RenderOptions = renderOptions ?? new IconRenderOptions();
            }

            public ItemAsset Asset { get; }

            public ushort SkinId { get; }

            public string OutputPathBase { get; }

            public int Width { get; }

            public int Height { get; }

            public Vector3 ExtraEulerDegrees { get; }

            public bool PreviewOnly { get; }

            public int SupersamplingScale { get; }

            public IconRenderOptions RenderOptions { get; }

            public Action<Texture2D>? OnPreviewTexture { get; }
        }

        public sealed class VanillaIconJob
        {
            public VanillaIconJob(ItemAsset asset, Item item, string outputPathBase, bool previewOnly,
                Action<Texture2D>? onPreviewTexture)
            {
                Asset = asset;
                Item = item;
                OutputPathBase = outputPathBase;
                PreviewOnly = previewOnly;
                OnPreviewTexture = onPreviewTexture;
            }

            public ItemAsset Asset { get; }

            public Item Item { get; }

            public string OutputPathBase { get; }

            public bool PreviewOnly { get; }

            public Action<Texture2D>? OnPreviewTexture { get; }
        }

        private static CustomItemTool? _instance;

        private readonly Queue<RenderedIconJob> _preview3D = new();

        private readonly Queue<RenderedIconJob> _queue3D = new();

        private readonly Queue<VanillaIconJob> _previewVanilla = new();

        private readonly Queue<VanillaIconJob> _queueVanilla = new();

        private VanillaIconJob? _vanillaInFlight;

        private Texture2D? _vanillaTextureResult;

        private int _vanillaWaitFrames;

        private const int VanillaTimeoutFrames = 600;

        private Transform _camera = null!;

        private static readonly GetStatTrackerValueHandler NoStatTracker =
            delegate(out EStatTrackerType type, out int value)
            {
                type = default;
                value = 0;
                return false;
            };

        public static void Load()
        {
            _instance = UnturnedImagesModule.Instance!.GameObject!.AddComponent<CustomItemTool>();
        }

        public static void Unload()
        {
            Destroy(_instance);
            _instance = null;
        }

        public static void ClearQueues()
        {
            if (_instance == null)
            {
                return;
            }

            _instance._preview3D.Clear();
            _instance._queue3D.Clear();
            _instance._previewVanilla.Clear();
            _instance._queueVanilla.Clear();
        }

        public static void QueueRenderedIcon(RenderedIconJob job)
        {
            if (_instance == null)
            {
                return;
            }

            if (job.PreviewOnly)
            {
                _instance._preview3D.Enqueue(job);
            }
            else
            {
                _instance._queue3D.Enqueue(job);
            }
        }

        public static void QueueVanillaIcon(VanillaIconJob job)
        {
            if (_instance == null)
            {
                return;
            }

            if (job.PreviewOnly)
            {
                _instance._previewVanilla.Enqueue(job);
            }
            else
            {
                _instance._queueVanilla.Enqueue(job);
            }
        }

        private void Start()
        {
            var holder = new GameObject();
            _camera = Instantiate(holder).transform;
            Destroy(holder);
        }

        private void Update()
        {
            if (_vanillaInFlight != null)
            {
                if (_vanillaTextureResult == null)
                {
                    if (++_vanillaWaitFrames > VanillaTimeoutFrames)
                    {
                        UnturnedLog.warn(
                            $"UnturnedImagesGenerator: vanilla icon timed out (item {_vanillaInFlight.Asset.id}), skipping.");
                        if (!_vanillaInFlight.PreviewOnly)
                        {
                            ExportProgressTracker.CompleteOne($"Item {_vanillaInFlight.Asset.id}: icon timeout",
                                false);
                        }

                        _vanillaInFlight = null;
                        _vanillaWaitFrames = 0;
                    }

                    return;
                }

                FinishVanilla(_vanillaInFlight, _vanillaTextureResult);
                _vanillaInFlight = null;
                _vanillaTextureResult = null;
                _vanillaWaitFrames = 0;
                return;
            }

            if (_previewVanilla.Count > 0)
            {
                StartVanilla(_previewVanilla.Dequeue());
                return;
            }

            if (_preview3D.Count > 0)
            {
                ProcessRendered(_preview3D.Dequeue());
                return;
            }

            if (_queueVanilla.Count > 0)
            {
                StartVanilla(_queueVanilla.Dequeue());
                return;
            }

            if (_queue3D.Count > 0)
            {
                ProcessRendered(_queue3D.Dequeue());
            }
        }

        private void StartVanilla(VanillaIconJob job)
        {
            _vanillaInFlight = job;
            _vanillaTextureResult = null;
            _vanillaWaitFrames = 0;

            try
            {
                ItemTool.getIcon(job.Asset.id, job.Item.quality, job.Item.state, job.Asset,
                    delegate(int handle, Texture2D texture)
                    {
                        _ = handle;
                        _vanillaTextureResult = texture;
                    });
            }
            catch (Exception ex)
            {
                UnturnedLog.error($"UnturnedImagesGenerator: ItemTool.getIcon unavailable for item {job.Asset.id} " +
                                  $"({ex.GetType().Name}). Switch to the Item: 3D mode.");
                if (!job.PreviewOnly)
                {
                    ExportProgressTracker.CompleteOne($"Item {job.Asset.id}: icon unavailable", false);
                }

                _vanillaInFlight = null;
            }
        }

        private void FinishVanilla(VanillaIconJob job, Texture2D texture)
        {
            try
            {
                // ItemTool.getIcon returns a GPU-only texture, so copy it to a CPU-readable one via
                // a blit (works even though the source is not marked readable) before encoding/using it.
                var readable = ImageUtils.DuplicateReadable(texture);

                if (job.PreviewOnly)
                {
                    if (job.OnPreviewTexture != null)
                    {
                        job.OnPreviewTexture(readable);
                    }
                    else
                    {
                        Destroy(readable);
                    }

                    return;
                }

                try
                {
                    var path = $"{job.OutputPathBase}.png";
                    ReadWrite.writeBytes(path, false, false, readable.EncodeToPNG());
                    ExportProgressTracker.CompleteOne($"Item {job.Asset.id}", true);
                }
                finally
                {
                    Destroy(readable);
                }
            }
            catch (Exception ex)
            {
                UnturnedLog.error("UnturnedImagesGenerator: vanilla icon export failed: " + ex.Message);
                if (!job.PreviewOnly)
                {
                    ExportProgressTracker.CompleteOne($"Item {job.Asset.id}: error", false);
                }
            }
        }

        private void ProcessRendered(RenderedIconJob job)
        {
            var asset = job.Asset;
            Transform? parent = null;
            Texture2D? texture = null;
            Texture2D? outputTexture = null;
            Texture2D? processed = null;

            try
            {
                var item = new Item(asset.id, EItemOrigin.ADMIN);
                var model = ItemTool.getItem(asset.id, job.SkinId, item.quality, item.state, false, asset,
                    NoStatTracker);
                if (model == null)
                {
                    UnturnedLog.error($"UnturnedImagesGenerator: ItemTool.getItem returned null (item id={asset.id}).");
                    if (!job.PreviewOnly)
                    {
                        ExportProgressTracker.CompleteOne($"Item {asset.id}: model not found", false);
                    }

                    return;
                }

                Layerer.relayer(model, LayerMasks.SMALL);

                parent = new GameObject().transform;
                model.SetParent(parent);
                parent.position = new Vector3(-256f, -256f, 0f);

                if (_camera == null)
                {
                    _camera = Instantiate(new GameObject()).transform;
                }

                _camera.SetParent(model, false);
                model.Rotate(job.ExtraEulerDegrees);
                _camera.rotation = Quaternion.identity;

                var renderWidth = job.Width * job.SupersamplingScale;
                var renderHeight = job.Height * job.SupersamplingScale;

                // The vanilla ItemTool.CalculateOrthographicSize no longer exists, so frame the item with
                // our own bounding sphere (same approach as vehicles) instead of falling back to size 1.
                float orthographicSize;
                if (CustomImageTool.TryGetBoundingSphere(parent.gameObject, out var center, out var radius))
                {
                    orthographicSize = radius * CustomImageTool.FrameMargin;
                    _camera.position = center - _camera.forward * (radius + 1f);
                }
                else
                {
                    orthographicSize = 1f;
                    UnturnedLog.warn($"UnturnedImagesGenerator: item {asset.id} has no renderers to frame.");
                }

                texture = ItemTool.captureIcon(asset.id, job.SkinId, model, _camera, renderWidth, renderHeight,
                    orthographicSize, true);
                outputTexture = job.SupersamplingScale > 1
                    ? ImageUtils.DownscaleReadable(texture, job.Width, job.Height)
                    : texture;
                processed = ImageUtils.ApplyPostProcessing(outputTexture, job.RenderOptions, true);

                if (job.PreviewOnly)
                {
                    job.OnPreviewTexture?.Invoke(ImageUtils.DuplicateReadable(processed));
                }
                else
                {
                    var path = $"{job.OutputPathBase}.png";
                    ReadWrite.writeBytes(path, false, false, processed.EncodeToPNG());
                    ExportProgressTracker.CompleteOne($"Item {asset.id}", true);
                }
            }
            catch (Exception ex)
            {
                UnturnedLog.error($"UnturnedImagesGenerator: item {asset.id} render failed: {ex}");
                if (!job.PreviewOnly)
                {
                    ExportProgressTracker.CompleteOne($"Item {asset.id}: error", false);
                }
            }
            finally
            {
                if (processed != null && processed != outputTexture)
                {
                    Destroy(processed);
                }

                if (outputTexture != null && outputTexture != texture)
                {
                    Destroy(outputTexture);
                }

                if (texture != null)
                {
                    Destroy(texture);
                }

                if (_camera != null)
                {
                    _camera.SetParent(null);
                }

                if (parent != null)
                {
                    Destroy(parent.gameObject);
                }
            }
        }
    }
}
