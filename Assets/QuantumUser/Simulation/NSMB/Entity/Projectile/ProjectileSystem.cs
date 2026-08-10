using Photon.Deterministic;
using Quantum.Collections;

namespace Quantum {
    public unsafe class ProjectileSystem : SystemMainThreadEntityFilter<Projectile, ProjectileSystem.Filter>, ISignalOnProjectileHitEntity {
        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Projectile* Projectile;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* PhysicsCollider;
        }

        private const byte BoomerangMaxTravelFrames = 15;
        private const byte BoomerangSlowdownFrames = 15;
        private const byte BoomerangReturnFramesTotal = 10;

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<Projectile, Projectile>(f, OnProjectileProjectileInteraction);
            f.Context.Interactions.Register<Projectile, Coin>(f, OnProjectileCoinInteraction);
            f.Context.RegisterPreContactCallback(f, OnBoomerangPreContact);
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

            if (projectile->Lifetime > 0 && QuantumUtils.Decrement(ref projectile->Lifetime)) {
                // Despawn via timer
                Destroy(f, filter.Entity, asset.DestroyParticleEffect);
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

            if (asset.Effect == ProjectileEffectType.Boomerang) {
                HandleBoomerangTileCollision(f, ref filter, stage, asset);
                UpdateBoomerangVelocity(f, ref filter, asset);
            } else {
                HandleTileCollision(f, ref filter, asset);

                physicsObject->Velocity.X = projectile->Speed * (projectile->FacingRight ? 1 : -1);

                if (asset.LockTo45Degrees) {
                    physicsObject->TerminalVelocity = -projectile->Speed;
                }
            }
        }

