using SDG.Unturned;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnturnedImages.Module.Images
{
    public sealed class CustomVehicleTool : MonoBehaviour
    {
        public sealed class CustomVehicleIconInfo
        {
            public CustomVehicleIconInfo(VehicleAsset vehicleAsset, string outputPath, int width, int height,
                Vector3 angles, IconRenderOptions renderOptions, bool previewOnly, Action<Texture2D>? onPreviewTexture)
            {
                VehicleAsset = vehicleAsset;
                OutputPath = outputPath;
                Width = width;
                Height = height;
                Angles = angles;
                RenderOptions = renderOptions;
                PreviewOnly = previewOnly;
                OnPreviewTexture = onPreviewTexture;
            }

            public VehicleAsset VehicleAsset { get; }

            public string OutputPath { get; }

            public int Width { get; }

            public int Height { get; }

            public Vector3 Angles { get; }

            public IconRenderOptions RenderOptions { get; }

            public bool PreviewOnly { get; }

            public Action<Texture2D>? OnPreviewTexture { get; }
        }

        private static CustomVehicleTool? _instance;

        private static readonly string[] WeirdLookingObjects =
        {
            "DepthMask"
        };

        private readonly Queue<CustomVehicleIconInfo> _previewQueue = new();

        private readonly Queue<CustomVehicleIconInfo> _queue = new();

        private Transform _camera = null!;

        public static void Load()
        {
            _instance = UnturnedImagesModule.Instance!.GameObject!.AddComponent<CustomVehicleTool>();
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

            _instance._previewQueue.Clear();
            _instance._queue.Clear();
        }

        private void Start()
        {
            var holder = new GameObject();
            _camera = Instantiate(holder).transform;
            Destroy(holder);
        }

        public static Transform? GetVehicle(VehicleAsset vehicleAsset)
        {
            var gameObject = vehicleAsset.GetOrLoadModel();

            if (gameObject == null)
            {
                return null;
            }

            return Instantiate(gameObject).transform;
        }

        public static void QueueVehicleIcon(VehicleAsset vehicleAsset, string outputPath, int width, int height,
            Vector3? vehicleAngles, bool previewOnly, Action<Texture2D>? onPreviewTexture)
        {
            QueueVehicleIcon(vehicleAsset, outputPath, width, height, vehicleAngles, new IconRenderOptions(),
                previewOnly, onPreviewTexture);
        }

        public static void QueueVehicleIcon(VehicleAsset vehicleAsset, string outputPath, int width, int height,
            Vector3? vehicleAngles, IconRenderOptions renderOptions, bool previewOnly, Action<Texture2D>? onPreviewTexture)
        {
            if (_instance == null)
            {
                return;
            }

            vehicleAngles ??= Vector3.zero;

            var vehicleIconInfo = new CustomVehicleIconInfo(vehicleAsset, outputPath, width, height,
                vehicleAngles.Value, renderOptions, previewOnly, onPreviewTexture);

            if (previewOnly)
            {
                _instance._previewQueue.Enqueue(vehicleIconInfo);
            }
            else
            {
                _instance._queue.Enqueue(vehicleIconInfo);
            }
        }

        private void Update()
        {
            if (_previewQueue.Count == 0 && _queue.Count == 0)
            {
                return;
            }

            var vehicleIconInfo = _previewQueue.Count > 0 ? _previewQueue.Dequeue() : _queue.Dequeue();
            var vehicleAsset = vehicleIconInfo.VehicleAsset;
            var vehicle = GetVehicle(vehicleAsset);
            if (vehicle == null)
            {
                UnturnedLog.error($"UnturnedImagesGenerator: could not load vehicle model for id={vehicleAsset.id}");
                if (!vehicleIconInfo.PreviewOnly)
                {
                    ExportProgressTracker.CompleteOne($"Vehicle {vehicleAsset.id}: model not found", false);
                }
                return;
            }

            Layerer.relayer(vehicle, LayerMasks.VEHICLE);
            foreach (var weirdLookingObject in WeirdLookingObjects)
            {
                var child = vehicle.Find(weirdLookingObject);
                if (child != null)
                {
                    child.gameObject.SetActive(false);
                }
            }

            var rotors = vehicle.Find("Rotors");
            if (rotors != null)
            {
                for (var i = 0; i < rotors.childCount; i++)
                {
                    var rotor = rotors.GetChild(i);

                    var model0 = rotor.Find("Model_0");
                    var model1 = rotor.Find("Model_1");
                    if (model0 == null || model1 == null)
                    {
                        continue;
                    }

                    var rend0 = model0.GetComponent<Renderer>();
                    var rend1 = model1.GetComponent<Renderer>();
                    if (rend0 == null || rend1 == null)
                    {
                        continue;
                    }

                    var material0 = rend0.material;
                    var material1 = rend1.material;

                    if (vehicleAsset.requiredShaderUpgrade)
                    {
                        if (StandardShaderUtils.isMaterialUsingStandardShader(material0))
                        {
                            StandardShaderUtils.setModeToTransparent(material0);
                        }

                        if (StandardShaderUtils.isMaterialUsingStandardShader(material1))
                        {
                            StandardShaderUtils.setModeToTransparent(material1);
                        }
                    }

                    var color = material0.color;
                    color.a = 1f;
                    material0.color = color;

                    color.a = 0f;
                    material1.color = color;

                    rotor.localRotation = Quaternion.identity;
                }
            }

            var vehicleParent = new GameObject().transform;
            vehicle.SetParent(vehicleParent);

            vehicleParent.position = new Vector3(-256f, -256f, 0f);

            if (_camera == null)
            {
                _camera = Instantiate(new GameObject()).transform;
            }

            _camera.SetParent(vehicleParent, false);
            _camera.rotation = Quaternion.identity;

            if (!CustomImageTool.TryGetBoundingSphere(vehicleParent.gameObject, out var center, out var radius))
            {
                center = vehicleParent.position;
                radius = 1f;
            }

            // Rotate the model about its centre so it stays framed at any angle and any size.
            var spin = new GameObject().transform;
            spin.SetParent(vehicleParent, false);
            spin.position = center;
            vehicle.SetParent(spin, true);
            spin.localRotation = Quaternion.Euler(vehicleIconInfo.Angles);

            var orthographicSize = radius * CustomImageTool.FrameMargin;
            _camera.position = center - _camera.forward * (radius + 1f);

            var texture = CustomImageTool.CaptureIcon(vehicleAsset.id, 0, vehicle, _camera,
                vehicleIconInfo.Width, vehicleIconInfo.Height, orthographicSize, true, vehicleIconInfo.RenderOptions);
            var processed = ImageUtils.ApplyPostProcessing(texture, vehicleIconInfo.RenderOptions, true);

            try
            {
                if (vehicleIconInfo.PreviewOnly)
                {
                    vehicleIconInfo.OnPreviewTexture?.Invoke(ImageUtils.DuplicateReadable(processed));
                }
                else
                {
                    var path = $"{vehicleIconInfo.OutputPath}.png";
                    ReadWrite.writeBytes(path, false, false, processed.EncodeToPNG());
                    ExportProgressTracker.CompleteOne($"Vehicle {vehicleAsset.id}", true);
                }
            }
            catch (Exception ex)
            {
                if (!vehicleIconInfo.PreviewOnly)
                {
                    ExportProgressTracker.CompleteOne($"Vehicle {vehicleAsset.id}: error", false);
                }

                UnturnedLog.error($"UnturnedImagesGenerator: vehicle export failed ({vehicleAsset.id}): {ex.Message}");
            }
            finally
            {
                if (processed != texture)
                {
                    Destroy(processed);
                }

                Destroy(texture);

                _camera.SetParent(null);
                Destroy(vehicleParent.gameObject);
            }
        }
    }
}
