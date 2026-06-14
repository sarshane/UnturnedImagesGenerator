using SDG.Unturned;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnturnedImages.Module.Images
{
    public class CustomImageTool : MonoBehaviour
    {
        private static CustomImageTool? _tool;

        private Camera? _cameraComponent;
        private Light? _lightComponent;
        private Light? _rimLightComponent;
        private Transform? _renderRig;
        private Transform? _keyLightTransform;
        private Transform? _rimLightTransform;

        public static void Load()
        {
            _tool = UnturnedImagesModule.Instance!.GameObject!.AddComponent<CustomImageTool>();
        }

        public static void Unload()
        {
            Destroy(_tool);

            _tool = null;
        }

        private void Start()
        {
            var rig = new GameObject("UnturnedImages Icon Camera");
            rig.transform.SetParent(transform, false);
            _renderRig = rig.transform;

            _cameraComponent = rig.AddComponent<Camera>();
            _cameraComponent.orthographic = true;
            _cameraComponent.enabled = false;

            var keyLightObject = new GameObject("Key Light");
            keyLightObject.transform.SetParent(rig.transform, false);
            _keyLightTransform = keyLightObject.transform;
            _lightComponent = keyLightObject.AddComponent<Light>();
            _lightComponent.type = LightType.Point;
            _lightComponent.range = 12f;
            _lightComponent.shadows = LightShadows.Soft;
            _lightComponent.enabled = false;

            var rimLightObject = new GameObject("Rim Light");
            rimLightObject.transform.SetParent(rig.transform, false);
            _rimLightTransform = rimLightObject.transform;
            _rimLightComponent = rimLightObject.AddComponent<Light>();
            _rimLightComponent.type = LightType.Point;
            _rimLightComponent.range = 12f;
            _rimLightComponent.shadows = LightShadows.None;
            _rimLightComponent.enabled = false;
        }

        public static Texture2D CaptureIcon(ushort id, ushort skin, Transform model, Transform icon, int width, int height, float orthoSize, bool readableOnCPU)
        {
            return CaptureIcon(id, skin, model, icon, width, height, orthoSize, readableOnCPU, null);
        }

        public static Texture2D CaptureIcon(ushort id, ushort skin, Transform model, Transform icon, int width,
            int height, float orthoSize, bool readableOnCPU, IconRenderOptions? options)
        {
            if (_tool == null)
            {
                throw new Exception("No instance of CustomImageTool");
            }

            if (_tool._cameraComponent == null)
            {
                throw new Exception("No instance of camera");
            }

            if (_tool._lightComponent == null)
            {
                throw new Exception("No instance of light");
            }

            options ??= new IconRenderOptions();
            var renderWidth = options.RenderWidth(width);
            var renderHeight = options.RenderHeight(height);

            _tool._renderRig!.position = icon.position;
            _tool._renderRig.rotation = icon.rotation;
            _tool._keyLightTransform!.localPosition = options.KeyLightPosition;
            _tool._rimLightTransform!.localPosition = options.RimLightPosition;
            _tool._lightComponent.intensity = options.KeyLightIntensity;
            _tool._lightComponent.color = options.KeyLightColor;
            _tool._rimLightComponent!.intensity = options.RimLightIntensity;
            _tool._rimLightComponent.color = options.RimLightColor;

            if (options.UseDirectionalLight)
            {
                _tool._lightComponent.type = LightType.Directional;
                var aim = -options.KeyLightPosition;
                _tool._keyLightTransform.localRotation = Quaternion.LookRotation(
                    aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector3.forward);
            }
            else
            {
                _tool._lightComponent.type = LightType.Point;
                _tool._keyLightTransform.localRotation = Quaternion.identity;
            }

            const int antiAliasing = 4;
            var temporary = RenderTexture.GetTemporary(renderWidth, renderHeight, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB, antiAliasing);
            temporary.name = $"Render_{id}_{skin}";
            var shadowPlane = options.UseShadowCatcher ? CreateShadowCatcher(model) : null;

            RenderTexture.active = temporary;
            _tool._cameraComponent.targetTexture = temporary;
            _tool._cameraComponent.orthographicSize = options.ApplyZoom(orthoSize);
            _tool._cameraComponent.transform.position =
                options.ApplyCameraOffset(_tool._cameraComponent.transform, _tool._cameraComponent.transform.position,
                    orthoSize);
            var fog = RenderSettings.fog;
            var ambientMode = RenderSettings.ambientMode;
            var ambientSkyColor = RenderSettings.ambientSkyColor;
            var ambientEquatorColor = RenderSettings.ambientEquatorColor;
            var ambientGroundColor = RenderSettings.ambientGroundColor;

            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.white;
            RenderSettings.ambientEquatorColor = Color.white;
            RenderSettings.ambientGroundColor = Color.white;

            _tool._lightComponent.enabled = true;
            _tool._rimLightComponent.enabled = options.RimLightIntensity > 0.001f;
            GL.Clear(true, true, ColorEx.BlackZeroAlpha);
            _tool._cameraComponent.cullingMask = 67313664;
            _tool._cameraComponent.farClipPlane = 128f;
            _tool._cameraComponent.clearFlags = CameraClearFlags.Nothing;
            _tool._cameraComponent.Render();
            _tool._lightComponent.enabled = false;
            _tool._rimLightComponent.enabled = false;

            RenderSettings.fog = fog;
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientSkyColor = ambientSkyColor;
            RenderSettings.ambientEquatorColor = ambientEquatorColor;
            RenderSettings.ambientGroundColor = ambientGroundColor;

            if (shadowPlane != null)
            {
                Destroy(shadowPlane);
            }

            Destroy(model.gameObject);

            var rendered = new Texture2D(renderWidth, renderHeight, TextureFormat.ARGB32, false)
            {
                name = $"Icon_{id}_{skin}",
                filterMode = FilterMode.Bilinear
            };

            rendered.ReadPixels(new Rect(0f, 0f, renderWidth, renderHeight), 0, 0);
            rendered.Apply(false, false);
            RenderTexture.ReleaseTemporary(temporary);

            if (renderWidth == width && renderHeight == height)
            {
                rendered.Apply(false, !readableOnCPU);
                return rendered;
            }

            var downscaled = Downscale(rendered, width, height, readableOnCPU);
            Destroy(rendered);
            return downscaled;
        }

        private static Texture2D Downscale(Texture2D source, int width, int height, bool readableOnCPU)
        {
            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            Graphics.Blit(source, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var result = new Texture2D(width, height, TextureFormat.ARGB32, false)
            {
                filterMode = FilterMode.Bilinear
            };
            result.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            result.Apply(false, !readableOnCPU);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return result;
        }

        private static GameObject? CreateShadowCatcher(Transform model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length == 0)
            {
                return null;
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "UnturnedImages Shadow Catcher";
            plane.transform.SetParent(model.parent, true);
            plane.transform.position = new Vector3(bounds.center.x, bounds.min.y - 0.035f, bounds.center.z);
            var size = Mathf.Max(bounds.size.x, bounds.size.z) * 0.16f;
            plane.transform.localScale = new Vector3(size, 1f, size);
            Layerer.relayer(plane.transform, LayerMasks.VEHICLE);

            var renderer = plane.GetComponent<Renderer>();
            renderer.receiveShadows = true;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.material = new Material(Shader.Find("Standard"))
            {
                color = new Color(0.82f, 0.82f, 0.82f, 1f)
            };

            return plane;
        }

        /// <summary>
        /// Extra orthographic margin (1.0 = sphere touches the frame edges).
        /// </summary>
        public const float FrameMargin = 1.04f;

        /// <summary>
        /// Computes a bounding sphere (centre + radius) around every mesh of the model.
        /// Because a sphere is rotation-invariant, the caller can rotate the model about
        /// <paramref name="center"/> and it will always stay inside the frame at the same scale —
        /// no per-rotation reframing, so the camera never jitters and any vehicle size fits.
        /// </summary>
        public static bool TryGetBoundingSphere(GameObject modelGameObject, out Vector3 center, out float radius)
        {
            center = Vector3.zero;
            radius = 0f;

            var renderers = modelGameObject.GetComponentsInChildren<Renderer>(false);
            var bounds = default(Bounds);
            var found = false;

            foreach (var renderer in renderers)
            {
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    // Force per-frame bounds so the mesh is not wrongly frustum-culled while rotating,
                    // and so the bounds are accurate/stable (preview and export frame it identically).
                    skinned.updateWhenOffscreen = true;
                }
                else if (!(renderer is MeshRenderer))
                {
                    continue;
                }

                if (found)
                {
                    bounds.Encapsulate(renderer.bounds);
                }
                else
                {
                    bounds = renderer.bounds;
                    found = true;
                }
            }

            if (!found)
            {
                return false;
            }

            var extents = bounds.extents;
            if (extents.ContainsInfinity() || extents.ContainsNaN() || extents.IsNearlyZero(0.0001f))
            {
                return false;
            }

            center = bounds.center;
            // Half-diagonal of the AABB = radius of the circumscribing sphere (encloses all geometry).
            radius = extents.magnitude;
            return radius > 0.0001f;
        }
    }
}
