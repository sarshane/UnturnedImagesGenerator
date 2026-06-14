using System;
using System.IO;
using Newtonsoft.Json;
using SDG.Unturned;
using UnityEngine;
using UnturnedImages.Module.Images;

namespace UnturnedImages.Module.Config
{
    [Serializable]
    public sealed class ExportSettings
    {
        public ExportNamingMode NamingMode { get; set; } = ExportNamingMode.AssetId;

        public ItemIconExportMode ItemIconMode { get; set; } = ItemIconExportMode.Rendered3D;

        public bool ExportOfficialAssets { get; set; } = true;

        public bool ExportWorkshopAssets { get; set; } = true;

        /// <summary>
        /// When non-zero, only workshop assets from this mod ID are exported (official still follows <see cref="ExportOfficialAssets"/>).
        /// </summary>
        public uint WorkshopModIdFilter { get; set; }

        public int ImageSize { get; set; } = 1024;

        public int SupersamplingScale { get; set; } = 2;

        public float CameraZoom { get; set; } = 1f;

        public float CameraOffsetX { get; set; }

        public float CameraOffsetY { get; set; }

        public ushort ItemSkinId { get; set; }

        public float VehicleEulerX { get; set; } = 10f;

        public float VehicleEulerY { get; set; } = 135f;

        public float VehicleEulerZ { get; set; } = -10f;

        [JsonIgnore]
        public Vector3 VehicleEulerDegrees
        {
            get => new(VehicleEulerX, VehicleEulerY, VehicleEulerZ);
            set
            {
                VehicleEulerX = value.x;
                VehicleEulerY = value.y;
                VehicleEulerZ = value.z;
            }
        }

        /// <summary>
        /// Applied after the vanilla item pose from <see cref="ItemTool.getItem"/> (degrees).
        /// </summary>
        public float ItemExtraEulerX { get; set; }

        public float ItemExtraEulerY { get; set; }

        public float ItemExtraEulerZ { get; set; }

        [JsonIgnore]
        public Vector3 ItemExtraEulerDegrees
        {
            get => new(ItemExtraEulerX, ItemExtraEulerY, ItemExtraEulerZ);
            set
            {
                ItemExtraEulerX = value.x;
                ItemExtraEulerY = value.y;
                ItemExtraEulerZ = value.z;
            }
        }

        public bool WriteUnturnedImagesYamlHints { get; set; } = true;

        public float KeyLightX { get; set; } = 1.5f;

        public float KeyLightY { get; set; } = 2.2f;

        public float KeyLightZ { get; set; } = -2.5f;

        public float KeyLightIntensity { get; set; } = 1.2f;

        public float KeyLightR { get; set; } = 1f;

        public float KeyLightG { get; set; } = 1f;

        public float KeyLightB { get; set; } = 1f;

        public float RimLightX { get; set; } = -2.2f;

        public float RimLightY { get; set; } = 1.6f;

        public float RimLightZ { get; set; } = 2.6f;

        public float RimLightIntensity { get; set; } = 0.55f;

        public float RimLightR { get; set; } = 0.75f;

        public float RimLightG { get; set; } = 0.85f;

        public float RimLightB { get; set; } = 1f;

        public bool UseShadowCatcher { get; set; } = true;

        public bool UseDirectionalLight { get; set; }

        public bool TrimTransparentPadding { get; set; }

        public float TrimPaddingFraction { get; set; } = 0.06f;

        public bool UseSolidBackground { get; set; }

        public float BackgroundR { get; set; } = 0.12f;

        public float BackgroundG { get; set; } = 0.12f;

        public float BackgroundB { get; set; } = 0.14f;

        /// <summary>
        /// When set, exported images go here instead of Unturned/Extras/UnturnedImagesGenerator.
        /// </summary>
        public string OutputDirectoryOverride { get; set; } = string.Empty;

