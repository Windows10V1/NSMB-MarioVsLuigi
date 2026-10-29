using NSMB.Utilities;
using Quantum;
using System;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;

namespace NSMB.Particles {
    public unsafe class MiscParticles : QuantumSceneViewComponent {

        //---Static
        public static MiscParticles Instance { get; private set; }

        //---Serialized Variables
        [SerializeField] private ParticlePair[] particles;

        public void Start() {
            Instance = this;

            QuantumEvent.Subscribe<EventProjectileDestroyed>(this, OnProjectileDestroyed, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventCollectableDespawned>(this, OnCollectableDespawned, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventEnemyKicked>(this, OnEnemyKicked, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventBoomerangPierced>(this, OnBoomerangPierced, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventEnemyDespawnedOffscreen>(this, OnEnemyDespawnedOffscreen, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventMarioPlayerBlueShellStomped>(this, OnMarioPlayerBlueShellStomped, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventBulletBillLauncherShoot>(this, OnBulletBillLauncherShoot, FilterOutReplayFastForward);
        }

        private bool TryGetParticlePair(ParticleEffect particleEffect, out ParticlePair particlePair) {
            foreach (var pair in particles) {
                if (particleEffect == pair.particle) {
                    particlePair = pair;
                    return true;
                }
            }
            particlePair = null;
            return false;
        }

        public void Play(ParticleEffect particle, Vector3 position) {
            if (TryGetParticlePair(particle, out ParticlePair pp)) {
                Instantiate(pp.prefab, position + pp.offset, Quaternion.identity);
            }
        }

        private void OnProjectileDestroyed(EventProjectileDestroyed e) {
            Play(e.Particle, e.Position.ToUnityVector3());
        }

        private void OnCollectableDespawned(EventCollectableDespawned e) {
            if (!e.Collected) {
                Play(ParticleEffect.Puff, e.Position.ToUnityVector3());
            }
        }

        private void OnEnemyKicked(EventEnemyKicked e) {
            QuantumEntityView view = Updater.GetView(e.Entity);
            if (view) {
                Instantiate(
                    Enums.PrefabParticle.Enemy_HardKick.GetGameObject(),
                    view.transform.position + (Vector3.back * 5) + (Vector3.up * 0.1f),
                    Quaternion.identity);
            }
        }

        private void OnBoomerangPierced(EventBoomerangPierced e) {
            QuantumEntityView view = Updater.GetView(e.Entity);
            if (view) {
                Instantiate(
                    Enums.PrefabParticle.BulletBillShoot.GetGameObject(),
                    view.transform.position + (Vector3.back * 5) + (Vector3.up * 0.1f),
                    Quaternion.identity);
            }
        }

        private void OnEnemyDespawnedOffscreen(EventEnemyDespawnedOffscreen e) {
            Play(ParticleEffect.Puff, e.Position.ToUnityVector3());
        }

        private void OnMarioPlayerBlueShellStomped(EventMarioPlayerBlueShellStomped e) {
            QuantumEntityView view = Updater.GetView(e.Entity);
            if (view) {
                Instantiate(
                    Enums.PrefabParticle.Enemy_HardKick.GetGameObject(),
                    view.transform.position + (Vector3.back * 5) + (Vector3.up * 0.1f),
                    Quaternion.identity);
            }
        }

        private void OnBulletBillLauncherShoot(EventBulletBillLauncherShoot e) {
            QuantumEntityView view = Updater.GetView(e.Entity);
            if (!view) {
                return;
            }

            var launcherAnimator = view.GetComponent<NSMB.Entities.World.BulletBillLauncherAnimator>();
            if (!launcherAnimator || !launcherAnimator.IsBanzaiLauncher) {
                return;
            }

            GameObject puff = Instantiate(
                Enums.PrefabParticle.BulletBillShoot.GetGameObject(),
                launcherAnimator.HeadCenter + (Vector3.back * 5),
                Quaternion.identity);
            var puffSystems = puff.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var puffParticles in puffSystems) {
                if (puffParticles) {
                    puffParticles.transform.localScale = Vector3.one * 5f;
                }
            }
        }

        [Serializable]
        public class ParticlePair {
            public ParticleEffect particle;
            public GameObject prefab;
            public Vector3 offset;
        }
    }
}