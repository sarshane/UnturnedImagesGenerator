using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Linq;

namespace UnturnedImages.Module.Workshop
{
    public static class WorkshopHelper
    {
        private const string WorkshopPathIndicator = "steamapps/workshop/content/304930/";

        public static bool IsWorkshop(Asset asset)
        {
            // Asset.assetOrigin/EAssetOrigin are marked obsolete in newer Unturned builds, but the
            // replacement AssetOrigin API is not present across all supported game versions. The
            // path-based check below is the robust fallback, so the obsolete check is kept and
            // its warning suppressed locally rather than project-wide.
#pragma warning disable CS0618
            return asset.assetOrigin == EAssetOrigin.WORKSHOP ||
                   asset.absoluteOriginFilePath.Contains(WorkshopPathIndicator);
#pragma warning restore CS0618
        }

        public static uint GetWorkshopId(Asset asset)
        {
            var originFilePath = asset.absoluteOriginFilePath.Replace('\\', '/');

            var index = originFilePath.IndexOf(WorkshopPathIndicator, StringComparison.Ordinal);

            if (index < 0)
            {
                throw new Exception($"Workshop ID could not be found for asset {asset.id} ({asset.assetCategory}) ({asset.absoluteOriginFilePath})");
            }

            var cutStr = originFilePath.Substring(index + WorkshopPathIndicator.Length);

            var workshopIdStr = new string(cutStr.TakeWhile(char.IsNumber).ToArray());

            if (!uint.TryParse(workshopIdStr, out var workshopId))
            {
                throw new Exception($"Workshop ID could not be parsed for asset {asset.id} ({asset.assetCategory}) ({asset.absoluteOriginFilePath})");
            }

            return workshopId;
        }

        public static uint GetWorkshopIdSafe(Asset asset)
        {
            return !IsWorkshop(asset) ? 0 : GetWorkshopId(asset);
        }

        public static uint[] GetAllMods()
        {
            var mods = new List<uint>();

            mods.AddRange(Assets.find(EAssetType.ITEM).Select(GetWorkshopIdSafe).Distinct());
            mods.AddRange(Assets.find(EAssetType.VEHICLE).Select(GetWorkshopIdSafe).Distinct());

            return mods.Distinct().ToArray();
        }
    }
}
