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

                // Internal state used to keep accumulating across passes/sessions — hidden so only the
                // human-readable overrides.yaml shows up in the folder. The Hidden attribute is cleared
                // before writing (Windows rejects writing to a hidden file) and re-applied afterwards.
                var statePath = Path.Combine(dir, ".overrides.json");
                if (File.Exists(statePath))
                {
                    try
                    {
                        File.SetAttributes(statePath, File.GetAttributes(statePath) & ~FileAttributes.Hidden);
                    }
                    catch
                    {
                        // ignored
                    }
                }

                File.WriteAllText(statePath, JsonConvert.SerializeObject(_mods, Formatting.Indented));

                try
                {
                    File.SetAttributes(statePath, File.GetAttributes(statePath) | FileAttributes.Hidden);
                }
                catch
                {
                    // attribute is cosmetic only
                }

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
            sb.AppendLine("# Pick the block that matches your plugin and paste it in as is.");
            sb.AppendLine();

            AppendMarketSection(sb);

            sb.AppendLine("# ---------------------------------------------------------------------------");
            sb.AppendLine("# UnturnedImages (the upstream CDN plugin) — its own config format.");
            sb.AppendLine("# Replace <YOUR_CDN_BASE> with your host.");
            sb.AppendLine("# ---------------------------------------------------------------------------");
            AppendSection(sb, "ItemOverrides", false, ImageExportPaths.ItemsCategory, "{ItemId}");
            AppendSection(sb, "VehicleOverrides", true, ImageExportPaths.VehiclesCategory, "{VehicleId}");

            return sb.ToString();
        }

        /// <summary>
        /// Paste-ready block for SAR.Czarek.Market / SAR.Garage: one <c>WorkshopRanges</c> entry per
        /// contiguous ID range, written with the indentation those configs use so it drops straight
        /// under <c>Market: -&gt; Images:</c>. Both configs key items and vehicles off the same list,
        /// so the two categories of a mod are merged here.
        /// </summary>
        private static void AppendMarketSection(StringBuilder sb)
        {
            var mods = new List<KeyValuePair<uint, List<ushort>>>();
            var skipped = new List<uint>();

            foreach (var pair in _mods)
            {
                // Those configs build their URLs from the numeric asset ID, so a mod exported with
                // GUID file names cannot be addressed by them at all.
                if (pair.Value.NamingMode == ExportNamingMode.GuidString)
                {
                    skipped.Add(pair.Key);

                    continue;
                }

                var ids = new List<ushort>();

                if (pair.Value.Items != null)
                {
                    ids.AddRange(pair.Value.Items);
                }

                if (pair.Value.Vehicles != null)
                {
                    ids.AddRange(pair.Value.Vehicles);
                }

                if (ids.Count == 0)
                {
                    continue;
                }

                mods.Add(new KeyValuePair<uint, List<ushort>>(pair.Key, ids));
            }

            if (mods.Count == 0 && skipped.Count == 0)
            {
                return;
            }

            mods.Sort((a, b) => a.Key.CompareTo(b.Key));
            skipped.Sort();

            sb.AppendLine("# ---------------------------------------------------------------------------");
            sb.AppendLine("# SAR.Czarek.Market / SAR.Garage — paste under  Market:  ->  Images:");
            sb.AppendLine("# The indentation below is already the one those configs expect.");
            sb.AppendLine("# ---------------------------------------------------------------------------");

            foreach (var modId in skipped)
            {
                sb.AppendLine($"# mod {modId} left out — exported with GUID file names, which these");
                sb.AppendLine("# configs cannot address. Re-export it with 'Name: ID' to get it here.");
            }

            if (mods.Count > 0)
            {
                sb.AppendLine("    WorkshopRanges:");

                foreach (var pair in mods)
                {
                    sb.AppendLine($"      # mod {pair.Key}");

                    foreach (var range in ImageUtils.GenerateRanges(pair.Value))
                    {
                        sb.AppendLine($"      - WorkshopId: {pair.Key}");
                        sb.AppendLine($"        LowestId: {range.Lowest}");
                        sb.AppendLine($"        HighestId: {range.Highest}");
                    }
                }
            }

            sb.AppendLine();
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
