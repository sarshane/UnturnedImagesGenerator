using HarmonyLib;

namespace UnturnedImages.Module.Patches
{
    public sealed class HarmonyPatches
    {
        private readonly Harmony _harmonyInstance = new(HarmonyId);

        private bool _patched;

        public const string HarmonyId = "com.silksplugins.unturnedimages";

        public void Patch()
        {
            if (_patched)
            {
                return;
            }

            _patched = true;

            _harmonyInstance.PatchAll(GetType().Assembly);
        }

        public void Unpatch()
        {
            if (!_patched)
            {
                return;
            }

            _patched = false;

            _harmonyInstance.UnpatchAll(HarmonyId);
        }
    }
}