        public IconRenderOptions ToRenderOptions()
        {
            return new IconRenderOptions
            {
                SupersamplingScale = SupersamplingScale,
                CameraZoom = CameraZoom,
                CameraOffsetX = CameraOffsetX,
                CameraOffsetY = CameraOffsetY,
                KeyLightPosition = new Vector3(KeyLightX, KeyLightY, KeyLightZ),
                KeyLightIntensity = KeyLightIntensity,
                KeyLightColor = new Color(KeyLightR, KeyLightG, KeyLightB, 1f),
                RimLightPosition = new Vector3(RimLightX, RimLightY, RimLightZ),
                RimLightIntensity = RimLightIntensity,
                RimLightColor = new Color(RimLightR, RimLightG, RimLightB, 1f),
                UseShadowCatcher = UseShadowCatcher,
                UseDirectionalLight = UseDirectionalLight,
                TrimTransparentPadding = TrimTransparentPadding,
                TrimPaddingFraction = TrimPaddingFraction,
                UseSolidBackground = UseSolidBackground,
                BackgroundColor = new Color(BackgroundR, BackgroundG, BackgroundB, 1f)
            };
        }

        public static string SettingsDirectory =>
            Path.Combine(ReadWrite.PATH, "Extras", ImageExportPaths.RootFolderName);

        public static string SettingsFilePath => Path.Combine(SettingsDirectory, "settings.json");

        public static ExportSettings LoadOrDefaults()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    var parsed = JsonConvert.DeserializeObject<ExportSettings>(json);
                    if (parsed != null)
                    {
                        parsed.Clamp();
                        return parsed;
                    }
                }
            }
            catch (Exception ex)
            {
                UnturnedLog.warn("UnturnedImagesGenerator: could not load settings: " + ex.Message);
            }

            var fresh = new ExportSettings();
            fresh.Clamp();
            return fresh;
        }

        public void Save()
        {
            try
            {
                Clamp();
                Directory.CreateDirectory(SettingsDirectory);
                var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                UnturnedLog.warn("UnturnedImagesGenerator: could not save settings: " + ex.Message);
            }
        }

        public void NormalizeConstraints()
        {
            Clamp();
        }

        private void Clamp()
        {
            if (ImageSize < 64)
            {
                ImageSize = 64;
            }

            if (ImageSize > 4096)
            {
                ImageSize = 4096;
            }

            if (SupersamplingScale < 1)
            {
                SupersamplingScale = 1;
            }

            if (SupersamplingScale > 4)
            {
                SupersamplingScale = 4;
            }

            if (SupersamplingScale == 3)
            {
                SupersamplingScale = 4;
            }

            if (CameraZoom < 0.5f)
            {
                CameraZoom = 0.5f;
            }

            if (CameraZoom > 2.5f)
            {
                CameraZoom = 2.5f;
            }

            CameraOffsetX = Mathf.Clamp(CameraOffsetX, -0.8f, 0.8f);
            CameraOffsetY = Mathf.Clamp(CameraOffsetY, -0.8f, 0.8f);

            if (KeyLightIntensity < 0f)
            {
                KeyLightIntensity = 0f;
            }

            if (RimLightIntensity < 0f)
            {
                RimLightIntensity = 0f;
            }

            KeyLightR = Mathf.Clamp01(KeyLightR);
            KeyLightG = Mathf.Clamp01(KeyLightG);
            KeyLightB = Mathf.Clamp01(KeyLightB);
            RimLightR = Mathf.Clamp01(RimLightR);
            RimLightG = Mathf.Clamp01(RimLightG);
            RimLightB = Mathf.Clamp01(RimLightB);

            TrimPaddingFraction = Mathf.Clamp(TrimPaddingFraction, 0f, 0.45f);
            BackgroundR = Mathf.Clamp01(BackgroundR);
            BackgroundG = Mathf.Clamp01(BackgroundG);
            BackgroundB = Mathf.Clamp01(BackgroundB);

            OutputDirectoryOverride = OutputDirectoryOverride?.Trim() ?? string.Empty;
        }
    }
}
