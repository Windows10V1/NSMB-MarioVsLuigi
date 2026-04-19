using NSMB.Sound;
using NSMB.Utilities.Extensions;
using Quantum;
using System;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;

namespace NSMB.Entities.World {
    public class BigStarAnimator : QuantumEntityViewComponent {

        //---Static Variables
        public static event Action<Frame, BigStarAnimator> BigStarInitialized;
        public static event Action<Frame, BigStarAnimator> BigStarDestroyed;
        private static Color UncollectableColor = new(1, 1, 1, 0.55f);

        //---Serialized Variables
        [SerializeField] private float pulseAmount = 0.2f, pulseSpeed = 0.2f, rotationSpeed = 30f, blinkingSpeed = 0.5f;
        [SerializeField] private Transform graphicTransform;
        [SerializeField] private ParticleSystem particles;
        [SerializeField] private GameObject starCollectPrefab;

        //---Components
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private Animation legacyAnimation;
        [SerializeField] private SoundEffectPlayer sfx;
        [SerializeField] private Material originalMaterial;
        [SerializeField] private Material transparentMaterial;

        //--Private Variables
        private float pulseEffectCounter;

        public void OnValidate() {
            this.SetIfNull(ref meshRenderer, UnityExtensions.GetComponentType.Children);
            this.SetIfNull(ref legacyAnimation);
            this.SetIfNull(ref sfx);
        }

        public override unsafe void OnActivate(Frame f) {
            var star = f.Unsafe.GetPointer<BigStar>(EntityRef);

            graphicTransform.rotation = Quaternion.identity;
            meshRenderer.enabled = true;
            
            // Capture original material from mesh renderer if not assigned
            if (meshRenderer != null) {
                if (originalMaterial == null) {
                    originalMaterial = meshRenderer.sharedMaterial;
                }
                meshRenderer.material = originalMaterial;
            }
            
            if (f.Global->GameState == GameState.Playing && !IsReplayFastForwarding) {
                sfx.PlayOneShot(SoundEffect.World_Star_Spawn);
            }
            if (star->IsStationary) {
                legacyAnimation.Play();
            }

            BigStarInitialized?.Invoke(f, this);
        }

        public override void OnDeactivate() {
            BigStarDestroyed?.Invoke(VerifiedFrame, this);
        }

        public unsafe override void OnUpdateView() {
            Frame f = PredictedFrame;
            if (f.Global->GameState >= GameState.Ended) {
                return;
            }

            if (!f.Exists(EntityRef)) {
                meshRenderer.enabled = false;
                return;
            }

            var star = f.Unsafe.GetPointer<BigStar>(EntityRef);

            if (star->IsStationary) {
                pulseEffectCounter += Time.deltaTime;
                float sin = Mathf.Sin(pulseEffectCounter * pulseSpeed) * pulseAmount;
                graphicTransform.localScale = Vector3.one + new Vector3(sin, sin, sin);
                meshRenderer.enabled = true;
                if (meshRenderer != null && originalMaterial != null) {
                    meshRenderer.material = originalMaterial;
                }
            } else {
                graphicTransform.localScale = Vector3.one;
                graphicTransform.Rotate(new(0, 0, rotationSpeed * 30 * (star->FacingRight ? -1 : 1) * Time.deltaTime), Space.Self);
                float timeRemaining = star->Lifetime / 60f;
                meshRenderer.enabled = !(timeRemaining < 5 && timeRemaining * 2 % (blinkingSpeed * 2) < blinkingSpeed);
                if (meshRenderer != null) {
                    meshRenderer.material = star->UncollectableFrames > 0 ? transparentMaterial : originalMaterial;
                }
                legacyAnimation.Stop();
            }
        }
    }
}
