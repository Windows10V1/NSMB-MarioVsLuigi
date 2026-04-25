using NSMB.Particles;
using NSMB.Utilities.Extensions;
using Quantum;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;
using static Enums;

namespace NSMB.Entities.World {
    public unsafe class PowBlockAnimator : QuantumEntityViewComponent {

        //---Serialized Variables
        [SerializeField] private SpriteRenderer sRenderer;
        [SerializeField] private Sprite[] sprites; // 0: full, 1: 1st use, 2: 2nd use
        [SerializeField] private AudioSource sfx;

        public void OnValidate() {
            this.SetIfNull(ref sRenderer);
            this.SetIfNull(ref sfx, UnityExtensions.GetComponentType.Children);
        }

        public void Start() {
            QuantumEvent.Subscribe<EventPowBlockExploded>(this, OnPowBlockExploded, onlyIfEntityViewBound: true);
        }

        public override void OnUpdateView() {
            Frame f = PredictedFrame;
            if (!f.Unsafe.TryGetPointer(EntityRef, out PowBlock* powBlock)) {
                return;
            }

            // Update sprite based on SpriteState
            if (sprites != null && sprites.Length > 0 && powBlock->SpriteState < sprites.Length) {
                sRenderer.sprite = sprites[powBlock->SpriteState];
            }
        }

        private void OnPowBlockExploded(EventPowBlockExploded e) {
            if (e.Entity != EntityRef) {
                return;
            }

            if (!IsReplayFastForwarding) {
                // Play explosion sound globally
                sfx.PlayOneShot(SoundEffect.Powerup_MegaMushroom_Groundpound);

                // Spawn explosion particle effect using PrefabParticle enum
                Instantiate(PrefabParticle.Player_MegaGroundpoundDust.GetGameObject(), transform.position, Quaternion.identity);
            }
        }
    }
}
