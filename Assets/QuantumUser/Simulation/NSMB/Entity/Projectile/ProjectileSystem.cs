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

            // Super Ball has its own physics system - skip normal update logic
            if (asset.IsSuperBall) {
                HandleSuperBallUpdate(f, ref filter, asset);
                return;
            }

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

            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            FPVector2 ownerPosition = ownerTransform->Position + (FPVector2.Up * Constants._0_40);
            QuantumUtils.UnwrapWorldLocations(stage, transform->Position, ownerPosition, out FPVector2 unwrappedProjectilePos, out FPVector2 unwrappedOwnerPos);
            FPVector2 directionToOwner = unwrappedOwnerPos - unwrappedProjectilePos;
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
            // Only apply smooth transition when returning due to timer, not when hitting terrain
            FPVector2 smoothVelocity = targetVelocity;
            if (timeIntoReturn < FP.MaxValue && !projectile->IsReturning()) {
                // Natural countdown phase - use smooth transition
                FP transitionTime = FP.FromString("1.0"); // 1 second to fully transition
                FP transitionProgress = FPMath.Min(timeIntoReturn / transitionTime, FP._1);
                
                FPVector2 currentVelocity = physicsObject->Velocity;
                // Lerp between current and target: current * (1 - t) + target * t
                smoothVelocity = new FPVector2(
                    currentVelocity.X * (FP._1 - transitionProgress) + targetVelocity.X * transitionProgress,
                    currentVelocity.Y * (FP._1 - transitionProgress) + targetVelocity.Y * transitionProgress
                );
            }
            
            physicsObject->Velocity = smoothVelocity;

            // Disable gravity while returning
            physicsObject->Gravity = FPVector2.Zero;
        }

        private void HandleSuperBallUpdate(Frame f, ref Filter filter, ProjectileAsset asset) {
            var projectile = filter.Projectile;
            var physicsObject = filter.PhysicsObject;
            var transform = filter.Transform;
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            var collider = filter.PhysicsCollider;
            var entity = filter.Entity;

            // Handle Super Ball lifetime (if any)
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

            // Extract direction from Combo byte: bit 0 = horizontal (0=left, 1=right), bit 1 = vertical (0=down, 1=up)
            bool goingRight = (projectile->Combo & 1) != 0;
            bool goingUp = (projectile->Combo & 2) != 0;

            // Calculate velocity components (constant magnitude at 45 degrees)
            FP velocityX = goingRight ? projectile->Speed : -projectile->Speed;
            FP velocityY = goingUp ? -projectile->Speed : projectile->Speed;  // Negative = up

            // Move the projectile manually
            FPVector2 newPosition = transform->Position + new FPVector2(velocityX, velocityY) / 60;  // 60 FPS

            // Check for terrain collisions (including semisolids for ground detection)
            bool hitGround = false;
            bool hitCeiling = false;
            bool hitLeftWall = false;
            bool hitRightWall = false;

            // Check if new position would be in terrain
            if (PhysicsObjectSystem.BoxInGround(f, newPosition, collider->Shape, stage: stage, entity: entity)) {
                // We hit something - figure out what direction
                // Try moving only X
                FPVector2 posXOnly = new(newPosition.X, transform->Position.Y);
                if (!PhysicsObjectSystem.BoxInGround(f, posXOnly, collider->Shape, stage: stage, entity: entity)) {
                    // Can move in X but not Y - hit ceiling or ground
                    if (velocityY > 0) {
                        hitGround = true;
                    } else {
                        hitCeiling = true;
                    }
                    newPosition.X = posXOnly.X;
                } else {
                    // Try moving only Y
                    FPVector2 posYOnly = new(transform->Position.X, newPosition.Y);
                    if (!PhysicsObjectSystem.BoxInGround(f, posYOnly, collider->Shape, stage: stage, entity: entity)) {
                        // Can move in Y but not X - hit wall
                        if (velocityX < 0) {
                            hitLeftWall = true;
                        } else {
                            hitRightWall = true;
                        }
                        newPosition.Y = posYOnly.Y;
                    } else {
                        // Both blocked, stuck in corner - bounce off both
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
                        // Stay at current position
                        newPosition = transform->Position;
                    }
                }
            }

            // Update position
            transform->Position = newPosition;

            // Handle bounces - reverse appropriate velocity components
            if (hitGround) {
                // Flip vertical direction - set bit 1 (go UP after hitting ground)
                projectile->Combo |= 2;
            }
            if (hitCeiling) {
                // Flip vertical direction - clear bit 1 (go DOWN after hitting ceiling)
                projectile->Combo &= 0xFD;  // 11111101
            }
            if (hitLeftWall) {
                // Flip horizontal direction - set bit 0 (go right)
                projectile->Combo |= 1;
            }
            if (hitRightWall) {
                // Flip horizontal direction - clear bit 0 (go left)
                projectile->Combo &= 0xFE;  // 11111110
            }

            // Try to interact with tiles (for breaking blocks, etc.)
            bool anyCollision = hitGround || hitCeiling || hitLeftWall || hitRightWall;
            if (anyCollision) {
                // SuperBall uses manual collision detection - physics contacts are empty
                // Query tile directly at the collision position
                StageTileInstance tileInstance = stage.GetTileRelative(f, QuantumUtils.WorldToRelativeTile(f, transform->Position));
                StageTile tile = f.FindAsset(tileInstance.Tile);
                
                if (tile is IInteractableTile it) {
                    // Determine interaction direction based on what we hit
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

                    // Call interact on the tile
                    it.Interact(f, entity, direction, QuantumUtils.WorldToRelativeTile(f, transform->Position), tileInstance, out _);
                }
            }
        }

        public void HandleTileCollision(Frame f, ref Filter filter, ProjectileAsset asset) {
            var projectile = filter.Projectile;
            var physicsObject = filter.PhysicsObject;
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);

            // Super Ball uses its own collision handling
            if (asset.IsSuperBall) {
                return;
            }

            // Check for terrain collision
            bool hasTerrainCollision = false;
            if (!physicsObject->DisableCollision) {
                hasTerrainCollision = physicsObject->IsTouchingLeftWall
                    || physicsObject->IsTouchingRightWall
                    || physicsObject->IsTouchingCeiling
                    || physicsObject->IsTouchingGround
                    || PhysicsObjectSystem.BoxInGround(f, filter.Transform->Position, filter.PhysicsCollider->Shape);
            }

            // Try to interact with tiles (for breaking blocks, etc.)
            // Also check for boomerang breakable brick piercing
            bool hitBreakableBrick = false;
            FPVector2 newVelocity = FPVector2.Zero;
            if (hasTerrainCollision && !physicsObject->DisableCollision) {
                // Store original velocity for boomerangs to preserve momentum
                if (asset.IsBoomerang) {
                    FPVector2 originalVelocity = physicsObject->Velocity;
                    newVelocity = originalVelocity;
                }
                
                foreach (var contact in f.ResolveList(physicsObject->Contacts)) {
                    // Only process tile contacts (no entity)
                    if (f.Exists(contact.Entity)) {
                        continue;
                    }

                    StageTileInstance tileInstance = stage.GetTileRelative(f, contact.Tile);
                    StageTile tile = f.FindAsset(tileInstance.Tile);
                    
                    if (tile is IInteractableTile it) {
                        // Check if this is a breakable brick for boomerang piercing
                        if (asset.IsBoomerang && tile is BreakableBrickTile breakableBrick) {
                            if (breakableBrick.BreakingRules.HasFlag(BreakableBrickTile.BreakableBy.Boomerangs)) {
                                hitBreakableBrick = true;
                            }
                        }
                        
                        // Determine interaction direction based on contact normal
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

                        // Call interact on the tile
                        it.Interact(f, filter.Entity, direction, contact.Tile, tileInstance, out _);
                    }
                }
            }

            // Special handling for boomerangs: switch to returning mode on terrain hit
            // But allow piercing through breakable bricks
            if (asset.IsBoomerang && hasTerrainCollision && !projectile->IsReturning() && !hitBreakableBrick) {
                projectile->SetReturning();
                projectile->Lifetime = 0; // Reset frame counter for return phase with peak force
                // Apply max pull force immediately when hitting terrain
                ApplyBoomerangPullForce(f, ref filter, asset, FP.MaxValue);
                return; // Don't despawn, just switch to return mode
            }

            // Despawn
            if (!physicsObject->DisableCollision) {
                bool shouldDespawn = physicsObject->IsTouchingLeftWall
                    || physicsObject->IsTouchingRightWall
                    || physicsObject->IsTouchingCeiling
                    || (physicsObject->IsTouchingGround && (!asset.Bounce || (projectile->HasBounced && asset.DestroyOnSecondBounce)))
                    || PhysicsObjectSystem.BoxInGround(f, filter.Transform->Position, filter.PhysicsCollider->Shape);

                // Don't despawn boomerangs that just hit breakable bricks
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
                || (projectileAssetB.Effect == ProjectileEffectType.Fire && projectileAssetA.Effect == ProjectileEffectType.Freeze)) {
                // Fireball collided with Iceball. Destroy both.
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