using Photon.Deterministic;
using Quantum.Collections;

namespace Quantum {
    public unsafe class LiquidSystem : SystemSignalsOnly, ISignalOnTriggerEnter2D, ISignalOnTrigger2D, ISignalOnTriggerExit2D,
        ISignalOnComponentRemoved<Transform2D> {

        private const int FrozenWaterBreakExtensionFrames = 90;

        public void OnTriggerEnter2D(Frame f, TriggerInfo2D info) {
            EntityRef physicsObjectEntity = info.Entity;
            EntityRef liquidEntity = info.Other;

            if (!f.Unsafe.TryGetPointer(liquidEntity, out Liquid* liquidComponent)
                || !f.Unsafe.TryGetPointer(liquidEntity, out Transform2D* liquidTransform)
                || !f.Unsafe.TryGetPointer(physicsObjectEntity, out Transform2D* entityTransform)
                || !f.Unsafe.TryGetPointer(physicsObjectEntity, out PhysicsCollider2D* entityCollider)
                || !f.Unsafe.TryGetPointer(physicsObjectEntity, out PhysicsObject* entityPhysicsObject)) {
                return;
            }

            bool isFrozenWater = liquidComponent->LiquidType == LiquidType.FrozenWater;

            if (isFrozenWater && f.Unsafe.TryGetPointer(physicsObjectEntity, out Projectile* projectile)) {
                var projectileAsset = f.FindAsset(projectile->Asset);

                if (projectileAsset.Effect == ProjectileEffectType.Fire) {
                    // Fireballs evaporate immediately, with a satisfying hiss.
                    f.Events.FrozenWaterSizzled(liquidEntity, entityTransform->Position);
                    ProjectileSystem.Destroy(f, physicsObjectEntity, projectileAsset.DestroyParticleEffect);
                    return;
                }

                if (projectileAsset.Effect is ProjectileEffectType.Hammer or ProjectileEffectType.Boomerang
                    && (!f.Unsafe.TryGetPointer(physicsObjectEntity, out Freezable* projectileFreezable) || !projectileFreezable->IsFrozen(f))) {
                    // Hammers and boomerangs get encased in ice instead of despawning.
                    IceBlockSystem.Freeze(f, physicsObjectEntity);
                }
            }

            bool callSignals = true;
            if (f.Unsafe.TryGetPointer(physicsObjectEntity, out Interactable* interactable)
                && interactable->ColliderDisabled) {
                callSignals = false;
            } 

            FP surface = liquidComponent->GetSurfaceHeight(liquidTransform);
            FP checkHeight = entityTransform->Position.Y + entityCollider->Shape.Centroid.Y - (entityPhysicsObject->Velocity.Y * f.DeltaTime);
            bool isEntityUnderwater = checkHeight <= surface;

            QHashSet<EntityRef> splashed = f.ResolveHashSet(liquidComponent->SplashedEntities);
            if (!splashed.Contains(physicsObjectEntity)) {
                // Enter splash
                splashed.Add(physicsObjectEntity);

                var colliders = f.ResolveHashSet(entityPhysicsObject->LiquidContacts);
                colliders.Add(liquidEntity);

                if (callSignals) {
                    bool doSplash = !isEntityUnderwater;
                    f.Signals.OnTryLiquidSplash(physicsObjectEntity, liquidEntity, false, &doSplash);
                    if (doSplash) {
                        f.Events.LiquidSplashed(liquidEntity, physicsObjectEntity, FPMath.Abs(entityPhysicsObject->Velocity.Y), new FPVector2(entityTransform->Position.X, surface), false);
                    }
                }
            }

            if (isFrozenWater) {
                HandleFrozenWaterContact(f, physicsObjectEntity);
            }
        }

        public void OnTrigger2D(Frame f, TriggerInfo2D info) {
            EntityRef physicsObjectEntity = info.Entity;
            EntityRef liquidEntity = info.Other;

            if (!f.Unsafe.TryGetPointer(liquidEntity, out Liquid* liquidComponent)
                || !f.Unsafe.TryGetPointer(liquidEntity, out Transform2D* liquidTransform)
                || !f.Unsafe.TryGetPointer(physicsObjectEntity, out Transform2D* entityTransform)
                || !f.Unsafe.TryGetPointer(physicsObjectEntity, out PhysicsCollider2D* entityCollider)
                || !f.Unsafe.TryGetPointer(physicsObjectEntity, out PhysicsObject* entityPhysicsObject)) {
                return;
            }

            FP surface = liquidComponent->GetSurfaceHeight(liquidTransform);
            FP checkHeight = entityTransform->Position.Y + entityCollider->Shape.Centroid.Y;
            bool isEntityUnderwater = checkHeight <= surface;

            QHashSet<EntityRef> underwater = f.ResolveHashSet(liquidComponent->UnderwaterEntities);
            if (isEntityUnderwater && !underwater.Contains(physicsObjectEntity)) {
                // Enter state
                underwater.Add(physicsObjectEntity);
                f.Signals.OnEntityEnterExitLiquid(physicsObjectEntity, liquidEntity, true);
            } else if (!isEntityUnderwater && underwater.Contains(physicsObjectEntity)) {
                // Exit state
                underwater.Remove(physicsObjectEntity);
                f.Signals.OnEntityEnterExitLiquid(physicsObjectEntity, liquidEntity, false);
            }

            if (liquidComponent->LiquidType == LiquidType.FrozenWater) {
                // Re-freeze as soon as the post-thaw invincibility frames run out.
                HandleFrozenWaterContact(f, physicsObjectEntity);
            }
        }

        public void OnTriggerExit2D(Frame f, ExitInfo2D info) {
            EntityRef physicsObjectEntity = info.Entity;
            EntityRef liquidEntity = info.Other;

            if (!f.Unsafe.TryGetPointer(liquidEntity, out Liquid* liquidComponent)
                || !f.Unsafe.TryGetPointer(liquidEntity, out Transform2D* liquidTransform)
                || !f.Unsafe.TryGetPointer(physicsObjectEntity, out Transform2D* entityTransform)
                || !f.Unsafe.TryGetPointer(physicsObjectEntity, out PhysicsCollider2D* entityCollider)
                || !f.Unsafe.TryGetPointer(physicsObjectEntity, out PhysicsObject* entityPhysicsObject)) {
                return;
            }

            FP surface = liquidComponent->GetSurfaceHeight(liquidTransform);
            FP checkHeight = entityTransform->Position.Y + entityCollider->Shape.Centroid.Y;
            bool isEntityUnderwater = checkHeight <= surface;

            bool callSignals = true;
            if (f.Unsafe.TryGetPointer(physicsObjectEntity, out Interactable* interactable)
                && interactable->ColliderDisabled) {
                callSignals = false;
            }

            QHashSet<EntityRef> splashed = f.ResolveHashSet(liquidComponent->SplashedEntities);
            QHashSet<EntityRef> underwater = f.ResolveHashSet(liquidComponent->UnderwaterEntities);

            if (splashed.Remove(physicsObjectEntity)) {
                var colliders = f.ResolveHashSet(entityPhysicsObject->LiquidContacts);
                colliders.Remove(liquidEntity);

                if (callSignals) {
                    bool doSplash = !isEntityUnderwater && (f.Number - entityTransform->PositionTeleportFrame) > 3;
                    f.Signals.OnTryLiquidSplash(physicsObjectEntity, liquidEntity, true, &doSplash);

                    if (doSplash) {
                        f.Events.LiquidSplashed(liquidEntity, physicsObjectEntity, FPMath.Abs(entityPhysicsObject->Velocity.Y), new FPVector2(entityTransform->Position.X, surface), true);
                    }
                }
            }

            if (underwater.Remove(physicsObjectEntity)) {
                f.Signals.OnEntityEnterExitLiquid(physicsObjectEntity, liquidEntity, false);
            }
        }

        private static void HandleFrozenWaterContact(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out IceBlock* iceBlock)) {
                // Frozen entities (including the player) collide with liquids through their ice block.
                entity = iceBlock->Entity;
            }

