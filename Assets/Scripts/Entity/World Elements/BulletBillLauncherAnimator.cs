using Quantum;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;

namespace NSMB.Entities.World {
    public class BulletBillLauncherAnimator : BreakableObjectAnimator {

        //---Serialized Variables
        [SerializeField] private Animation headAnimation;
        [SerializeField] private SpriteRenderer headRenderer;
        [SerializeField] private Transform headOrigin;
        [SerializeField] private ParticleSystem bulletBillShoot;

        public override void Start() {
            base.Start();
            QuantumEvent.Subscribe<EventBulletBillLauncherShoot>(this, OnBulletBillLauncherShoot, FilterOutReplayFastForward);
        }

        public override unsafe void OnUpdateView() {
            base.OnUpdateView();

            Frame f = Game.Frames.Verified;
            if (!f.Exists(EntityRef)
                || f.Global->GameState < GameState.Playing) {
                return;
            }

            // BreakableObject is optional: Banzai Bill launchers aren't breakable,
            // leave the head at its prefab position in that case.
            _isBanzaiLauncher = !f.Unsafe.TryGetPointer(EntityRef, out BreakableObject* breakable);
            if (_isBanzaiLauncher) {
                return;
            }
            headRenderer.enabled = breakable->CurrentHeight > 0;
            headOrigin.transform.localPosition = Vector3.up * (breakable->CurrentHeight.AsFloat - 1f);
        }

        protected override void OnBreakableObjectBroken(EventBreakableObjectBroken e) {
            if (e.Entity != EntityRef) {
                return;
            }

            base.OnBreakableObjectBroken(e);
            headRenderer.enabled = false;
        }

        private Coroutine overlayResetRoutine;
        private bool _isBanzaiLauncher;

        public Vector3 HeadCenter {
            get {
                if (headRenderer) {
                    return headRenderer.bounds.center;
                }
                return transform.position;
            }
        }

        public bool IsBanzaiLauncher => _isBanzaiLauncher;

        private unsafe void OnBulletBillLauncherShoot(EventBulletBillLauncherShoot e) {
            if (e.Entity != EntityRef) {
                return;
            }

            bool isBanzai = !PredictedFrame.Has<BreakableObject>(e.Entity);

            if (headAnimation) {
                headAnimation.Play();
            }

            // Regular launchers play their own puff particle; Banzai puffs are
            // spawned centrally by MiscParticles (scaled up).
            if (!isBanzai && bulletBillShoot) {
                bulletBillShoot.transform.position = headOrigin.position + (e.Right ? new Vector3(0.25f, 0.25f, 0) : new Vector3(-0.25f, 0.25f, 0));
                bulletBillShoot.Play();
            }

            // Banzai launchers pop in front of the bill only while shooting.
            if (isBanzai && sRenderer) {
                Vector3 graphicsPos = sRenderer.transform.localPosition;
                graphicsPos.z = -1.1f;
                sRenderer.transform.localPosition = graphicsPos;

                if (overlayResetRoutine != null) {
                    StopCoroutine(overlayResetRoutine);
                }
                overlayResetRoutine = StartCoroutine(ResetShootOverlay());
            }
        }

        private System.Collections.IEnumerator ResetShootOverlay() {
            float duration = headAnimation && headAnimation.clip ? headAnimation.clip.length : 0.35f;
            yield return new UnityEngine.WaitForSeconds(duration);

            if (sRenderer) {
                Vector3 graphicsPos = sRenderer.transform.localPosition;
                graphicsPos.z = 0f;
                sRenderer.transform.localPosition = graphicsPos;
            }
            overlayResetRoutine = null;
        }
    }
}
