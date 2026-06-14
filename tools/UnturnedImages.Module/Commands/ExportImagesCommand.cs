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
        private const string Usage = "Использование: exportimages <items|vehicles|all|workshop> [workshopId]";

        public ExportImagesCommand()
        {
            _command = "exportimages";
            _info = "Экспортирует иконки предметов/транспорта (UnturnedImagesGenerator).";
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
                        CommandWindow.Log("UnturnedImagesGenerator: экспорт предметов поставлен в очередь.");
                        break;

                    case "vehicles":
                        ImageUtils.CaptureAllVehicleImages(settings);
                        CommandWindow.Log("UnturnedImagesGenerator: экспорт транспорта поставлен в очередь.");
                        break;

                    case "all":
                        ImageUtils.CaptureAllItemImages(settings);
                        ImageUtils.CaptureAllVehicleImages(settings);
                        CommandWindow.Log("UnturnedImagesGenerator: экспорт предметов и транспорта поставлен в очередь.");
                        break;

                    case "workshop":
                        if (args.Length < 2 || !uint.TryParse(args[1], out var modId) || modId == 0)
                        {
                            CommandWindow.LogError("Укажите корректный Workshop ID: exportimages workshop <id>");
                            return;
                        }

                        settings.WorkshopModIdFilter = modId;
                        ImageUtils.CaptureItemsForWorkshopMod(modId, settings);
                        ImageUtils.CaptureVehiclesForWorkshopMod(modId, settings);
                        CommandWindow.Log($"UnturnedImagesGenerator: экспорт workshop-мода {modId} поставлен в очередь.");
                        break;

                    default:
                        CommandWindow.Log(Usage);
                        break;
                }
            }
            catch (Exception ex)
            {
                CommandWindow.LogError("UnturnedImagesGenerator: ошибка команды: " + ex.Message);
            }
        }
    }
}
