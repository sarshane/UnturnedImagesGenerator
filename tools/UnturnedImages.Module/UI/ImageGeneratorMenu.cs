using DanielWillett.UITools;
using SDG.Unturned;
using UnityEngine;
using UnturnedImages.Module.Config;
using UnturnedImages.Module.Images;

namespace UnturnedImages.Module.UI
{
    internal sealed class ImageGeneratorMenu : MonoBehaviour
    {
        private static ImageGeneratorMenu? _instance;

        private readonly ExportSettings _settings = ExportSettings.LoadOrDefaults();

        private ISleekElement? _root;
        private ISleekImage? _previewImage;
        private ISleekLabel? _statusLabel;
        private ISleekBox? _progressFill;
        private ISleekLabel? _previewInfoLabel;

        private ushort _previewAssetId;
        private uint _workshopFilter;
        private bool _previewVehicle = true;
        private bool _isOpen;
        private bool _vehiclePreviewDirty;
        private bool _vehiclePreviewPending;
        private bool _itemPreviewDirty;
        private bool _itemPreviewPending;

        private const int PreviewSize = 256;
        private const float ProgressWidth = 616f;

        private static readonly ColorPreset[] LightColorPresets =
        {
            new("Белый", Color.white),
            new("Тёплый", new Color(1f, 0.82f, 0.55f, 1f)),
            new("Холодный", new Color(0.65f, 0.82f, 1f, 1f)),
            new("Жёлтый", new Color(1f, 0.9f, 0.25f, 1f)),
            new("Красный", new Color(1f, 0.35f, 0.3f, 1f)),
            new("Зелёный", new Color(0.35f, 1f, 0.45f, 1f)),
            new("Синий", new Color(0.35f, 0.55f, 1f, 1f)),
            new("Фиолет", new Color(0.8f, 0.45f, 1f, 1f))
        };

        private static readonly ColorPreset[] BackgroundPresets =
        {
            new("Тёмный", new Color(0.12f, 0.12f, 0.14f, 1f)),
            new("Серый", new Color(0.5f, 0.5f, 0.5f, 1f)),
            new("Белый", Color.white),
            new("Чёрный", Color.black),
            new("Синий", new Color(0.15f, 0.2f, 0.35f, 1f)),
            new("Хромакей", new Color(0.1f, 0.7f, 0.2f, 1f))
        };

        public static void Load()
        {
            if (_instance != null)
            {
                return;
            }

            UnturnedUIToolsNexus.InitializeIfNotStandalone();
            _instance = UnturnedImagesModule.Instance!.GameObject!.AddComponent<ImageGeneratorMenu>();
        }

        public static void Unload()
        {
            if (_instance == null)
            {
                return;
            }

            Destroy(_instance);
            _instance = null;
        }

        private void Awake()
        {
            ImageUtils.EnsureExportDirectories();
            _workshopFilter = _settings.WorkshopModIdFilter;
        }

        private void OnDestroy()
        {
            Close();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F10))
            {
                Toggle();
            }

            if (!_isOpen)
            {
                return;
            }

