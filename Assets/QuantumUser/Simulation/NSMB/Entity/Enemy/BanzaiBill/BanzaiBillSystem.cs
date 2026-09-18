using Photon.Deterministic;

namespace Quantum {
    public unsafe class BanzaiBillSystem : SystemMainThreadEntityFilter<BanzaiBill, BanzaiBillSystem.Filter>, ISignalOnBobombExplodeEntity, ISignalOnIceBlockBroken {
        public struct Filter {
            public EntityRef Entity;
            public BanzaiBill* BanzaiBill;
            public Transform2D* Transform;
            public Enemy* Enemy;
            public PhysicsObject* PhysicsObject;
            public Freezable* Freezable;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<BanzaiBill, MarioPlayer>(f, OnBanzaiBillMarioInteraction);
            f.Context.Interactions.Register<BanzaiBill, Projectile>(f, OnBanzaiBillProjectileInteraction);
            f.Context.Interactions.Register<BanzaiBill, IceBlock>(f, OnBanzaiBillIceBlockInteraction);
            f.Context.Interactions.Register<Koopa, BanzaiBill>(f, OnKoopaBanzaiBillInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var enemy = filter.Enemy;
            var banzaiBill = filter.BanzaiBill;

            if (!enemy->IsAlive) {
                if (banzaiBill->DespawnFrames == 0) {
                    // Just died.
                    banzaiBill->DespawnFrames = 255;
                }

                if (QuantumUtils.Decrement(ref banzaiBill->DespawnFrames)) {
                    f.Destroy(filter.Entity);
                }
                return;
            }

            if (filter.Freezable->IsFrozen(f)) {
                // Banzai Bills stay pinned mid-air while frozen instead of dropping:
                // keep the ice cube frozen in place until its timer breaks it.
                // (Skipped while held or sliding so throws still work.)
                EntityRef iceEntity = filter.Freezable->FrozenCubeEntity;
                if (f.Unsafe.TryGetPointer(iceEntity, out IceBlock* ice)
                    && ice->TimerEnabled(f, iceEntity)
                    && f.Unsafe.TryGetPointer(iceEntity, out PhysicsObject* icePhysics)) {
                    icePhysics->IsFrozen = true;
                    icePhysics->Velocity = FPVector2.Zero;
                }
                return;
            }

            var physicsObject = filter.PhysicsObject;
            physicsObject->DisableCollision = true;
            physicsObject->Velocity.X = banzaiBill->Speed * (enemy->FacingRight ? 1 : -1);
        }

        #region Interactions
        public static void OnBanzaiBillMarioInteraction(Frame f, EntityRef banzaiBillEntity, EntityRef marioEntity) {
            var banzaiBill = f.Unsafe.GetPointer<BanzaiBill>(banzaiBillEntity);
            var banzaiBillTransform = f.Unsafe.GetPointer<Transform2D>(banzaiBillEntity);
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity);
            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);

            QuantumUtils.UnwrapWorldLocations(f, banzaiBillTransform->Position + FPVector2.Up * FP._0_10, marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;
            bool attackedFromAbove = FPVector2.Dot(damageDirection, FPVector2.Up) > 0;

            // Only starman or mega mushroom players can kill it.
            if (mario->IsStarmanOrMega) {
                banzaiBill->Kill(f, banzaiBillEntity, marioEntity, EnemyKillReason.Special);
                return;
            }

            if (attackedFromAbove) {
                // Launch the player away at run speed with an upwards bounce, and scale the bill.
                var physicsInfo = f.FindAsset(mario->PhysicsAsset);
                FP runSpeed = physicsInfo.WalkMaxVelocity[physicsInfo.RunSpeedStage];
                int direction = theirPos.X >= ourPos.X ? 1 : -1;
                marioPhysicsObject->Velocity.X = runSpeed * direction;
                mario->DoEntityBounce = true;
                mario->IsDrilling = false;

                f.Events.BanzaiBillHitByProjectile(banzaiBillEntity);
            } else if (!mario->IsCrouchedInShell && mario->IsDamageable(f)) {
                mario->Powerdown(f, marioEntity, false, banzaiBillEntity);
            }
        }

