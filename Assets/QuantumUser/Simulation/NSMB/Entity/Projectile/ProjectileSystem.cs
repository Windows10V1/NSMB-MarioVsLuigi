using Photon.Deterministic;

namespace Quantum {
    public unsafe class ProjectileSystem : SystemMainThreadEntityFilter<Projectile, ProjectileSystem.Filter>, ISignalOnProjectileHitEntity {
        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Projectile* Projectile;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* PhysicsCollider;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<Projectile, Projectile>(f, OnProjectileProjectileInteraction);
            f.Context.Interactions.Register<Projectile, Coin>(f, OnProjectileCoinInteraction);
            f.Context.Interactions.Register<Projectile, ObjectiveCoin>(f, OnProjectileObjectiveCoinInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var collider = filter.PhysicsCollider;
            var transform = filter.Transform;

            if (filter.Transform->Position.Y + collider->Shape.Centroid.Y + collider->Shape.Box.Extents.Y < stage.StageWorldMin.Y) {
                Destroy(f, filter.Entity, ParticleEffect.None);
                return;
            }

            var projectile = filter.Projectile;
            var asset = f.FindAsset(projectile->Asset);

            // Handle boomerang-specific logic
            if (asset.IsBoomerang) {
                HandleBoomerangUpdate(f, ref filter, asset);
            } else {
                // Normal projectile lifetime handling
                if (projectile->Lifetime > 0 && QuantumUtils.Decrement(ref projectile->Lifetime)) {
                    // Despawn via timer
                    Destroy(f, filter.Entity, asset.DestroyParticleEffect);
                    return;
                }
            }

            var physicsObject = filter.PhysicsObject;

            // Check to instant-despawn if spawned inside a wall
            if (!physicsObject->DisableCollision && !projectile->CheckedCollision) {
                if (PhysicsObjectSystem.BoxInGround(f, transform->Position, collider->Shape)) {
                    Destroy(f, filter.Entity, asset.DestroyParticleEffect);
                    return;
                }
                projectile->CheckedCollision = true;
            }

            HandleTileCollision(f, ref filter, asset);

            // For boomerangs, check if pull force should be applied
            bool boomerangPullForceActive = false;
            if (asset.IsBoomerang) {
                if (projectile->IsReturning()) {
                    boomerangPullForceActive = true;
                } else {
                    FP elapsedTime = (FP)projectile->Lifetime / 60;
                    boomerangPullForceActive = elapsedTime >= asset.BoomerangReturnDelay;
                }
            }

            // Don't override velocity if boomerang pull force is active (it's controlled by the pull force)
            if (!boomerangPullForceActive) {
                physicsObject->Velocity.X = projectile->Speed * (projectile->FacingRight ? 1 : -1);
            }

            if (asset.LockTo45Degrees) {
                physicsObject->TerminalVelocity = -projectile->Speed;
            }
        }

        private void HandleBoomerangUpdate(Frame f, ref Filter filter, ProjectileAsset asset) {
            var projectile = filter.Projectile;
            var physicsObject = filter.PhysicsObject;
            var transform = filter.Transform;

            // Increment frame counter
            projectile->Lifetime++;

            // Check if we should be returning
            if (!projectile->IsReturning()) {
                // Still in "going" phase - check if it's time to start pulling back
                FP elapsedTime = (FP)projectile->Lifetime / 60; // Assuming 60 FPS

                if (elapsedTime >= asset.BoomerangReturnDelay) {
                    // Time to start the return force
                    FP timeIntoReturn = elapsedTime - asset.BoomerangReturnDelay;
                    ApplyBoomerangPullForce(f, ref filter, asset, timeIntoReturn);
                }
            } else {
                // Already returning - apply max pull force
                ApplyBoomerangPullForce(f, ref filter, asset, FP.MaxValue); // Max value triggers peak force
            }
        }

        private void ApplyBoomerangPullForce(Frame f, ref Filter filter, ProjectileAsset asset, FP timeIntoReturn) {
            var projectile = filter.Projectile;
            var physicsObject = filter.PhysicsObject;
            var transform = filter.Transform;

            if (!f.Unsafe.TryGetPointer(projectile->Owner, out Transform2D* ownerTransform)) {
                return;
            }

            FPVector2 directionToOwner = (ownerTransform->Position + (FPVector2.Up * Constants._0_40)) - transform->Position;
            FP distanceToOwner = directionToOwner.Magnitude;

            if (distanceToOwner < FP.FromString("0.5")) {
                // Very close to owner - despawn without particle
                Destroy(f, filter.Entity, ParticleEffect.None);
                return;
            }

            directionToOwner = directionToOwner.Normalized;

            // Calculate pull force strength based on time
            // Gradually ramp up from 0 to peak speed
            FP pullForceStrength;
            if (timeIntoReturn >= FP.MaxValue || projectile->IsReturning()) {
                // Peak force - same speed as projectile moving toward owner
                pullForceStrength = asset.Speed;
            } else {
                // Gradually increase from 0 to peak over time
                pullForceStrength = timeIntoReturn * asset.BoomerangReturnAcceleration * asset.Speed;
                if (pullForceStrength > asset.Speed) {
                    pullForceStrength = asset.Speed;
                }
            }

            // Calculate target velocity (toward owner at pull force strength)
            FPVector2 targetVelocity = directionToOwner * pullForceStrength;
            
            // Smoothly transition current velocity to target velocity over time
            // This creates a smooth slowdown and reversal effect
            FP transitionTime = FP.FromString("1.0"); // 1 second to fully transition
            FP transitionProgress = FPMath.Min(timeIntoReturn / transitionTime, FP._1);
            
            FPVector2 currentVelocity = physicsObject->Velocity;
            // Lerp between current and target: current * (1 - t) + target * t
            FPVector2 smoothVelocity = new FPVector2(
                currentVelocity.X * (FP._1 - transitionProgress) + targetVelocity.X * transitionProgress,
                currentVelocity.Y * (FP._1 - transitionProgress) + targetVelocity.Y * transitionProgress
            );
            
            physicsObject->Velocity = smoothVelocity;

            // Disable gravity while returning
            physicsObject->Gravity = FPVector2.Zero;
        }


