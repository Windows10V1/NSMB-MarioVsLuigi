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
            QuantumEvent.Subscribe<EventProjectileHitPlayer>(this, OnProjectileHitPlayer, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventEnemyDespawnedOffscreen>(this, OnEnemyDespawnedOffscreen, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventMarioPlayerBlueShellStomped>(this, OnMarioPlayerBlueShellStomped, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventMarioPlayerCollectedPowerup>(this, OnMarioPlayerCollectedPowerup, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventFrogSuitBounce>(this, OnFrogSuitBounce, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventPOWBlockSpawnLanded>(this, OnPOWBlockSpawnLanded, FilterOutReplayFastForward);
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

        private void OnFrogSuitBounce(EventFrogSuitBounce e) {
            QuantumEntityView view = Updater.GetView(e.Entity);
            if (view) {
                Instantiate(
                    Enums.PrefabParticle.Item_FrogSuitBounce.GetGameObject(),
                    view.transform.position + (Vector3.back * 5) + (Vector3.up * 0.1f),
                    Quaternion.identity
                );
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

        private void OnProjectileHitPlayer(EventProjectileHitPlayer e) {
            QuantumEntityView view = Updater.GetView(e.Entity);
            if (view && e.Effect == ProjectileEffectType.Boomerang) {
                Instantiate(
                    Enums.PrefabParticle.Enemy_KillPoof.GetGameObject(),
                    view.transform.position + (Vector3.back * 4) + (Vector3.up * 0.1f),
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
                    Quaternion.identity
                );
            }
        }

        private void OnPOWBlockSpawnLanded(EventPOWBlockSpawnLanded e) {
            QuantumEntityView view = Updater.GetView(e.Entity);
            if (view) {
                Instantiate(
                    Enums.PrefabParticle.Player_Groundpound.GetGameObject(),
                    view.transform.position + (Vector3.back * 5),
                    Quaternion.identity
                );
            }
        }

        private void OnMarioPlayerCollectedPowerup(EventMarioPlayerCollectedPowerup e) {
            if (e.Scriptable is OneUpPowerupAsset) {
                QuantumEntityView view = Updater.GetView(e.Entity);
                if (view) {
                    Instantiate(
                        Enums.PrefabParticle.Player_1Up.GetGameObject(),
                        view.transform.position,
                        Quaternion.identity
                    );
                }
            } else if (e.Scriptable is PoisonMushroomPowerupAsset) {
                QuantumEntityView view = Updater.GetView(e.Entity);
                if (view) {
                    Instantiate(
                        Enums.PrefabParticle.Player_WaterDust.GetGameObject(),
                        view.transform.position,
                        Quaternion.identity
                    );
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