        public static bool OnBanzaiBillIceBlockInteraction(Frame f, EntityRef banzaiBillEntity, EntityRef iceBlockEntity, PhysicsContact contact) {
            var banzaiBill = f.Unsafe.GetPointer<BanzaiBill>(banzaiBillEntity);
            var iceBlock = f.Unsafe.GetPointer<IceBlock>(iceBlockEntity);

            FP upDot = FPVector2.Dot(contact.Normal, FPVector2.Up);
            if (iceBlock->IsSliding
                && upDot < Constants.PhysicsGroundMaxAngleCos) {

                banzaiBill->Kill(f, banzaiBillEntity, iceBlockEntity, EnemyKillReason.Special);
            }
            return false;
        }

        public static void OnBanzaiBillProjectileInteraction(Frame f, EntityRef banzaiBillEntity, EntityRef projectileEntity) {
            var projectileAsset = f.FindAsset(f.Unsafe.GetPointer<Projectile>(projectileEntity)->Asset);

            switch (projectileAsset.Effect) {
                case ProjectileEffectType.Freeze:
                    // Frozen for 30 frames via the Freezable prototype, not killed on break.
                    EntityRef iceEntity = IceBlockSystem.Freeze(f, banzaiBillEntity, true);
                    if (f.Unsafe.TryGetPointer(iceEntity, out IceBlock* ice)) {
                        // Break in place when the timer expires instead of dropping
                        // out of the sky like other flying frozen enemies.
                        ice->IsFlying = false;
                    }
                    break;
                case ProjectileEffectType.Fire:
                    f.Events.BanzaiBillHitByProjectile(banzaiBillEntity);
                    break;
                case ProjectileEffectType.Hammer:
                    // Hammer bounces off: OnProjectileHitEntity emits EnemyKicked (bounce SFX)
                    // since the hammer asset has Bounce enabled.
                    f.Events.BanzaiBillHitByProjectile(banzaiBillEntity);
                    break;
                case ProjectileEffectType.Boomerang:
                    // Does nothing, for now.
                    return;
            }

            f.Signals.OnProjectileHitEntity(projectileEntity, banzaiBillEntity);
        }

        public static void OnKoopaBanzaiBillInteraction(Frame f, EntityRef koopaEntity, EntityRef banzaiBillEntity) {
            var koopa = f.Unsafe.GetPointer<Koopa>(koopaEntity);
            var holdable = f.Unsafe.GetPointer<Holdable>(koopaEntity);

            // A kicked shell bounces off a Banzai Bill instead of killing it.
            if (koopa->IsKicked || f.Exists(holdable->Holder)) {
                f.Events.BanzaiBillHitByProjectile(banzaiBillEntity);
            }
        }
        #endregion

        #region Signals
        public void OnBobombExplodeEntity(Frame f, EntityRef bobomb, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out BanzaiBill* banzaiBill)) {
                banzaiBill->Kill(f, entity, bobomb, EnemyKillReason.Special);
            }
        }
        public void OnIceBlockBroken(Frame f, EntityRef brokenIceBlock, IceBlockBreakReason breakReason, EntityRef attacker) {
            var iceBlock = f.Unsafe.GetPointer<IceBlock>(brokenIceBlock);
            if (f.Unsafe.TryGetPointer(iceBlock->Entity, out BanzaiBill* banzaiBill)) {
                // Survives the break (unlike BulletBill): re-enable interactions,
                // which Freeze disabled, and pop with a scale pulse.
                f.Unsafe.GetPointer<Interactable>(iceBlock->Entity)->ColliderDisabled = false;
                f.Events.BanzaiBillHitByProjectile(iceBlock->Entity);
            }
        }
        #endregion
    }
}
