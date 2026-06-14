using SDG.Framework.Modules;
using SDG.Unturned;
using System;
using UnityEngine;
using UnturnedImages.Module.Commands;
using UnturnedImages.Module.Images;
using UnturnedImages.Module.Patches;
using UnturnedImages.Module.UI;

namespace UnturnedImages.Module
{
    public class UnturnedImagesModule : MonoBehaviour, IModuleNexus
    {
        private readonly HarmonyPatches _harmonyPatches = new();

        public static UnturnedImagesModule? Instance { get; private set; }

        public GameObject? GameObject;

        public void initialize()
        {
            UnturnedLog.info("Loading UnturnedImages Module — F10: PNG export window.");

            Instance = this;

            GameObject = new GameObject();
            DontDestroyOnLoad(GameObject);

            _harmonyPatches.Patch();
            CustomImageTool.Load();
            CustomVehicleTool.Load();
            CustomItemTool.Load();
            ImageGeneratorMenu.Load();

            try
            {
                Commander.register(new ExportImagesCommand());
            }
            catch (Exception ex)
            {
                UnturnedLog.warn("UnturnedImagesGenerator: could not register exportimages command: " + ex.Message);
            }
        }

        public void shutdown()
        {
            UnturnedLog.info("Unloading UnturnedImages Module");

            Destroy(GameObject);

            ImageGeneratorMenu.Unload();
            CustomItemTool.Unload();
            CustomVehicleTool.Unload();
            CustomImageTool.Unload();
            _harmonyPatches.Unpatch();

            Instance = null;
        }
    }
}
