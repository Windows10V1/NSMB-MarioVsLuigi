using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct CloudBlock {
        public void Initialize(Frame f, EntityRef cloudEntity, EntityRef owner, FPVector2 spawnPosition, AssetRef<CloudBlockAsset> assetRef) {
            Owner = owner;
            Asset = assetRef;
            var asset = f.FindAsset(assetRef);
            Lifetime = asset.LifetimeFrames;
            IsDestroying = false;
            IsSummoning = true;
            SquishCooldownFrames = 0;

            var transform = f.Unsafe.GetPointer<Transform2D>(cloudEntity);
            transform->Position = spawnPosition;
        }

        public void StartDestroying(Frame f, EntityRef cloudEntity) {
            if (IsDestroying) {
                return;
            }
            var asset = f.FindAsset(Asset);
            IsDestroying = true;
            IsSummoning = false;
            Lifetime = asset.DestroyAnimationFrames;
            f.Events.CloudBlockDestroyed(cloudEntity, f.Unsafe.GetPointer<Transform2D>(cloudEntity)->Position);
        }
    }
}
