using NSMB.Quantum;
using NSMB.UI.Game;
using Quantum;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NSMB.Utilities.Components {
    /// <summary>
    /// Makes a Point/Spot light wrap across the stage loop-point.
    /// URP lights are world-space, so a light sitting next to the seam only
    /// illuminates its own side. This spawns two ghost lights at +/- levelWidth
    /// (as children, so they follow moving entities automatically) and enables
    /// them only when a gameplay camera can actually see them.
    /// Directional lights are global and intentionally ignored.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class WrappingLight : MonoBehaviour {

        private Light original;
        private Light ghostLeft, ghostRight;
        private float levelWidth;
        private VersusStageData stage;
        private bool initialized;

        private static readonly List<Camera> cameraCache = new(8);

        public void Awake() {
            original = GetComponent<Light>();
        }

        public void Start() {
            TryInitialize();
        }

        public void LateUpdate() {
            if (!initialized) {
                TryInitialize();
                if (!initialized) {
                    return;
                }
            } else if (Time.frameCount % 120 == Mathf.Abs(GetInstanceID()) % 120) {
                // Levels (and their widths) change; pooled views persist.
                // Re-create ghosts if we switched to a different stage.
                VersusStageData current = FindStage();
                if (current != stage) {
                    stage = current;
                    initialized = false;
                    DestroyGhosts();
                    TryInitialize();
                    if (!initialized) {
                        return;
                    }
                }
            }

            if (!original) {
                DestroyGhosts();
                return;
            }

            // Non-wrapping levels need no ghosts.
            if (stage == null || !stage.IsWrappingLevel) {
                SetGhostEnabled(ghostLeft, false);
                SetGhostEnabled(ghostRight, false);
                return;
            }

            // Directional (and other non-positional) lights need no wrapping.
            if (original.type != LightType.Point && original.type != LightType.Spot) {
                SetGhostEnabled(ghostLeft, false);
                SetGhostEnabled(ghostRight, false);
                return;
            }

            SyncGhostProperties();
            UpdateGhostVisibility();
        }

        public void OnDestroy() {
            DestroyGhosts();
        }

        private void TryInitialize() {
            if (initialized) {
                return;
            }
            if (!original) {
                original = GetComponent<Light>();
                if (!original) {
                    return;
                }
            }

            // Ghosts must never wrap themselves (infinite recursion).
            if (gameObject.name.Contains("(Wrap ")) {
                enabled = false;
                return;
            }

            stage = FindStage();
            if (stage == null) {
                return;
            }
            if (!stage.IsWrappingLevel) {
                initialized = true;
                return;
            }

            levelWidth = stage.TileDimensions.X * 0.5f;
            if (levelWidth <= 0f) {
                return;
            }

            // Directional lights don't need ghosts.
            if (original.type != LightType.Point && original.type != LightType.Spot) {
                initialized = true;
                return;
            }

            CreateGhosts();
            initialized = ghostLeft && ghostRight;
        }

        private void CreateGhosts() {
            DestroyGhosts();

            ghostLeft = CreateGhost(" (Wrap Left)", -levelWidth);
            ghostRight = CreateGhost(" (Wrap Right)", levelWidth);
        }

        private Light CreateGhost(string suffix, float xOffset) {
            var go = new GameObject(gameObject.name + suffix);
            // Parent to the light itself so activation, destruction and movement
            // (e.g. fireballs, stars) are inherited automatically.
            go.transform.SetParent(transform, false);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            // Use world-space offset so wrapping stays on the X axis even if
            // the light (e.g. a Spot) ever has a rotation.
            go.transform.position = transform.position + new Vector3(xOffset, 0f, 0f);
            go.layer = gameObject.layer;

            Light ghost = go.AddComponent<Light>();
            CopyLightProperties(original, ghost);
            CopyAdditionalLightData(gameObject, go);

            ghost.enabled = false;
            return ghost;
        }

        private void DestroyGhosts() {
            if (ghostLeft) {
                if (ghostLeft.gameObject) {
                    Destroy(ghostLeft.gameObject);
                }
                ghostLeft = null;
            }
            if (ghostRight) {
                if (ghostRight.gameObject) {
                    Destroy(ghostRight.gameObject);
                }
                ghostRight = null;
            }
        }

        private void SyncGhostProperties() {
            if (!original) {
                return;
            }
            if (ghostLeft) {
                CopyLightProperties(original, ghostLeft);
            }
            if (ghostRight) {
                CopyLightProperties(original, ghostRight);
            }
        }

        private void UpdateGhostVisibility() {
            if (!original || !original.enabled || !original.gameObject.activeInHierarchy) {
                SetGhostEnabled(ghostLeft, false);
                SetGhostEnabled(ghostRight, false);
                return;
            }

            CollectGameplayCameras();

            bool leftVisible = ghostLeft && IsVisibleToAnyCamera(ghostLeft.transform.position, ghostLeft.range);
            bool rightVisible = ghostRight && IsVisibleToAnyCamera(ghostRight.transform.position, ghostRight.range);

            SetGhostEnabled(ghostLeft, leftVisible);
            SetGhostEnabled(ghostRight, rightVisible);
        }

        private bool IsVisibleToAnyCamera(Vector3 worldPos, float range) {
            if (cameraCache.Count == 0) {
                // No gameplay camera yet (loading, menu, etc.).
                // Fall back to seam proximity so lights near the loop still wrap.
                if (stage == null) {
                    return true;
                }
                float minX = stage.StageWorldMin.X.AsFloat;
                float maxX = stage.StageWorldMax.X.AsFloat;
                return worldPos.x > minX - range - 2f && worldPos.x < maxX + range + 2f;
            }

            foreach (Camera cam in cameraCache) {
                if (!cam || !cam.isActiveAndEnabled) {
                    continue;
                }
                float halfHeight = cam.orthographic ? cam.orthographicSize : 10f;
                float halfWidth = halfHeight * cam.aspect;
                if (Mathf.Abs(worldPos.x - cam.transform.position.x) < halfWidth + range + 1f
                    && Mathf.Abs(worldPos.y - cam.transform.position.y) < halfHeight + range + 1f) {
                    return true;
                }
            }
            return false;
        }

        private static void SetGhostEnabled(Light ghost, bool enabled) {
            if (ghost && ghost.enabled != enabled) {
                ghost.enabled = enabled;
            }
        }

        private static void CollectGameplayCameras() {
            cameraCache.Clear();
            // Prefer real gameplay views so UI/preview cameras don't keep ghosts alive.
            if (PlayerElements.AllPlayerElements != null) {
                foreach (PlayerElements elements in PlayerElements.AllPlayerElements) {
                    if (elements && elements.Camera && elements.Camera.isActiveAndEnabled) {
                        cameraCache.Add(elements.Camera);
                    }
                }
            }
            if (cameraCache.Count == 0 && Camera.main && Camera.main.isActiveAndEnabled) {
                cameraCache.Add(Camera.main);
            }
        }

        private static VersusStageData FindStage() {
            try {
                var context = UnityEngine.Object.FindFirstObjectByType<StageContext>();
                if (context && context.Stage) {
                    return context.Stage;
                }
                if (QuantumRunner.DefaultGame != null) {
                    var frame = QuantumRunner.DefaultGame.Frames.Predicted;
                    if (frame != null) {
                        return frame.FindAsset<VersusStageData>(frame.Map.UserAsset);
                    }
                }
            } catch {
                // Scene is tearing down; ignore.
            }
            return null;
        }

        private static void CopyLightProperties(Light from, Light to) {
            to.type = from.type;
            to.color = from.color;
            to.intensity = from.intensity;
            to.range = from.range;
            to.spotAngle = from.spotAngle;
            to.innerSpotAngle = from.innerSpotAngle;
            to.shadows = from.shadows;
            to.shadowStrength = from.shadowStrength;
            to.shadowBias = from.shadowBias;
            to.shadowNormalBias = from.shadowNormalBias;
            to.shadowNearPlane = from.shadowNearPlane;
#if UNITY_6000_0_OR_NEWER
            to.shadowRadius = from.shadowRadius;
            to.shadowAngle = from.shadowAngle;
#endif
            to.cullingMask = from.cullingMask;
            to.cookie = from.cookie;
            if (to.cookie) {
                to.cookieSize = from.cookieSize;
            }
            to.renderMode = from.renderMode;
            to.bounceIntensity = from.bounceIntensity;
        }

        private static void CopyAdditionalLightData(GameObject from, GameObject to) {
            var fromData = from.GetComponent<UniversalAdditionalLightData>();
            if (!fromData) {
                return;
            }
            var toData = to.AddComponent<UniversalAdditionalLightData>();
            // Copy all serialized fields via reflection so this survives URP version changes.
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            foreach (FieldInfo field in typeof(UniversalAdditionalLightData).GetFields(flags)) {
                if (field.IsNotSerialized) {
                    continue;
                }
                try {
                    field.SetValue(toData, field.GetValue(fromData));
                } catch {
                    // Ignore version-specific fields that can't be copied.
                }
            }
        }

        /// <summary>
        /// Adds <see cref="WrappingLight"/> to every Point/Spot light under root that needs it.
        /// Used for pooled entity views and scene lights so prefabs don't all need manual edits.
        /// </summary>
        public static void EnsureAdded(GameObject root) {
            if (!root) {
                return;
            }
            foreach (Light light in root.GetComponentsInChildren<Light>(true)) {
                if (!light) {
                    continue;
                }
                if (light.type != LightType.Point && light.type != LightType.Spot) {
                    continue;
                }
                if (light.GetComponent<WrappingLight>()) {
                    continue;
                }
                if (light.gameObject.name.Contains("(Wrap ")) {
                    continue;
                }
                light.gameObject.AddComponent<WrappingLight>();
            }
        }
    }
}