            HandleFrozenWaterMarioContact(f, entity);
        }

        private static void HandleFrozenWaterMarioContact(Frame f, EntityRef entity) {
            if (!f.Unsafe.TryGetPointer(entity, out MarioPlayer* mario)
                || !f.Unsafe.TryGetPointer(entity, out Freezable* freezable)
                || mario->IsDead
                || f.Exists(mario->CurrentPipe)) {
                return;
            }

            if (freezable->IsFrozen(f)) {
                if (mario->FrozenByFrozenWater || mario->DamageOnUnfreeze) {
                    // Already frozen by this water, or already marked for damage.
                    return;
                }

                // Frozen by something else (an iceball): make the ice last longer and
                // hurt on thaw.
                if (f.Unsafe.TryGetPointer(freezable->FrozenCubeEntity, out IceBlock* iceBlock)) {
                    iceBlock->AutoBreakFrames = (byte) System.Math.Min(byte.MaxValue,
                        iceBlock->AutoBreakFrames + FrozenWaterBreakExtensionFrames);
                }
                mario->DamageOnUnfreeze = true;
                return;
            }

            if (!mario->IsDamageable(f)) {
                // Swim freely during damage invincibility.
                return;
            }

            IceBlockSystem.Freeze(f, entity);
            mario->FrozenByFrozenWater = true;
            mario->DamageOnUnfreeze = false;
        }

        public unsafe void OnRemoved(Frame f, EntityRef entity, Transform2D* component) {
            foreach ((_, var liquidPtr) in f.Unsafe.GetComponentBlockIterator<Liquid>()) {
                f.ResolveHashSet(liquidPtr->SplashedEntities).Remove(entity);
                f.ResolveHashSet(liquidPtr->UnderwaterEntities).Remove(entity);
            }
        }
    }
}