        public void HandleTileCollision(Frame f, ref Filter filter, ProjectileAsset asset) {
            var projectile = filter.Projectile;
            var physicsObject = filter.PhysicsObject;

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

        public void HandleBoomerangTileCollision(Frame f, ref Filter filter, VersusStageData stage, ProjectileAsset asset) {
            var projectile = filter.Projectile;
            var physicsObject = filter.PhysicsObject;

            if (!physicsObject->DisableCollision && f.TryResolveList(physicsObject->Contacts, out QList<PhysicsContact> contacts)) {
                for (int i = 0; i < contacts.Count; i++) {
                    var contact = contacts[i];
                    if (contact.Frame != f.Number || contact.Tile.X < 0 || contact.Tile.Y < 0) {
                        continue;
                    }

                    StageTileInstance tileInstance = stage.GetTileRelative(f, contact.Tile);
                    if (tileInstance.Tile == default || !f.TryFindAsset(tileInstance.Tile, out StageTile tile)) {
                        continue;
                    }

                    // Activate coin/powerup blocks for the boomerang's owner
                    if ((tile is CoinTile or PowerupTileBase) && f.Unsafe.TryGetPointer(projectile->Owner, out MarioPlayer* owner)) {
                        InteractionDirection direction = FPVector2.Dot(contact.Normal, FPVector2.Right) > 0 ? InteractionDirection.Right : InteractionDirection.Left;
                        ((IInteractableTile) tile).Interact(f, projectile->Owner, direction, contact.Tile, tileInstance, out _);
                    }
                }
            }

            if (physicsObject->DisableCollision) {
                return;
            }

            // Any wall hit: while heading away this is a ricochet that turns the
            // boomerang around at max speed; otherwise it despawns.
            bool hitWall = physicsObject->IsTouchingLeftWall
                || physicsObject->IsTouchingRightWall
                || physicsObject->IsTouchingCeiling
                || physicsObject->IsTouchingGround
                || PhysicsObjectSystem.BoxInGround(f, filter.Transform->Position, filter.PhysicsCollider->Shape);

            if (!hitWall) {
                return;
            }

            if (!projectile->BoomerangReturning) {
                projectile->BoomerangReturning = true;
                projectile->BoomerangReturnFrames = BoomerangReturnFramesTotal;
            } else {
                Destroy(f, filter.Entity, asset.DestroyParticleEffect);
            }
        }

        public void UpdateBoomerangVelocity(Frame f, ref Filter filter, ProjectileAsset asset) {
            var projectile = filter.Projectile;
            var physicsObject = filter.PhysicsObject;

            FP speed = asset.Speed;
            if (!projectile->BoomerangReturning) {
                // Travel outwards for 15 frames, then gradually slow down to 0 over the next 15.
                if (projectile->BoomerangTravelFrames >= BoomerangMaxTravelFrames + BoomerangSlowdownFrames) {
                    // Fully stopped: instantly turn around and head back to the owner.
                    projectile->BoomerangReturning = true;
                    projectile->BoomerangReturnFrames = 0;
                    speed = 0;
                } else if (projectile->BoomerangTravelFrames >= BoomerangMaxTravelFrames) {
                    speed = asset.Speed * (BoomerangMaxTravelFrames + BoomerangSlowdownFrames - projectile->BoomerangTravelFrames) / BoomerangSlowdownFrames;
                }
                projectile->BoomerangTravelFrames++;
            } else if (projectile->BoomerangReturnFrames < BoomerangReturnFramesTotal) {
                // Gradually accelerate back up to max speed over 10 frames.
                speed = asset.Speed * (projectile->BoomerangReturnFrames + 1) / BoomerangReturnFramesTotal;
                projectile->BoomerangReturnFrames++;
            }

            if (projectile->BoomerangReturning
                && f.Unsafe.TryGetPointer(projectile->Owner, out Transform2D* ownerTransform)
                && f.Unsafe.TryGetPointer(projectile->Owner, out PhysicsCollider2D* ownerCollider)) {
                // Follow the shooter's hitbox center, wherever they've gone.
                FPVector2 toOwner = (ownerTransform->Position + ownerCollider->Shape.Centroid) - filter.Transform->Position;
                if (toOwner.SqrMagnitude > FP._0_01) {
                    physicsObject->Velocity = toOwner.Normalized * speed;
                    return;
                }
            }

            int direction = (projectile->FacingRight ? 1 : -1) * (projectile->BoomerangReturning ? -1 : 1);
            physicsObject->Velocity.X = speed * direction;
        }

        public void OnBoomerangPreContact(Frame f, VersusStageData stage, EntityRef entity, PhysicsContact contact, ref bool keepContact) {
            if (!f.Unsafe.TryGetPointer(entity, out Projectile* projectile)) {
                return;
            }

            var asset = f.FindAsset(projectile->Asset);
            if (asset == null || asset.Effect != ProjectileEffectType.Boomerang) {
                return;
            }

            if (contact.Tile.X < 0 || contact.Tile.Y < 0) {
                return;
            }

            StageTileInstance tileInstance = stage.GetTileRelative(f, contact.Tile);
            if (tileInstance.Tile == default || !f.TryFindAsset(tileInstance.Tile, out StageTile tile)) {
                return;
            }

            if (tile is not BreakableBrickTile breakable || tile is CoinTile or PowerupTileBase) {
                // Activating blocks are handled in HandleBoomerangTileCollision.
                return;
            }

            InteractionDirection direction = FPVector2.Dot(contact.Normal, FPVector2.Right) > 0 ? InteractionDirection.Right : InteractionDirection.Left;
            if (breakable.Interact(f, entity, direction, contact.Tile, tileInstance, out _)) {
                // Broken by the boomerang as if it wasn't even there.
                keepContact = false;
            }
        }

        private void OnProjectileCoinInteraction(Frame f, EntityRef projectileEntity, EntityRef coinEntity) {
            var projectile = f.Unsafe.GetPointer<Projectile>(projectileEntity);
            var asset = f.FindAsset(projectile->Asset);
            if (asset == null || !asset.CollectCoins) {
                return;
            }

            CoinSystem.TryCollectCoin(f, coinEntity, projectile->Owner);
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
                // Fireball collided with Iceball, or Hammer collided with Boomerang. Destroy both.
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

            if (projectileAsset.Effect == ProjectileEffectType.Boomerang) {
                f.Events.EnemyPierced(hitEntity);
                return;
            }

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
    }
}