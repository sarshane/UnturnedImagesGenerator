using SDG.Unturned;
using UnturnedImages.Module.Config;

namespace UnturnedImages.Module.Images
{
    internal static class AssetNaming
    {
        internal static string GetFileBaseName(Asset asset, ExportNamingMode mode)
        {
            return mode == ExportNamingMode.GuidString
                ? asset.GUID.ToString("N")
                : asset.id.ToString();
        }
    }
}
