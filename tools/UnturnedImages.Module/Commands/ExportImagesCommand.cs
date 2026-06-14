using SDG.Unturned;
using Steamworks;
using System;
using UnturnedImages.Module.Config;
using UnturnedImages.Module.Images;

namespace UnturnedImages.Module.Commands
{
    /// <summary>
    /// Console command for headless batch export without opening the F10 menu.
    /// Usage: <c>exportimages &lt;items|vehicles|all|workshop&gt; [workshopId]</c>
    /// </summary>
    public sealed class ExportImagesCommand : Command
    {
        private const string Usage = "Usage: exportimages <items|vehicles|all|workshop> [workshopId]";

        public ExportImagesCommand()
        {
            _command = "exportimages";
            _info = "Exports item/vehicle icons (UnturnedImagesGenerator).";
            _help = Usage;
        }

        protected override void execute(CSteamID executorID, string parameter)
        {
            var args = (parameter ?? string.Empty).Trim()
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (args.Length == 0)
            {
                CommandWindow.Log(Usage);
                return;
            }

            var settings = ExportSettings.LoadOrDefaults();
            var mode = args[0].ToLowerInvariant();

            try
            {
                switch (mode)
                {
                    case "items":
                        ImageUtils.CaptureAllItemImages(settings);
                        CommandWindow.Log("UnturnedImagesGenerator: item export queued.");
                        break;

                    case "vehicles":
                        ImageUtils.CaptureAllVehicleImages(settings);
                        CommandWindow.Log("UnturnedImagesGenerator: vehicle export queued.");
                        break;

                    case "all":
                        ImageUtils.CaptureAllItemImages(settings);
                        ImageUtils.CaptureAllVehicleImages(settings);
                        CommandWindow.Log("UnturnedImagesGenerator: item and vehicle export queued.");
                        break;

                    case "workshop":
                        if (args.Length < 2 || !uint.TryParse(args[1], out var modId) || modId == 0)
                        {
                            CommandWindow.LogError("Specify a valid Workshop ID: exportimages workshop <id>");
                            return;
                        }

                        settings.WorkshopModIdFilter = modId;
                        ImageUtils.CaptureItemsForWorkshopMod(modId, settings);
                        ImageUtils.CaptureVehiclesForWorkshopMod(modId, settings);
                        CommandWindow.Log($"UnturnedImagesGenerator: workshop mod {modId} export queued.");
                        break;

                    default:
                        CommandWindow.Log(Usage);
                        break;
                }
            }
            catch (Exception ex)
            {
                CommandWindow.LogError("UnturnedImagesGenerator: command error: " + ex.Message);
            }
        }
    }
}
