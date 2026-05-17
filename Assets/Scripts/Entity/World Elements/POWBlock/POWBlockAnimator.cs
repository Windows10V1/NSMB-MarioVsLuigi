using NSMB.Cameras;
using NSMB.Sound;
using NSMB.Utilities.Extensions;
using Quantum;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;

namespace NSMB.Entities.World {
    public unsafe class POWBlockAnimator : QuantumEntityViewComponent {

        [SerializeField] private SpriteRenderer sRenderer;
        [SerializeField] private Sprite[] useSprites;

        public void OnValidate() {
            this.SetIfNull(ref sRenderer, UnityExtensions.GetComponentType.Children);
        }

        public void Start() {
            QuantumEvent.Subscribe<EventPOWBlockActivated>(this, OnPOWBlockActivated, FilterOutReplayFastForward);
        }

        public override void OnActivate(Frame f) {
            UpdateSprite(f);
        }

        public override void OnUpdateView() {
            Frame f = PredictedFrame;
            if (f.Exists(EntityRef)) {
                UpdateSprite(f);
            }
        }

        private void OnPOWBlockActivated(EventPOWBlockActivated e) {
            if (e.Entity != EntityRef) {
                return;
            }

            SoundEffectResolver.Instance.PlayOneShotGlobally(SoundEffect.Powerup_MegaMushroom_Groundpound);
            Instantiate(
                Enums.PrefabParticle.Player_MegaGroundpoundDust.GetGameObject(),
                e.Position.ToUnityVector3() + (Vector3.back * 5),
                Quaternion.identity
            );
            CameraAnimator.TriggerScreenshake(0.15f);
        }

        private void UpdateSprite(Frame f) {
            if (!sRenderer) {
                sRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (!sRenderer
                || useSprites == null
                || useSprites.Length == 0
                || !f.Unsafe.TryGetPointer(EntityRef, out POWBlock* powBlock)) {
                return;
            }

            int index = Mathf.Clamp(powBlock->Uses, 0, useSprites.Length - 1);
            if (useSprites[index]) {
                sRenderer.sprite = useSprites[index];
            }
        }
    }
}
