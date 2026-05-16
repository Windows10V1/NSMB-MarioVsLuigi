using NSMB.Utilities.Extensions;
using Quantum;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;

namespace Quantum {
    public unsafe class CloudBlockAnimator : QuantumEntityViewComponent {

        private static readonly int TriggerDestroy = Animator.StringToHash("Destroy");
        private static readonly int TriggerLand = Animator.StringToHash("Land");
        private static readonly int TriggerGroundpound = Animator.StringToHash("Groundpound");

        [SerializeField] private Animator animator;
        [SerializeField] private GameObject summonParticle;
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
            SpawnSummonParticle();
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
        }

        private void OnCloudBlockSquished(EventCloudBlockSquished e) {
            if (e.Entity != EntityRef) {
                return;
            }
            Frame f = PredictedFrame;
            if (!f.Exists(EntityRef)) {
                return;
            }
            var cloudBlock = f.Unsafe.GetPointer<CloudBlock>(EntityRef);
            // Prevent soft squishes during summon/destroy
            if (cloudBlock->IsSummoning || cloudBlock->IsDestroying) {
                return;
            }
            if (animator != null) {
                animator.SetTrigger(TriggerLand);
                return;
            }
        }

        private void OnCloudBlockHardSquished(EventCloudBlockHardSquished e) {
            if (e.Entity != EntityRef) {
                return;
            }
            Frame f = PredictedFrame;
            if (!f.Exists(EntityRef)) {
                return;
            }
            var cloudBlock = f.Unsafe.GetPointer<CloudBlock>(EntityRef);
            // Prevent hard squishes during summon/destroy
            if (cloudBlock->IsSummoning || cloudBlock->IsDestroying) {
                return;
            }
            if (animator != null) {
                animator.SetTrigger(TriggerGroundpound);
                return;
            }
        }

        private void OnCloudBlockDestroyed(EventCloudBlockDestroyed e) {
            if (e.Entity != EntityRef) {
                return;
            }
            if (animator != null) {
                animator.SetTrigger(TriggerDestroy);
            }
            SpawnDestroyParticle();
        }

        private void SpawnSummonParticle() {
            if (summonParticle == null) {
                return;
            }
            var instance = Instantiate(summonParticle, transform.position, transform.rotation);
            var ps = instance.GetComponent<ParticleSystem>();
            if (ps != null) {
                ps.Play();
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
            }
        }
    }
}