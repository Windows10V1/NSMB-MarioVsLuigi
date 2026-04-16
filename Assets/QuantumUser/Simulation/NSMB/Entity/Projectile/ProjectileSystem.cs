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

            // Fuck the normal update logic
            if (asset.IsSuperBall) {
                HandleSuperBallUpdate(f, ref filter, asset);
                return;
            }

            // Insane
            if (asset.IsBoomerang) {
                HandleBoomerangUpdate(f, ref filter, asset);
            } else {
                // Sane
                if (projectile->Lifetime > 0 && QuantumUtils.Decrement(ref projectile->Lifetime)) {
                    // Tick-tock tick-Tock. Boom.
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

            bool boomerangPullForceActive = false;
            if (asset.IsBoomerang) {
                if (projectile->IsReturning()) {
                    boomerangPullForceActive = true;
                } else {
                    FP elapsedTime = (FP)projectile->Lifetime / 60;
                    boomerangPullForceActive = elapsedTime >= asset.BoomerangReturnDelay;
                }
            }

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

            projectile->Lifetime++;

            if (!projectile->IsReturning()) {
                FP elapsedTime = (FP)projectile->Lifetime / 60; // 60 FPS

                if (elapsedTime >= asset.BoomerangReturnDelay) {
                    FP timeIntoReturn = elapsedTime - asset.BoomerangReturnDelay;
                    ApplyBoomerangPullForce(f, ref filter, asset, timeIntoReturn);
                }
            } else {
                ApplyBoomerangPullForce(f, ref filter, asset, FP.MaxValue);
            }
        }

        private void ApplyBoomerangPullForce(Frame f, ref Filter filter, ProjectileAsset asset, FP timeIntoReturn) {
            var projectile = filter.Projectile;
            var physicsObject = filter.PhysicsObject;
            var transform = filter.Transform;

            if (!f.Unsafe.TryGetPointer(projectile->Owner, out Transform2D* ownerTransform)) {
                return;
            }

            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            FPVector2 ownerPosition = ownerTransform->Position + (FPVector2.Up * Constants._0_40);
            QuantumUtils.UnwrapWorldLocations(stage, transform->Position, ownerPosition, out FPVector2 unwrappedProjectilePos, out FPVector2 unwrappedOwnerPos);
            FPVector2 directionToOwner = unwrappedOwnerPos - unwrappedProjectilePos;
            FP distanceToOwner = directionToOwner.Magnitude;

            if (distanceToOwner < FP.FromString("0.5")) {
                // Very close to the shooter. Despawn without particle.
                Destroy(f, filter.Entity, ParticleEffect.None);
                return;
            }

            directionToOwner = directionToOwner.Normalized;

            FP pullForceStrength;
            if (timeIntoReturn >= FP.MaxValue || projectile->IsReturning()) {
                pullForceStrength = asset.Speed;
            } else {
                pullForceStrength = timeIntoReturn * asset.BoomerangReturnAcceleration * asset.Speed;
                if (pullForceStrength > asset.Speed) {
                    pullForceStrength = asset.Speed;
                }
            }

            FPVector2 targetVelocity = directionToOwner * pullForceStrength;
            
            FPVector2 smoothVelocity = targetVelocity;
            if (timeIntoReturn < FP.MaxValue && !projectile->IsReturning()) {
                FP transitionTime = FP.FromString("1.0");
                FP transitionProgress = FPMath.Min(timeIntoReturn / transitionTime, FP._1);
                
                FPVector2 currentVelocity = physicsObject->Velocity;
                smoothVelocity = new FPVector2(
                    currentVelocity.X * (FP._1 - transitionProgress) + targetVelocity.X * transitionProgress,
                    currentVelocity.Y * (FP._1 - transitionProgress) + targetVelocity.Y * transitionProgress
                );
            }
            
            physicsObject->Velocity = smoothVelocity;

            physicsObject->Gravity = FPVector2.Zero;
        }

        private void HandleSuperBallUpdate(Frame f, ref Filter filter, ProjectileAsset asset) {
            var projectile = filter.Projectile;
            var physicsObject = filter.PhysicsObject;
            var transform = filter.Transform;
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            var collider = filter.PhysicsCollider;
            var entity = filter.Entity;

            // Handle Super Ball lifetime (i hate bytes)
            if (projectile->Lifetime > 0 && QuantumUtils.Decrement(ref projectile->Lifetime)) {
                Destroy(f, entity, asset.DestroyParticleEffect);
                return;
            }

            // Check to instant-despawn if spawned inside a wall
            if (!projectile->CheckedCollision) {
                if (PhysicsObjectSystem.BoxInGround(f, transform->Position, collider->Shape, stage: stage, entity: entity)) {
                    Destroy(f, entity, asset.DestroyParticleEffect);
                    return;
                }
                projectile->CheckedCollision = true;
            }

            // Direction Extraction, that sounds cool.
            bool goingRight = (projectile->Combo & 1) != 0;
            bool goingUp = (projectile->Combo & 2) != 0;

            // Calculate Calculate Calculate
            FP velocityX = goingRight ? projectile->Speed : -projectile->Speed;
            FP velocityY = goingUp ? -projectile->Speed : projectile->Speed;  // Negative = UP

            FPVector2 newPosition = transform->Position + new FPVector2(velocityX, velocityY) / 60;  // 60 FPS

            // Check for terrain collisions (semisolids are broken, somehow)
            bool hitGround = false;
            bool hitCeiling = false;
            bool hitLeftWall = false;
            bool hitRightWall = false;

            if (PhysicsObjectSystem.BoxInGround(f, newPosition, collider->Shape, stage: stage, entity: entity)) {
                FPVector2 posXOnly = new(newPosition.X, transform->Position.Y);
                if (!PhysicsObjectSystem.BoxInGround(f, posXOnly, collider->Shape, stage: stage, entity: entity)) {
                    if (velocityY > 0) {
                        hitGround = true;
                    } else {
                        hitCeiling = true;
                    }
                    newPosition.X = posXOnly.X;
                } else {
                    FPVector2 posYOnly = new(transform->Position.X, newPosition.Y);
                    if (!PhysicsObjectSystem.BoxInGround(f, posYOnly, collider->Shape, stage: stage, entity: entity)) {
                        if (velocityX < 0) {
                            hitLeftWall = true;
                        } else {
                            hitRightWall = true;
                        }
                        newPosition.Y = posYOnly.Y;
                    } else {
                        if (velocityX < 0) {
                            hitLeftWall = true;
                        } else {
                            hitRightWall = true;
                        }
                        if (velocityY > 0) {
                            hitGround = true;
                        } else {
                            hitCeiling = true;
                        }
                        newPosition = transform->Position;
                    }
                }
            }

            transform->Position = newPosition;

            if (hitGround) {
                projectile->Combo |= 2;
            }
            if (hitCeiling) {
                projectile->Combo &= 0xFD;
            }
            if (hitLeftWall) {
                projectile->Combo |= 1;
            }
            if (hitRightWall) {
                projectile->Combo &= 0xFE;
            }

            bool anyCollision = hitGround || hitCeiling || hitLeftWall || hitRightWall;
            if (anyCollision) {
                StageTileInstance tileInstance = stage.GetTileRelative(f, QuantumUtils.WorldToRelativeTile(f, transform->Position));
                StageTile tile = f.FindAsset(tileInstance.Tile);
                
                if (tile is IInteractableTile it) {
                    InteractionDirection direction = InteractionDirection.Up;
                    if (hitGround) {
                        direction = InteractionDirection.Down;
                    } else if (hitCeiling) {
                        direction = InteractionDirection.Up;
                    } else if (hitLeftWall) {
                        direction = InteractionDirection.Left;
                    } else if (hitRightWall) {
                        direction = InteractionDirection.Right;
                    }

                    it.Interact(f, entity, direction, QuantumUtils.WorldToRelativeTile(f, transform->Position), tileInstance, out _);
                }
            }
        }

        public void HandleTileCollision(Frame f, ref Filter filter, ProjectileAsset asset) {
            var projectile = filter.Projectile;
            var physicsObject = filter.PhysicsObject;
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);

            if (asset.IsSuperBall) {
                return;
            }

            bool hasTerrainCollision = false;
            if (!physicsObject->DisableCollision) {
                hasTerrainCollision = physicsObject->IsTouchingLeftWall
                    || physicsObject->IsTouchingRightWall
                    || physicsObject->IsTouchingCeiling
                    || physicsObject->IsTouchingGround
                    || PhysicsObjectSystem.BoxInGround(f, filter.Transform->Position, filter.PhysicsCollider->Shape);
            }

            bool hitBreakableBrick = false;
            FPVector2 newVelocity = FPVector2.Zero;
            if (hasTerrainCollision && !physicsObject->DisableCollision) {
                if (asset.IsBoomerang) {
                    FPVector2 originalVelocity = physicsObject->Velocity;
                    newVelocity = originalVelocity;
                }
                
                foreach (var contact in f.ResolveList(physicsObject->Contacts)) {
                    if (f.Exists(contact.Entity)) {
                        continue;
                    }

                    StageTileInstance tileInstance = stage.GetTileRelative(f, contact.Tile);
                    StageTile tile = f.FindAsset(tileInstance.Tile);
                    
                    if (tile is IInteractableTile it) {
                        if (asset.IsBoomerang && tile is BreakableBrickTile breakableBrick) {
                            if (breakableBrick.BreakingRules.HasFlag(BreakableBrickTile.BreakableBy.Boomerangs)) {
                                hitBreakableBrick = true;
                            }
                        }
                        
                        InteractionDirection direction = InteractionDirection.Up;
                        if (FPVector2.Dot(contact.Normal, FPVector2.Up) > FP.FromString("0.5")) {
                            direction = InteractionDirection.Down; // Hit from above
                        } else if (FPVector2.Dot(contact.Normal, FPVector2.Down) > FP.FromString("0.5")) {
                            direction = InteractionDirection.Up; // Hit from below
                        } else if (FPVector2.Dot(contact.Normal, FPVector2.Right) > FP.FromString("0.5")) {
                            direction = InteractionDirection.Left; // Hit from right
                        } else if (FPVector2.Dot(contact.Normal, FPVector2.Left) > FP.FromString("0.5")) {
                            direction = InteractionDirection.Right; // Hit from left
                        }

                        it.Interact(f, filter.Entity, direction, contact.Tile, tileInstance, out _);
                    }
                }
            }

            if (asset.IsBoomerang && hasTerrainCollision && !projectile->IsReturning() && !hitBreakableBrick) {
                projectile->SetReturning();
                projectile->Lifetime = 0;
                ApplyBoomerangPullForce(f, ref filter, asset, FP.MaxValue);
                return;
            }

            if (!physicsObject->DisableCollision) {
                bool shouldDespawn = physicsObject->IsTouchingLeftWall
                    || physicsObject->IsTouchingRightWall
                    || physicsObject->IsTouchingCeiling
                    || (physicsObject->IsTouchingGround && (!asset.Bounce || (projectile->HasBounced && asset.DestroyOnSecondBounce)))
                    || PhysicsObjectSystem.BoxInGround(f, filter.Transform->Position, filter.PhysicsCollider->Shape);

                if (shouldDespawn && asset.IsBoomerang && hitBreakableBrick) {
                    shouldDespawn = false;
                }

                if (shouldDespawn) {
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
                || (projectileAssetB.Effect == ProjectileEffectType.Fire && projectileAssetA.Effect == ProjectileEffectType.Freeze)
                || (projectileAssetA.Effect == ProjectileEffectType.Hammer && projectileAssetB.Effect == ProjectileEffectType.Boomerang)
                || (projectileAssetB.Effect == ProjectileEffectType.Hammer && projectileAssetA.Effect == ProjectileEffectType.Boomerang)) {
                // Fireball collided with Iceball/Hammer collided with Boomerang. Destroy both.
                Destroy(f, projectileEntityA, projectileAssetA.DestroyParticleEffect);
                Destroy(f, projectileEntityB, projectileAssetB.DestroyParticleEffect);
            }
        }

        public static void Destroy(Frame f, EntityRef entity, ParticleEffect particle) {
            var projectile = f.Unsafe.GetPointer<Projectile>(entity);
            var transform = f.Unsafe.GetPointer<Transform2D>(entity);
            var asset = f.FindAsset<ProjectileAsset>(projectile->Asset);
            
            // Check if this projectile has a cooldown and set it on owner
            if (asset.CooldownFrames > 0) {
                if (f.Unsafe.TryGetPointer(projectile->Owner, out MarioPlayer* owner)) {
                    owner->ProjectileCooldownFrames = (ushort)asset.CooldownFrames;
                }
            }
            
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