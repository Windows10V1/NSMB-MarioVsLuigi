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

        //---Serialized Variables
        [SerializeField] private float pulseAmount = 0.2f, pulseSpeed = 0.2f, rotationSpeed = 30f, blinkingSpeed = 0.5f;
        [SerializeField] private Transform graphicTransform;
        [SerializeField] private ParticleSystem particles;
        [SerializeField] private GameObject starCollectPrefab;

        //---Components
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private Animation legacyAnimation;
        [SerializeField] private SoundEffectPlayer sfx;
        [SerializeField] private Material solidMaterial, transparentMaterial;

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
                graphicTransform.localScale = Vector3.one + new Vector3(sin, sin, 0);
                meshRenderer.material = solidMaterial;
                meshRenderer.enabled = true;
            } else {
                graphicTransform.localScale = Vector3.one;
                graphicTransform.Rotate(new(0, 0, rotationSpeed * 30 * (star->FacingRight ? -1 : 1) * Time.deltaTime), Space.Self);
                float timeRemaining = star->Lifetime / 60f;
                meshRenderer.enabled = !(timeRemaining < 5 && timeRemaining * 2 % (blinkingSpeed * 2) < blinkingSpeed);
                meshRenderer.material = star->UncollectableFrames > 0 ? transparentMaterial : solidMaterial;
                legacyAnimation.Stop();
            }
        }
    }
}
