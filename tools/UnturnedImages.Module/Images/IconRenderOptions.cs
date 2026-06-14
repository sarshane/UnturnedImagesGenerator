using UnityEngine;

namespace UnturnedImages.Module.Images
{
    public sealed class IconRenderOptions
    {
        public int SupersamplingScale { get; set; } = 1;

        public Vector3 KeyLightPosition { get; set; } = new(1.5f, 2.2f, -2.5f);

        public float KeyLightIntensity { get; set; } = 1.2f;

        public Color KeyLightColor { get; set; } = Color.white;

        public Vector3 RimLightPosition { get; set; } = new(-2.2f, 1.6f, 2.6f);

        public float RimLightIntensity { get; set; } = 0.55f;

        public Color RimLightColor { get; set; } = new(0.75f, 0.85f, 1f, 1f);

        public bool UseShadowCatcher { get; set; } = true;

        /// <summary>
        /// When true, the key light is rendered as a size-independent directional light aimed
        /// from <see cref="KeyLightPosition"/> instead of a point light. Useful for large vehicles.
        /// </summary>
        public bool UseDirectionalLight { get; set; }

        /// <summary>
        /// When true, transparent margins are cropped and re-padded to <see cref="TrimPaddingFraction"/>.
        /// </summary>
        public bool TrimTransparentPadding { get; set; }

        /// <summary>
        /// Fraction of the image kept as empty margin around the model after trimming (0..0.45).
        /// </summary>
        public float TrimPaddingFraction { get; set; } = 0.06f;

        /// <summary>
        /// When true, the transparent background is filled with <see cref="BackgroundColor"/>.
        /// </summary>
        public bool UseSolidBackground { get; set; }

        public Color BackgroundColor { get; set; } = new(0.12f, 0.12f, 0.14f, 1f);

        public float CameraZoom { get; set; } = 1f;

        public float CameraOffsetX { get; set; }

        public float CameraOffsetY { get; set; }

        public int RenderWidth(int width) => width * Mathf.Clamp(SupersamplingScale, 1, 4);

        public int RenderHeight(int height) => height * Mathf.Clamp(SupersamplingScale, 1, 4);

        public float ApplyZoom(float orthographicSize) => orthographicSize / Mathf.Clamp(CameraZoom, 0.5f, 2.5f);

        public Vector3 ApplyCameraOffset(Transform cameraTransform, Vector3 cameraPosition, float orthographicSize)
        {
            var zoomedSize = ApplyZoom(orthographicSize);
            return cameraPosition +
                   cameraTransform.right * (Mathf.Clamp(CameraOffsetX, -0.8f, 0.8f) * zoomedSize) +
                   cameraTransform.up * (Mathf.Clamp(CameraOffsetY, -0.8f, 0.8f) * zoomedSize);
        }
    }
}
