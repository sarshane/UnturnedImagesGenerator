using System.Collections.Generic;
using System.IO;
using System.Text;
using SDG.Unturned;
using UnturnedImages.Module.Config;

namespace UnturnedImages.Module.Images
{
    /// <summary>
    /// Collects the asset IDs exported per workshop mod and writes one paste-ready YAML file per mod
    /// (<c>&lt;root&gt;/_Overrides/&lt;modId&gt;.yaml</c>) listing the ID ranges as
    /// <c>ItemOverrides</c> / <c>VehicleOverrides</c>, so they can be dropped straight into the
    /// UnturnedImages plugin config. Item and vehicle exports are separate passes, so the data is
    /// accumulated here and the file is rewritten with everything known so far after each pass.
    /// </summary>
    internal static class OverrideHintWriter
    {
        private sealed class ModData
        {
            public List<ushort>? Items;
            public List<ushort>? Vehicles;
        }

        private static readonly Dictionary<uint, ModData> Mods = new();

        /// <summary>Replaces the stored ID list for one category of a mod.</summary>
        public static void SetCategory(uint modId, bool isVehicle, List<ushort> ids)
        {
            if (!Mods.TryGetValue(modId, out var data))
            {
                data = new ModData();
                Mods[modId] = data;
            }

            if (isVehicle)
            {
                data.Vehicles = ids;
            }
            else
            {
                data.Items = ids;
            }
        }

        /// <summary>Writes <c>&lt;root&gt;/_Overrides/&lt;modId&gt;.yaml</c> with everything known for the mod.</summary>
        public static void Write(uint modId, string root, ExportNamingMode namingMode)
        {
            if (!Mods.TryGetValue(modId, out var data))
            {
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"# UnturnedImagesGenerator — ID ranges for workshop mod {modId}");
            sb.AppendLine("# Paste the sections below into your UnturnedImages plugin config.");
            sb.AppendLine("# Replace <YOUR_CDN_BASE> with where you host the images.");
            sb.AppendLine();

            AppendSection(sb, "ItemOverrides", data.Items, ImageExportPaths.ItemsCategory, modId, namingMode, true);
            AppendSection(sb, "VehicleOverrides", data.Vehicles, ImageExportPaths.VehiclesCategory, modId, namingMode,
                false);

            var directory = Path.Combine(root, "_Overrides");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, modId + ".yaml");
            File.WriteAllText(path, sb.ToString());

            UnturnedLog.info($"UnturnedImagesGenerator: wrote override hints {path}");
        }

        private static void AppendSection(StringBuilder sb, string key, List<ushort>? ids, string categoryFolder,
            uint modId, ExportNamingMode namingMode, bool item)
        {
            if (ids == null || ids.Count == 0)
            {
                return;
            }

            var ranges = ImageUtils.GenerateIdRanges(new List<ushort>(ids));
            var token = namingMode == ExportNamingMode.GuidString
                ? "{Guid}"
                : item
                    ? "{ItemId}"
                    : "{VehicleId}";

            sb.AppendLine($"{key}:");
            sb.AppendLine($"  - Id: \"{ranges}\"");
            sb.AppendLine($"    Repository: \"<YOUR_CDN_BASE>/{categoryFolder}/{ImageExportPaths.WorkshopSegment}/{modId}/{token}.png\"");
            sb.AppendLine();
        }
    }
}