        public void HandleTileCollision(Frame f, ref Filter filter, ProjectileAsset asset) {
            var projectile = filter.Projectile;
            var physicsObject = filter.PhysicsObject;

            // Check for terrain collision
            bool hasTerrainCollision = false;
            if (!physicsObject->DisableCollision) {
                hasTerrainCollision = physicsObject->IsTouchingLeftWall
                    || physicsObject->IsTouchingRightWall
                    || physicsObject->IsTouchingCeiling
                    || physicsObject->IsTouchingGround
                    || PhysicsObjectSystem.BoxInGround(f, filter.Transform->Position, filter.PhysicsCollider->Shape);
            }

            // Special handling for boomerangs: switch to returning mode on terrain hit
            if (asset.IsBoomerang && hasTerrainCollision && !projectile->IsReturning()) {
                projectile->SetReturning();
                projectile->Lifetime = 0; // Reset frame counter for return phase with peak force
                return; // Don't despawn, just switch to return mode
            }

            // Despawn
            if (!physicsObject->DisableCollision) {
                if (physicsObject->IsTouchingLeftWall
                    || physicsObject->IsTouchingRightWall
                    || physicsObject->IsTouchingCeiling
                    || (physicsObject->IsTouchingGround && (!asset.Bounce || (projectile->HasBounced && asset.DestroyOnSecondBounce)))
                    || PhysicsObjectSystem.BoxInGround(f, filter.Transform->Position, filter.PhysicsCollider->Shape)) {

                    Destroy(f, filter.Entity, asset.DestroyParticleEffect);
                    return;
                }
            }

            // Bounce
            if (physicsObject->IsTouchingGround && asset.Bounce) {
                FP boost = asset.BounceStrength * FPMath.Abs(FPMath.Sin(physicsObject->FloorAngle * FP.Deg2Rad)) * FP._1_25;
                if ((physicsObject->FloorAngle > 0) == projectile->FacingRight) {
                    boost = 0;
                }

                physicsObject->Velocity.Y = asset.BounceStrength + boost;
                physicsObject->IsTouchingGround = false;
                projectile->HasBounced = true;
            }
        }

        private void OnProjectileProjectileInteraction(Frame f, EntityRef projectileEntityA, EntityRef projectileEntityB) {
            var projectileA = f.Unsafe.GetPointer<Projectile>(projectileEntityA);
            var projectileB = f.Unsafe.GetPointer<Projectile>(projectileEntityB);

            if (projectileA->Owner == projectileB->Owner) {
                return;
            }

            var projectileAssetA = f.FindAsset(projectileA->Asset);
            var projectileAssetB = f.FindAsset(projectileB->Asset);

            if ((projectileAssetA.Effect == ProjectileEffectType.Fire && projectileAssetB.Effect == ProjectileEffectType.Freeze)
                || (projectileAssetB.Effect == ProjectileEffectType.Fire && projectileAssetA.Effect == ProjectileEffectType.Freeze)) {
                // Fireball collided with Iceball. Destroy both.
                Destroy(f, projectileEntityA, projectileAssetA.DestroyParticleEffect);
                Destroy(f, projectileEntityB, projectileAssetB.DestroyParticleEffect);
            }
        }

        public static void Destroy(Frame f, EntityRef entity, ParticleEffect particle) {
            var transform = f.Unsafe.GetPointer<Transform2D>(entity);
            f.Events.ProjectileDestroyed(entity, particle, transform->Position);
            f.Destroy(entity);
        }

        public void OnProjectileHitEntity(Frame f, EntityRef projectileEntity, EntityRef hitEntity) {
            var projectile = f.Unsafe.GetPointer<Projectile>(projectileEntity);
            var projectileAsset = f.FindAsset(projectile->Asset);

            if (projectileAsset.DestroyOnHit) {
                Destroy(f, projectileEntity, projectileAsset.DestroyParticleEffect);
            } else if (projectileAsset.Bounce) {
                var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(projectileEntity);
                projectile->Speed *= Constants._0_85;
                physicsObject->Gravity *= Constants._0_85;
                physicsObject->Velocity.Y = projectile->Speed;

                f.Events.EnemyKicked(hitEntity, false);
                if (projectile->Speed < 1) {
                    Destroy(f, projectileEntity, projectileAsset.DestroyParticleEffect);
                }
            }
        }

        private static void OnProjectileCoinInteraction(Frame f, EntityRef projectileEntity, EntityRef coinEntity) {
            var projectile = f.Unsafe.GetPointer<Projectile>(projectileEntity);
            var projectileAsset = f.FindAsset(projectile->Asset);

            if (projectileAsset.CollectCoins) {
                CoinSystem.TryCollectCoin(f, coinEntity, projectile->Owner);
            }
        }

        private static void OnProjectileObjectiveCoinInteraction(Frame f, EntityRef projectileEntity, EntityRef objectiveCoinEntity) {
            var projectile = f.Unsafe.GetPointer<Projectile>(projectileEntity);
            var projectileAsset = f.FindAsset(projectile->Asset);

            if (projectileAsset.CollectCoins) {
                CoinSystem.TryCollectCoin(f, objectiveCoinEntity, projectile->Owner);
            }
        }
    }
}