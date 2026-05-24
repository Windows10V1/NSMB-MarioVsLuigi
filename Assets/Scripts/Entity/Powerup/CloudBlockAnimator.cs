using NSMB.Sound;
using NSMB.Utilities.Extensions;
using Quantum;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;

namespace NSMB.Entities.Powerup {
    public unsafe class CloudBlockAnimator : QuantumEntityViewComponent {

        private static readonly int StateIdle = Animator.StringToHash("idle");
        private static readonly int StateSummon = Animator.StringToHash("summon");
        private static readonly int StateSoftSquish = Animator.StringToHash("soft-squish");
        private static readonly int StateHardSquish = Animator.StringToHash("hard-squish");
        private static readonly int StateDestroy = Animator.StringToHash("destroy");

        [SerializeField] private Animator animator;
        [SerializeField] private GameObject spawnParticles;
        [SerializeField] private AudioSource sfx;

        private ushort previousAnimationCounter;
        private ushort previousSoundCounter;

        public void OnValidate() {
            this.SetIfNull(ref animator, UnityExtensions.GetComponentType.Children);
            this.SetIfNull(ref sfx);
        }

        public override void OnActivate(Frame f) {
            if (!f.Unsafe.TryGetPointer(EntityRef, out CloudBlock* cloudBlock)) {
                return;
            }

            previousAnimationCounter = cloudBlock->AnimationCounter;
            previousSoundCounter = cloudBlock->SoundCounter;

            PlayAnimation(cloudBlock->Animation);
            PlaySpawnEffects(f, cloudBlock);
        }

        public override void OnUpdateView() {
            if (!PredictedFrame.Unsafe.TryGetPointer(EntityRef, out CloudBlock* cloudBlock)) {
                return;
            }

            if (animator) {
                animator.enabled = PredictedFrame.Global->GameState == GameState.Playing;
            }

            if (cloudBlock->AnimationCounter != previousAnimationCounter) {
                previousAnimationCounter = cloudBlock->AnimationCounter;
                PlayAnimation(cloudBlock->Animation);
            }

            if (cloudBlock->SoundCounter != previousSoundCounter) {
                previousSoundCounter = cloudBlock->SoundCounter;
                PlaySound(PredictedFrame, cloudBlock->QueuedSound, cloudBlock);
            }
        }

        private void PlaySpawnEffects(Frame f, CloudBlock* cloudBlock) {
            if (spawnParticles) {
                spawnParticles.SetActive(false);
                spawnParticles.SetActive(true);
            }

            PlaySound(f, f.FindAsset(cloudBlock->Asset).SpawnSound, cloudBlock);
        }

        private void PlayAnimation(CloudBlockAnimation animation) {
            if (!animator) {
                return;
            }

            animator.Play(animation switch {
                CloudBlockAnimation.Summon => StateSummon,
                CloudBlockAnimation.SoftSquish => StateSoftSquish,
                CloudBlockAnimation.HardSquish => StateHardSquish,
                CloudBlockAnimation.Destroy => StateDestroy,
                _ => StateIdle,
            }, 0, 0f);
        }

        private void PlaySound(Frame f, SoundEffect sound, CloudBlock* cloudBlock) {
            if (IsReplayFastForwarding) {
                return;
            }

            CloudBlockProjectileAsset asset = f.FindAsset(cloudBlock->Asset);
            if (sfx) {
                sfx.PlayOneShot(sound, new[] { asset });
            } else if (SoundEffectResolver.Instance) {
                SoundEffectResolver.Instance.PlayOneShotGlobally(sound, new[] { asset });
            }
        }
    }
}
