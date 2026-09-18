using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct BanzaiBill {
        public void Initialize(Frame f, EntityRef entity, EntityRef owner, bool right) {
            var enemy = f.Unsafe.GetPointer<Enemy>(entity);
            enemy->FacingRight = right;
            enemy->IsActive = true;
            enemy->IsDead = false;

            Owner = owner;
        }

        public void Kill(Frame f, EntityRef banzaiBillEntity, EntityRef killerEntity, EnemyKillReason reason) {
            var enemy = f.Unsafe.GetPointer<Enemy>(banzaiBillEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(banzaiBillEntity);

            // Free the launcher slot immediately instead of waiting for the
            // corpse to despawn. Owner is cleared so OnRemoved won't count it twice.
            if (Owner != EntityRef.None
                && f.Unsafe.TryGetPointer(Owner, out BulletBillLauncher* launcher)
                && launcher->BulletBillCount > 0) {
                launcher->BulletBillCount--;
            }
            Owner = EntityRef.None;

            bool playSound;
            if (reason == EnemyKillReason.Normal) {
                // Stomped, fall off screen
                physicsObject->DisableCollision = true;
                physicsObject->Velocity = new FPVector2(
                    2 * (enemy->FacingRight ? 1 : -1),
                    0
                );
                physicsObject->Gravity = new FPVector2(0, -Constants._14_75);
                playSound = f.Has<Holdable>(killerEntity);
            } else {
                // Special kill, disappear.
                enemy->IsActive = false;
                physicsObject->IsFrozen = true;
                playSound = true;
            }

            if (playSound) {
                byte combo = ComboKeeper.IncrementOrDefault(f, killerEntity);
                f.Events.PlayComboSound(banzaiBillEntity, combo);
            }

            enemy->IsDead = true;
            f.Unsafe.GetPointer<Interactable>(banzaiBillEntity)->ColliderDisabled = true;

            var collider = f.Unsafe.GetPointer<PhysicsCollider2D>(banzaiBillEntity);
            FPVector2 center = f.Unsafe.GetPointer<Transform2D>(banzaiBillEntity)->Position + collider->Shape.Centroid;
            f.Events.EnemyKilled(banzaiBillEntity, killerEntity, reason, center);
        }
    }
}
