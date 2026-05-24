using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct CloudBlock {

        public readonly bool CanRunActions => !Destroying && ActionLockFrames == 0;

        public void Initialize(Frame f, EntityRef thisEntity, EntityRef owner, AssetRef<CloudBlockProjectileAsset> assetRef, CloudBlockProjectileAsset asset, FPVector2 spawnpoint) {
            var transform = f.Unsafe.GetPointer<Transform2D>(thisEntity);

            Asset = assetRef;
            Owner = owner;
            Lifetime = asset.LifetimeFrames;
            ActionLockFrames = asset.SummonInactiveFrames;
            DestroyFrames = 0;
            Destroying = false;
            Animation = CloudBlockAnimation.Summon;
            AnimationCounter++;
            SoundCounter = 0;

            transform->Position = spawnpoint;
        }

        public void PlayAnimation(CloudBlockAnimation animation) {
            if (!CanRunActions && animation is CloudBlockAnimation.SoftSquish or CloudBlockAnimation.HardSquish) {
                return;
            }

            Animation = animation;
            AnimationCounter++;
        }

        public void QueueSound(SoundEffect sound) {
            QueuedSound = sound;
            SoundCounter++;
        }
    }
}
