using NSMB.Utilities.Extensions;
using Quantum;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;

namespace Quantum {
    public unsafe class CloudBlockAnimator : QuantumEntityViewComponent {

        private static readonly int AnimSummon = Animator.StringToHash("summon");
        private static readonly int AnimIdle = Animator.StringToHash("idle");
        private static readonly int AnimSoftSquish = Animator.StringToHash("soft-squish");
        private static readonly int AnimHardSquish = Animator.StringToHash("hard-squish");
        private static readonly int AnimDestroy = Animator.StringToHash("destroy");

        [SerializeField] private Animator animator;
        [SerializeField] private GameObject destroyParticle;

        public void OnValidate() {
            this.SetIfNull(ref animator, UnityExtensions.GetComponentType.Children);
        }

        public void Start() {
            QuantumEvent.Subscribe<EventCloudBlockSquished>(this, OnCloudBlockSquished, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventCloudBlockHardSquished>(this, OnCloudBlockHardSquished, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventCloudBlockDestroyed>(this, OnCloudBlockDestroyed, FilterOutReplayFastForward);
        }

        public override void OnActivate(Frame f) {
            if (animator != null) {
                animator.Play(AnimSummon);
            }
            SpawnDestroyParticle();
        }

        public override void OnUpdateView() {
            Frame f = PredictedFrame;
            if (!f.Exists(EntityRef)) {
                return;
            }

            if (animator == null) {
                return;
            }

            var cloudBlock = f.Unsafe.GetPointer<CloudBlock>(EntityRef);
            int currentState = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
            bool inOneShot = currentState == AnimSoftSquish || currentState == AnimHardSquish;

            if (cloudBlock->IsDestroying) {
                if (currentState != AnimDestroy) {
                    animator.Play(AnimDestroy);
                }
            } else if (cloudBlock->IsSummoning) {
                if (currentState != AnimSummon) {
                    animator.Play(AnimSummon);
                }
            } else if (!inOneShot && currentState != AnimIdle) {
                animator.Play(AnimIdle);
            }
        }

        public override void OnDeactivate() {
            SpawnDestroyParticle();
        }

        private void OnCloudBlockSquished(EventCloudBlockSquished e) {
            if (e.Entity != EntityRef) {
                return;
            }
            if (animator != null) {
                animator.Play(AnimSoftSquish);
            }
        }

        private void OnCloudBlockHardSquished(EventCloudBlockHardSquished e) {
            if (e.Entity != EntityRef) {
                return;
            }
            if (animator != null) {
                animator.Play(AnimHardSquish);
            }
        }

        private void OnCloudBlockDestroyed(EventCloudBlockDestroyed e) {
            if (e.Entity != EntityRef) {
                return;
            }
            if (animator != null) {
                animator.Play(AnimDestroy);
            }
        }

        private void SpawnDestroyParticle() {
            if (destroyParticle == null) {
                return;
            }
            var instance = Instantiate(destroyParticle, transform.position, transform.rotation);
            var ps = instance.GetComponent<ParticleSystem>();
            if (ps != null) {
                ps.Play();
                Destroy(instance, ps.main.duration + ps.main.startLifetime.constantMax);
            }
        }
    }
}