            try
            {
                RefreshProgress();
                RefreshPreview();
                PumpVehiclePreview();
                PumpItemPreview();
            }
            catch (System.Exception ex)
            {
                UnturnedLog.error("UnturnedImagesGenerator: menu update failed: " + ex);
            }
        }

        private void Toggle()
        {
            if (_isOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        private void Open()
        {
            if (_isOpen)
            {
                return;
            }

            var parent = GetUiParent();
            if (parent == null)
            {
                UnturnedLog.error("UnturnedImagesGenerator: UI parent is not ready yet.");
                return;
            }

            _root = CreateUi(parent);
            _isOpen = true;
            _vehiclePreviewDirty = true;
            _itemPreviewDirty = true;
            RefreshProgress();
            RefreshPreview(force: true);
        }

        private void Close()
        {
            if (_root?.Parent != null)
            {
                _root.Parent.RemoveChild(_root);
            }

            _root = null;
            _previewImage = null;
            _statusLabel = null;
            _progressFill = null;
            _previewInfoLabel = null;
            _isOpen = false;
        }

        private static ISleekElement? GetUiParent()
        {
            if (PlayerUI.window != null)
            {
                return PlayerUI.window;
            }

            if (MenuUI.window != null)
            {
                return MenuUI.window;
            }

            return null;
        }

        private ISleekElement CreateUi(ISleekElement parent)
        {
            const float c1 = 18f;
            const float c2 = 290f;
            const float c3 = 606f;
            const float half = 146f;
            const float gap = 154f;

            var glazier = Glazier.Get();
            var root = glazier.CreateBox();
            SetTransform(root, 24f, 40f, 916f, 600f);
            root.BackgroundColor = ESleekTint.BACKGROUND;
            parent.AddChild(root);

            AddLabel(root, "Unturned Images Generator", c1, 12f, 470f, 28f, ESleekFontSize.Medium);
            AddButton(root, "Закрыть (F10)", 790f, 12f, 108f, 28f, _ => Close());

            // ---- Column 1: preview ----
            AddLabel(root, "Превью", c1, 50f, 250f, 24f, ESleekFontSize.Medium);
            var imageFrame = glazier.CreateBox();
            SetTransform(imageFrame, c1, 80f, 250f, 250f);
            imageFrame.BackgroundColor = ESleekTint.FOREGROUND;
            root.AddChild(imageFrame);

            _previewImage = glazier.CreateImage();
            SetTransform(_previewImage, 5f, 5f, 240f, 240f);
            imageFrame.AddChild(_previewImage);

            _previewInfoLabel = AddLabel(root, "ID: 0", c1, 336f, 250f, 24f);
            AddNumeric(root, "ID для превью", c1, 366f, _previewAssetId, value =>
            {
                _previewAssetId = value;
                _itemPreviewDirty = true;
                RefreshPreview(force: true);
            });
            AddButton(root, _previewVehicle ? "Режим: транспорт" : "Режим: предмет", c1, 402f, 250f, 32f, _ =>
            {
                _previewVehicle = !_previewVehicle;
                _itemPreviewDirty = true;
                Rebuild();
            });
            AddButton(root, "Снять PNG-превью", c1, 438f, 250f, 32f, _ => QueuePreviewPng());

            // ---- Column 2: pose, camera, light ----
            AddLabel(root, "Поза и камера", c2, 50f, 300f, 24f, ESleekFontSize.Medium);
            AddAngleSlider(root, "Поворот X", c2, 80f, CurrentAngles.x, value =>
            {
                SetAngle(0, value);
                RefreshPreview(force: true);
            });
            AddAngleSlider(root, "Поворот Y", c2, 116f, CurrentAngles.y, value =>
            {
                SetAngle(1, value);
                RefreshPreview(force: true);
            });
            AddAngleSlider(root, "Поворот Z", c2, 152f, CurrentAngles.z, value =>
            {
                SetAngle(2, value);
                RefreshPreview(force: true);
            });
            AddRangeSlider(root, "Зум", c2, 188f, _settings.CameraZoom, 0.5f, 2.5f, value =>
            {
                _settings.CameraZoom = value;
                RefreshPreview(force: true);
            });

            AddLabel(root, "Свет", c2, 226f, 300f, 24f, ESleekFontSize.Medium);
            AddRangeSlider(root, "Key X", c2, 256f, _settings.KeyLightX, -5f, 5f, value =>
            {
                _settings.KeyLightX = value;
                _itemPreviewDirty = true;
                RefreshPreview(force: true);
            });
            AddRangeSlider(root, "Key Y", c2, 292f, _settings.KeyLightY, -5f, 5f, value =>
            {
                _settings.KeyLightY = value;
                _itemPreviewDirty = true;
                RefreshPreview(force: true);
            });
            AddRangeSlider(root, "Key Z", c2, 328f, _settings.KeyLightZ, -5f, 5f, value =>
            {
                _settings.KeyLightZ = value;
                _itemPreviewDirty = true;
                RefreshPreview(force: true);
            });
            AddRangeSlider(root, "Rim X", c2, 364f, _settings.RimLightX, -5f, 5f, value =>
            {
                _settings.RimLightX = value;
                _itemPreviewDirty = true;
                RefreshPreview(force: true);
            });
            AddRangeSlider(root, "Rim Y", c2, 400f, _settings.RimLightY, -5f, 5f, value =>
            {
                _settings.RimLightY = value;
                _itemPreviewDirty = true;
                RefreshPreview(force: true);
            });
            AddRangeSlider(root, "Rim Z", c2, 436f, _settings.RimLightZ, -5f, 5f, value =>
            {
                _settings.RimLightZ = value;
                _itemPreviewDirty = true;
                RefreshPreview(force: true);
            });
            AddButton(root, GetKeyColorText(), c2, 472f, half, 30f, _ =>
            {
                SetKeyLightColor(NextColor(GetKeyLightColor()));
                _itemPreviewDirty = true;
                Rebuild();
            });
            AddButton(root, GetRimColorText(), c2 + gap, 472f, half, 30f, _ =>
            {
                SetRimLightColor(NextColor(GetRimLightColor()));
                _itemPreviewDirty = true;
                Rebuild();
            });
            AddButton(root, ToggleText("Directional-свет", _settings.UseDirectionalLight), c2, 508f, 300f, 30f, _ =>
            {
                _settings.UseDirectionalLight = !_settings.UseDirectionalLight;
                _itemPreviewDirty = true;
                Rebuild();
            });

            // ---- Column 3: image + export ----
            AddLabel(root, "Изображение", c3, 50f, 300f, 24f, ESleekFontSize.Medium);
            AddNumeric(root, "Размер PNG", c3, 80f, (ushort)Mathf.Clamp(_settings.ImageSize, 64, 4096), value =>
            {
                _settings.ImageSize = value;
                _settings.NormalizeConstraints();
            });
            AddButton(root, $"Сглаживание: {_settings.SupersamplingScale}x", c3, 116f, 300f, 30f, _ =>
            {
                _settings.SupersamplingScale = _settings.SupersamplingScale >= 4 ? 1 : _settings.SupersamplingScale * 2;
                Rebuild();
            });
            AddButton(root, GetNamingText(), c3, 152f, half, 30f, _ =>
            {
                _settings.NamingMode = _settings.NamingMode == ExportNamingMode.AssetId
                    ? ExportNamingMode.GuidString
                    : ExportNamingMode.AssetId;
                Rebuild();
            });
            AddButton(root, GetItemModeText(), c3 + gap, 152f, half, 30f, _ =>
            {
                _settings.ItemIconMode = _settings.ItemIconMode == ItemIconExportMode.Rendered3D
                    ? ItemIconExportMode.VanillaUiIcon
                    : ItemIconExportMode.Rendered3D;
                Rebuild();
            });
            AddButton(root, ToggleText("Тень", _settings.UseShadowCatcher), c3, 188f, half, 30f, _ =>
            {
                _settings.UseShadowCatcher = !_settings.UseShadowCatcher;
                _itemPreviewDirty = true;
                Rebuild();
            });
            AddButton(root, ToggleText("Обрезка", _settings.TrimTransparentPadding), c3 + gap, 188f, half, 30f, _ =>
            {
                _settings.TrimTransparentPadding = !_settings.TrimTransparentPadding;
                _itemPreviewDirty = true;
                Rebuild();
            });
            AddRangeSlider(root, "Отступ", c3, 224f, _settings.TrimPaddingFraction, 0f, 0.45f, value =>
            {
                _settings.TrimPaddingFraction = value;
                RefreshPreview(force: true);
            });
            AddButton(root, ToggleText("Фон", _settings.UseSolidBackground), c3, 260f, half, 30f, _ =>
            {
                _settings.UseSolidBackground = !_settings.UseSolidBackground;
                _itemPreviewDirty = true;
                Rebuild();
            });
            AddButton(root, GetBackgroundColorText(), c3 + gap, 260f, half, 30f, _ =>
            {
                SetBackgroundColor(NextBackgroundColor(GetBackgroundColor()));
                _itemPreviewDirty = true;
                Rebuild();
            });

            AddLabel(root, "Экспорт", c3, 298f, 300f, 24f, ESleekFontSize.Medium);
            AddButton(root, ToggleText("Официальные", _settings.ExportOfficialAssets), c3, 328f, half, 30f, _ =>
            {
                _settings.ExportOfficialAssets = !_settings.ExportOfficialAssets;
                Rebuild();
            });
            AddButton(root, ToggleText("Workshop", _settings.ExportWorkshopAssets), c3 + gap, 328f, half, 30f, _ =>
            {
                _settings.ExportWorkshopAssets = !_settings.ExportWorkshopAssets;
                Rebuild();
            });
            AddNumeric32(root, "Workshop ID", c3, 364f, _workshopFilter, value =>
            {
                _workshopFilter = value;
                _settings.WorkshopModIdFilter = value;
            });
            AddButton(root, "Экспорт: Предметы", c3, 400f, half, 34f, _ =>
            {
                ApplyAndSave();
                ImageUtils.CaptureAllItemImages(_settings);
            });
            AddButton(root, "Экспорт: Транспорт", c3 + gap, 400f, half, 34f, _ =>
            {
                ApplyAndSave();
                ImageUtils.CaptureAllVehicleImages(_settings);
            });
            AddButton(root, "Экспорт workshop-мода", c3, 438f, 300f, 34f, _ =>
            {
                ApplyAndSave();
                if (_workshopFilter == 0)
                {
                    UnturnedLog.error("UnturnedImagesGenerator: Workshop ID должен быть не 0.");
                    return;
                }

                ImageUtils.CaptureItemsForWorkshopMod(_workshopFilter, _settings);
                ImageUtils.CaptureVehiclesForWorkshopMod(_workshopFilter, _settings);
            });
            AddButton(root, "Отмена экспорта", c3, 476f, 300f, 30f, _ => CancelExport());

            // ---- Footer: progress + hint ----
            AddLabel(root, "Папка вывода задаётся в settings.json", c1, 508f, 250f, 22f);

            var progressBack = glazier.CreateBox();
            SetTransform(progressBack, c2, 548f, ProgressWidth, 16f);
            progressBack.BackgroundColor = ESleekTint.FOREGROUND;
            root.AddChild(progressBack);

            _progressFill = glazier.CreateBox();
            SetTransform(_progressFill, 0f, 0f, 0f, 16f);
            _progressFill.BackgroundColor = SleekColor.BackgroundIfLight(new Color(0.2f, 0.85f, 0.35f, 1f));
            progressBack.AddChild(_progressFill);

            _statusLabel = AddLabel(root, ExportProgressTracker.StatusText, c2, 568f, ProgressWidth, 22f);

            return root;
        }

        private void Rebuild()
        {
            if (!_isOpen)
            {
                return;
            }

            Close();
            Open();
        }

        private void ApplyAndSave()
        {
            _settings.WorkshopModIdFilter = _workshopFilter;
            _settings.NormalizeConstraints();
            _settings.Save();
        }

        private void QueuePreviewPng()
        {
            if (_previewVehicle)
            {
                var vehicle = Assets.find(EAssetType.VEHICLE, _previewAssetId) as VehicleAsset;
                if (vehicle == null)
                {
                    UnturnedLog.error($"UnturnedImagesGenerator: vehicle {_previewAssetId} not found.");
                    return;
                }

                CustomVehicleTool.QueueVehicleIcon(vehicle, string.Empty, _settings.ImageSize, _settings.ImageSize,
                    _settings.VehicleEulerDegrees, _settings.ToRenderOptions(), true, texture =>
                    {
                        if (_previewImage != null)
                        {
                            _previewImage.SetTextureAndShouldDestroy(texture, true);
                        }
                    });
                return;
            }

            var item = Assets.find(EAssetType.ITEM, _previewAssetId) as ItemAsset;
            if (item == null)
            {
                UnturnedLog.error($"UnturnedImagesGenerator: item {_previewAssetId} not found.");
                return;
            }

            CustomItemTool.QueueRenderedIcon(new CustomItemTool.RenderedIconJob(item, _settings.ItemSkinId, string.Empty,
                _settings.ImageSize, _settings.ImageSize, _settings.ItemExtraEulerDegrees, true, texture =>
                {
                    if (_previewImage != null)
                    {
                        _previewImage.SetTextureAndShouldDestroy(texture, true);
                    }
                }, _settings.SupersamplingScale));
        }

        private void RefreshProgress()
        {
            if (_statusLabel != null)
            {
                _statusLabel.Text = ExportProgressTracker.StatusText;
            }

            if (_progressFill != null)
            {
                _progressFill.SizeOffset_X = ProgressWidth * ExportProgressTracker.Progress;
            }
        }

        private void RefreshPreview(bool force = false)
        {
            if (_previewInfoLabel != null)
            {
                if (_previewVehicle)
                {
                    var vehicle = Assets.find(EAssetType.VEHICLE, _previewAssetId) as VehicleAsset;
                    _previewInfoLabel.Text = vehicle == null
                        ? $"Транспорт {_previewAssetId} не найден"
                        : $"Транспорт {_previewAssetId} | поворот {FormatAngles(_settings.VehicleEulerDegrees)}";
                }
                else
                {
                    var item = Assets.find(EAssetType.ITEM, _previewAssetId) as ItemAsset;
                    _previewInfoLabel.Text = item == null
                        ? $"Предмет {_previewAssetId} не найден"
                        : $"Предмет {_previewAssetId} | поворот {FormatAngles(_settings.ItemExtraEulerDegrees)}";
                }
            }

            if (!force)
            {
                return;
            }

            if (_previewVehicle)
            {
                _vehiclePreviewDirty = true;
            }
            else
            {
                _itemPreviewDirty = true;
            }
        }

        // The live preview renders through the SAME path as export (CustomVehicleTool, preview mode),
        // so what you see is exactly what gets generated. One render is in flight at a time.
        private void PumpVehiclePreview()
        {
            if (!_previewVehicle || !_vehiclePreviewDirty || _vehiclePreviewPending)
            {
                return;
            }

            var vehicle = Assets.find(EAssetType.VEHICLE, _previewAssetId) as VehicleAsset;
            if (vehicle == null)
            {
                _vehiclePreviewDirty = false;
                return;
            }

            _vehiclePreviewDirty = false;
            _vehiclePreviewPending = true;

            CustomVehicleTool.QueueVehicleIcon(vehicle, string.Empty, PreviewSize, PreviewSize,
                _settings.VehicleEulerDegrees, _settings.ToRenderOptions(), true, texture =>
                {
                    _vehiclePreviewPending = false;
                    if (_previewVehicle && _isOpen && _previewImage != null)
                    {
                        _previewImage.SetTextureAndShouldDestroy(texture, true);
                    }
                    else
                    {
                        Destroy(texture);
                    }
                });
        }

        private void PumpItemPreview()
        {
            if (_previewVehicle || !_itemPreviewDirty || _itemPreviewPending)
            {
                return;
            }

            var item = Assets.find(EAssetType.ITEM, _previewAssetId) as ItemAsset;
            if (item == null)
            {
                _itemPreviewDirty = false;
                return;
            }

            _itemPreviewDirty = false;
            _itemPreviewPending = true;

            CustomItemTool.QueueRenderedIcon(new CustomItemTool.RenderedIconJob(item, _settings.ItemSkinId,
                string.Empty, PreviewSize, PreviewSize, _settings.ItemExtraEulerDegrees, true, texture =>
                {
                    _itemPreviewPending = false;
                    if (!_previewVehicle && _isOpen && _previewImage != null)
                    {
                        _previewImage.SetTextureAndShouldDestroy(texture, true);
                    }
                    else
                    {
                        Destroy(texture);
                    }
                }, 1, _settings.ToRenderOptions()));
        }

        private static string FormatAngles(Vector3 angles)
        {
            return $"X {angles.x:0} Y {angles.y:0} Z {angles.z:0}";
        }

        private Vector3 CurrentAngles =>
            _previewVehicle ? _settings.VehicleEulerDegrees : _settings.ItemExtraEulerDegrees;

        private void SetAngle(int axis, float value)
        {
            var angles = CurrentAngles;
            angles[axis] = value;

            if (_previewVehicle)
            {
                _settings.VehicleEulerDegrees = angles;
            }
            else
            {
                _settings.ItemExtraEulerDegrees = angles;
            }
        }

        private void CancelExport()
        {
            CustomItemTool.ClearQueues();
            CustomVehicleTool.ClearQueues();
            ExportProgressTracker.Reset("Отменено");
            RefreshProgress();
        }

        private Color GetKeyLightColor()
        {
            return new Color(_settings.KeyLightR, _settings.KeyLightG, _settings.KeyLightB, 1f);
        }

        private Color GetRimLightColor()
        {
            return new Color(_settings.RimLightR, _settings.RimLightG, _settings.RimLightB, 1f);
        }

        private void SetKeyLightColor(Color color)
        {
            _settings.KeyLightR = color.r;
            _settings.KeyLightG = color.g;
            _settings.KeyLightB = color.b;
        }

        private void SetRimLightColor(Color color)
        {
            _settings.RimLightR = color.r;
            _settings.RimLightG = color.g;
            _settings.RimLightB = color.b;
        }

        private Color GetBackgroundColor()
        {
            return new Color(_settings.BackgroundR, _settings.BackgroundG, _settings.BackgroundB, 1f);
        }

        private void SetBackgroundColor(Color color)
        {
            _settings.BackgroundR = color.r;
            _settings.BackgroundG = color.g;
            _settings.BackgroundB = color.b;
        }

        private string GetBackgroundColorText()
        {
            return $"Фон: {GetBackgroundName(GetBackgroundColor())}";
        }

        private static string GetBackgroundName(Color color)
        {
            foreach (var preset in BackgroundPresets)
            {
                if (Approximately(color, preset.Color))
                {
                    return preset.Name;
                }
            }

            return "Custom";
        }

        private static Color NextBackgroundColor(Color current)
        {
            for (var i = 0; i < BackgroundPresets.Length; i++)
            {
                if (!Approximately(current, BackgroundPresets[i].Color))
                {
                    continue;
                }

                return BackgroundPresets[(i + 1) % BackgroundPresets.Length].Color;
            }

            return BackgroundPresets[0].Color;
        }

        private string GetKeyColorText()
        {
            return $"Key: {GetColorName(GetKeyLightColor())}";
        }

        private string GetRimColorText()
        {
            return $"Rim: {GetColorName(GetRimLightColor())}";
        }

        private static Color NextColor(Color current)
        {
            for (var i = 0; i < LightColorPresets.Length; i++)
            {
                if (!Approximately(current, LightColorPresets[i].Color))
                {
                    continue;
                }

                return LightColorPresets[(i + 1) % LightColorPresets.Length].Color;
            }

            return LightColorPresets[0].Color;
        }

        private static string GetColorName(Color color)
        {
            foreach (var preset in LightColorPresets)
            {
                if (Approximately(color, preset.Color))
                {
                    return preset.Name;
                }
            }

            return "Custom";
        }

        private static bool Approximately(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.01f &&
                   Mathf.Abs(a.g - b.g) < 0.01f &&
                   Mathf.Abs(a.b - b.b) < 0.01f;
        }

        private static ISleekLabel AddLabel(ISleekElement parent, string text, float x, float y, float w, float h,
            ESleekFontSize fontSize = ESleekFontSize.Small)
        {
            var label = Glazier.Get().CreateLabel();
            SetTransform(label, x, y, w, h);
            label.Text = text;
            label.FontSize = fontSize;
            label.TextAlignment = TextAnchor.MiddleLeft;
            parent.AddChild(label);
            return label;
        }

        private static ISleekButton AddButton(ISleekElement parent, string text, float x, float y, float w, float h,
            ClickedButton onClicked)
        {
            var button = Glazier.Get().CreateButton();
            SetTransform(button, x, y, w, h);
            button.Text = text;
            button.FontSize = ESleekFontSize.Small;
            button.BackgroundColor = ESleekTint.BACKGROUND;
            button.OnClicked += onClicked;
            parent.AddChild(button);
            return button;
        }

        private static void AddNumeric(ISleekElement parent, string label, float x, float y, ushort value,
            System.Action<ushort> changed)
        {
            AddLabel(parent, label, x, y, 120f, 24f);
            var field = Glazier.Get().CreateUInt16Field();
            SetTransform(field, x + 124f, y, 72f, 26f);
            field.Value = value;
            field.OnValueChanged += (_, newValue) => changed(newValue);
            parent.AddChild(field);
        }

        private static void AddNumeric32(ISleekElement parent, string label, float x, float y, uint value,
            System.Action<uint> changed)
        {
            AddLabel(parent, label, x, y, 120f, 24f);
            var field = Glazier.Get().CreateUInt32Field();
            SetTransform(field, x + 124f, y, 72f, 26f);
            field.Value = value;
            field.OnValueChanged += (_, newValue) => changed(newValue);
            parent.AddChild(field);
        }

        private static void AddAngleSlider(ISleekElement parent, string label, float x, float y, float degrees,
            System.Action<float> changed)
        {
            AddLabel(parent, label, x, y, 24f, 24f);

            var slider = Glazier.Get().CreateSlider();
            SetTransform(slider, x + 28f, y, 208f, 20f);
            slider.Orientation = ESleekOrientation.HORIZONTAL;
            slider.Value = Mathf.InverseLerp(-180f, 180f, degrees);
            parent.AddChild(slider);

            var valueLabel = AddLabel(parent, $"{degrees:0}", x + 242f, y - 2f, 46f, 24f);
            slider.OnValueChanged += (_, value) =>
            {
                var degreesValue = Mathf.Lerp(-180f, 180f, value);
                valueLabel.Text = $"{degreesValue:0}";
                changed(degreesValue);
            };
        }

        private static void AddRangeSlider(ISleekElement parent, string label, float x, float y, float value,
            float min, float max, System.Action<float> changed)
        {
            AddLabel(parent, label, x, y, 28f, 24f);

            var slider = Glazier.Get().CreateSlider();
            SetTransform(slider, x + 32f, y, 204f, 20f);
            slider.Orientation = ESleekOrientation.HORIZONTAL;
            slider.Value = Mathf.InverseLerp(min, max, value);
            parent.AddChild(slider);

            var valueLabel = AddLabel(parent, $"{value:0.0}", x + 242f, y - 2f, 46f, 24f);
            slider.OnValueChanged += (_, sliderValue) =>
            {
                var rangeValue = Mathf.Lerp(min, max, sliderValue);
                valueLabel.Text = $"{rangeValue:0.0}";
                changed(rangeValue);
            };
        }

        private static void SetTransform(ISleekElement element, float x, float y, float width, float height)
        {
            element.PositionOffset_X = x;
            element.PositionOffset_Y = y;
            element.PositionScale_X = 0f;
            element.PositionScale_Y = 0f;
            element.SizeOffset_X = width;
            element.SizeOffset_Y = height;
            element.SizeScale_X = 0f;
            element.SizeScale_Y = 0f;
        }

        private static string GetNamingText()
        {
            return _instance?._settings.NamingMode == ExportNamingMode.GuidString ? "Имя: GUID" : "Имя: ID";
        }

        private static string GetItemModeText()
        {
            return _instance?._settings.ItemIconMode == ItemIconExportMode.VanillaUiIcon
                ? "Предмет: иконка"
                : "Предмет: 3D";
        }

        private static string ToggleText(string name, bool value)
        {
            return value ? $"{name}: да" : $"{name}: нет";
        }

        private readonly struct ColorPreset
        {
            public ColorPreset(string name, Color color)
            {
                Name = name;
                Color = color;
            }

            public string Name { get; }

            public Color Color { get; }
        }
    }
}
