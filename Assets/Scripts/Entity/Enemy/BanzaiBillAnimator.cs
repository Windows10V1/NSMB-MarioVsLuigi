using NSMB.Sound;
using NSMB.Utilities;
using NSMB.Utilities.Extensions;
using Quantum;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;

namespace NSMB.Entities.Enemies {
    public unsafe class BanzaiBillAnimator : QuantumEntityViewComponent {

        //---Serialized Variables
        [SerializeField] private Transform modelRoot;
        [SerializeField] private ParticleSystem trailParticles;
        [SerializeField] private SoundEffectPlayer sfx;
        [SerializeField] private GameObject specialKillParticles;

        [SerializeField] private float fireballScaleSize = 0.2f;
        [SerializeField] private float spinSpeed = 180f;

        //---Private Variables
        private float fireballScaleTimer;
        private float spinAngle;

        public void OnValidate() {
            this.SetIfNull(ref sfx);
        }

        public void Start() {
            QuantumEvent.Subscribe<EventEnemyKilled>(this, OnEnemyKilled, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventPlayComboSound>(this, OnPlayComboSound, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventBanzaiBillHitByProjectile>(this, OnBanzaiBillHitByProjectile, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventEnemyKicked>(this, OnEnemyKicked, FilterOutReplayFastForward);
        }

        public override void OnActivate(Frame f) {
            if (!IsReplayFastForwarding) {
                sfx.PlayOneShot(SoundEffect.Enemy_BulletBill_Shoot);
            }
            trailParticles.Play();
        }

        public override void OnUpdateView() {
            Frame f = PredictedFrame;

            if (!f.Exists(EntityRef)) {
                return;
            }

            var enemy = f.Unsafe.GetPointer<Enemy>(EntityRef);
            var freezable = f.Unsafe.GetPointer<Freezable>(EntityRef);
            bool frozen = freezable->IsFrozen(f);

            modelRoot.gameObject.SetActive(enemy->IsActive);

            var emission = trailParticles.emission;
            emission.enabled = enemy->IsActive && !frozen;

            if (enemy->IsDead) {
                transform.rotation *= Quaternion.Euler(0, 0, 400f * (enemy->FacingRight ? -1 : 1) * Time.deltaTime);
            } else {
                transform.rotation = Quaternion.identity;
            }

            float scale = 1 + Mathf.Abs(Mathf.Sin(fireballScaleTimer * 10 * Mathf.PI)) * fireballScaleSize;
            transform.localScale = Vector3.one * scale;
            fireballScaleTimer = Mathf.Max(0, fireballScaleTimer - Time.deltaTime);

            // Spin the model while flying (paused when frozen); base prefab
            // rotation faces left, mirrored for right.
            if (enemy->IsAlive && !frozen) {
                spinAngle += spinSpeed * Time.deltaTime;
            }
            modelRoot.localEulerAngles = new Vector3(0, enemy->FacingRight ? 90 : -90, spinAngle);
            Vector2 pos = trailParticles.transform.localPosition;
            pos.x = Mathf.Abs(pos.x) * (enemy->FacingRight ? -1 : 1);
            trailParticles.transform.localPosition = pos;
        }

        private void OnBanzaiBillHitByProjectile(EventBanzaiBillHitByProjectile e) {
            if (e.Entity != EntityRef) {
                return;
            }

            fireballScaleTimer = 0.3f;
        }

        private void OnEnemyKilled(EventEnemyKilled e) {
            if (e.Enemy != EntityRef) {
                return;
            }

            if (e.KillReason is EnemyKillReason.Special or EnemyKillReason.Groundpounded) {
                if (specialKillParticles) {
                    Instantiate(specialKillParticles, transform.position, Quaternion.identity);
                }
            } else {
                // sfx.PlayOneShot(SoundEffect.Enemy_Generic_Stomp);
            }
        }

        // MarioPlayerAnimator only plays the bounce sound for the player itself,
        // so hammers bouncing off a Banzai Bill need their own handler.
        private void OnEnemyKicked(EventEnemyKicked e) {
            if (e.Entity != EntityRef) {
                return;
            }

            sfx.PlayOneShot(SoundEffect.Powerup_HammerSuit_Bounce);
        }

        private void OnPlayComboSound(EventPlayComboSound e) {
            if (e.Entity != EntityRef) {
                return;
            }

            sfx.PlayOneShot(QuantumViewUtils.GetComboSoundEffect(e.Combo));
        }
    }
}
