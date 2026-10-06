using NSMB.Quantum;
using NSMB.Utilities.Extensions;
using Quantum;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NSMB.Cameras {
    public class SecondaryCameraPositioner : QuantumSceneViewComponent<StageContext> {

        //---Serialized Variables
        [SerializeField] private Camera mainCamera;
        [SerializeField] private Camera ourCamera;
        [SerializeField] private UnityEngine.LayerMask alwaysIgnoreMask;
        [SerializeField] private bool copyPropertiesOnly;

        //---Private Variables
        private bool destroyed;

        public void OnValidate() {
            this.SetIfNull(ref ourCamera);
        }

        public override void OnEnable() {
            base.OnEnable();
            DisableOverlayPostProcessing();
        }

        public void UpdatePosition() {
            if (!copyPropertiesOnly) {
                if (destroyed) {
                    return;
                }

                if (ViewContext == null) {
                    return;
                }
                VersusStageData stage = ViewContext.Stage;
                if (stage == null) {
                    return;
                }
                if (!mainCamera || !ourCamera) {
                    return;
                }

                if (!stage.IsWrappingLevel) {
                    Destroy(gameObject);
                    destroyed = true;
                    return;
                }

                float camX = mainCamera.transform.position.x;
                bool enable = Mathf.Abs(camX - stage.StageWorldMin.X.AsFloat) < (mainCamera.orthographicSize * mainCamera.aspect) || Mathf.Abs(camX - stage.StageWorldMax.X.AsFloat) < (mainCamera.orthographicSize * mainCamera.aspect);

                ourCamera.enabled = enable;

                if (enable) {
                    float middle = stage.StageWorldMin.X.AsFloat + stage.TileDimensions.X * 0.25f;
                    bool rightHalf = mainCamera.transform.position.x > middle;
                    transform.localPosition = new(stage.TileDimensions.X * (rightHalf ? -1 : 1) * 0.5f, 0, 0);
                }
            }

            if (!mainCamera || !ourCamera) {
                return;
            }
            ourCamera.orthographicSize = mainCamera.orthographicSize;
            ourCamera.cullingMask = mainCamera.cullingMask & ~alwaysIgnoreMask;
            DisableOverlayPostProcessing();
        }

        /// <summary>
        /// The wrap (scroll) camera is stacked on top of the main camera via URP camera stacking.
        /// Post-processing (bloom, vignette, etc.) must only run once on the base camera.
        /// If the overlay also renders post-processing, bloom/vignette get applied twice
        /// and visibly duplicate whenever the loop seam is on screen.
        /// </summary>
        private void DisableOverlayPostProcessing() {
            if (!ourCamera) {
                return;
            }
            if (ourCamera.TryGetComponent(out UniversalAdditionalCameraData overlayData)) {
                if (overlayData.renderPostProcessing) {
                    overlayData.renderPostProcessing = false;
                }
            }
        }
    }
}
