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
        [SerializeField] private AudioSource sfx;

        public void OnValidate() {
            this.SetIfNull(ref sRenderer, UnityExtensions.GetComponentType.Children);
            this.SetIfNull(ref sfx);
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

            Instantiate(
                Enums.PrefabParticle.Player_MegaGroundpoundDust.GetGameObject(),
                transform.position + (Vector3.back * 5),
                Quaternion.identity
            );
            Instantiate(
                Enums.PrefabParticle.Item_POWBlockImpact.GetGameObject(),
                transform.position + (Vector3.back * 5),
                Quaternion.identity
            );

            if (SoundEffectResolver.Instance) {
                SoundEffectResolver.Instance.PlayOneShotGlobally(SoundEffect.Powerup_MegaMushroom_Groundpound);
            } else if (sfx) {
                sfx.PlayOneShot(SoundEffect.Powerup_MegaMushroom_Groundpound);
            }

            CameraAnimator.TriggerScreenshake(0.35f);
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
