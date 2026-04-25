using Quantum;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;

namespace NSMB.Entities.World {
    public class BanzaiBillLauncherAnimator : QuantumEntityViewComponent {

        //---Serialized Variables
        [SerializeField] private Animation headAnimation;
        [SerializeField] private SpriteRenderer headRenderer;
        [SerializeField] private Transform headOrigin;
        [SerializeField] private ParticleSystem bulletBillShoot;

        public void Start() {
            QuantumEvent.Subscribe<EventBulletBillLauncherShoot>(this, OnBulletBillLauncherShoot, FilterOutReplayFastForward);
        }

        public override unsafe void OnUpdateView() {
            Frame f = Game.Frames.Verified;
            if (!f.Exists(EntityRef)
                || f.Global->GameState < GameState.Playing) {
                return;
            }

            headRenderer.enabled = true;
            headOrigin.transform.localPosition = Vector3.zero;
        }

        private unsafe void OnBulletBillLauncherShoot(EventBulletBillLauncherShoot e) {
            if (e.Entity != EntityRef) {
                return;
            }

            headAnimation.Play();
            bulletBillShoot.transform.position = headOrigin.position + (e.Right ? new Vector3(0.25f, 0.25f, 0) : new Vector3(-0.25f, 0.25f, 0));
            bulletBillShoot.Play();
        }
    }
}
