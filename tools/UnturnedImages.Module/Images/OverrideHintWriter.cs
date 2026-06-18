using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using SDG.Unturned;
using UnturnedImages.Module.Config;

namespace UnturnedImages.Module.Images
{
    /// <summary>
    /// Collects exported workshop asset IDs into a single, persistent
    /// <c>&lt;root&gt;/_Overrides/overrides.yaml</c> (ItemOverrides + VehicleOverrides for every mod).
    /// The accumulated state is saved to a JSON sidecar, so items and vehicles — and multiple mods —
    /// exported in separate passes or sessions keep adding up instead of overwriting each other.
    /// </summary>
    internal static class OverrideHintWriter
    {
        private sealed class ModData
        {
            public List<ushort>? Items { get; set; }
            public List<ushort>? Vehicles { get; set; }
            public ExportNamingMode NamingMode { get; set; }
        }

        private static Dictionary<uint, ModData> _mods = new();
        private static string? _loadedDir;

        /// <summary>Merges one category of a mod into the accumulated state (in memory).</summary>
        public static void Record(string root, uint modId, bool isVehicle, List<ushort> ids,
            ExportNamingMode namingMode)
        {
            Load(Path.Combine(root, "_Overrides"));

            if (!_mods.TryGetValue(modId, out var data))
            {
                data = new ModData();
                _mods[modId] = data;
            }

            if (isVehicle)
            {
                data.Vehicles = ids;
            }
            else
            {
                data.Items = ids;
            }

            data.NamingMode = namingMode;
        }

        /// <summary>Persists the accumulated state and rewrites the single overrides.yaml from all of it.</summary>
        public static void Flush(string root)
        {
            try
            {
                var dir = Path.Combine(root, "_Overrides");
                Load(dir);
                Directory.CreateDirectory(dir);

                File.WriteAllText(Path.Combine(dir, ".overrides.json"),
                    JsonConvert.SerializeObject(_mods, Formatting.Indented));

                var yamlPath = Path.Combine(dir, "overrides.yaml");
                File.WriteAllText(yamlPath, BuildYaml());

                UnturnedLog.info($"UnturnedImagesGenerator: updated {yamlPath}");
            }
            catch (Exception ex)
            {
                UnturnedLog.error("UnturnedImagesGenerator: could not write override hints: " + ex.Message);
            }
        }

        private static void Load(string dir)
        {
            if (_loadedDir == dir)
            {
                return;
            }

            _loadedDir = dir;
            _mods = new Dictionary<uint, ModData>();

            try
            {
                var statePath = Path.Combine(dir, ".overrides.json");
                if (File.Exists(statePath))
                {
                    _mods = JsonConvert.DeserializeObject<Dictionary<uint, ModData>>(File.ReadAllText(statePath))
                            ?? new Dictionary<uint, ModData>();
                }
            }
            catch
            {
                _mods = new Dictionary<uint, ModData>();
            }
        }

        private static string BuildYaml()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# UnturnedImagesGenerator — exported workshop ID ranges (all mods).");
            sb.AppendLine("# Paste the sections into your plugin config. Replace <YOUR_CDN_BASE> with your host.");
            sb.AppendLine();

            AppendSection(sb, "ItemOverrides", false, ImageExportPaths.ItemsCategory, "{ItemId}");
            AppendSection(sb, "VehicleOverrides", true, ImageExportPaths.VehiclesCategory, "{VehicleId}");

            return sb.ToString();
        }

        private static void AppendSection(StringBuilder sb, string key, bool vehicles, string categoryFolder,
            string idToken)
        {
            var any = false;

            foreach (var pair in _mods)
            {
                var ids = vehicles ? pair.Value.Vehicles : pair.Value.Items;
                if (ids == null || ids.Count == 0)
                {
                    continue;
                }

                if (!any)
                {
                    sb.AppendLine($"{key}:");
                    any = true;
                }

                var ranges = ImageUtils.GenerateIdRanges(new List<ushort>(ids));
                var token = pair.Value.NamingMode == ExportNamingMode.GuidString ? "{Guid}" : idToken;
                sb.AppendLine($"  # mod {pair.Key}");
                sb.AppendLine($"  - Id: \"{ranges}\"");
                sb.AppendLine(
                    $"    Repository: \"<YOUR_CDN_BASE>/{categoryFolder}/{ImageExportPaths.WorkshopSegment}/{pair.Key}/{token}.png\"");
            }

            if (any)
            {
                sb.AppendLine();
            }
        }
    }
}
