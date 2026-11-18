using Photon.Deterministic;
using System;

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

            // Normally this whole bit would check to instant-despawn if spawned inside a wall,
            if (!physicsObject->DisableCollision && !projectile->CheckedCollision) {
                if (PhysicsObjectSystem.BoxInGround(f, transform->Position, collider->Shape)) {
                    bool brokeTile = false;
                    // but tile-breaking projectiles get an exception when spawned inside breakables.
                    if (asset.BreakBreakableTiles) {
                        brokeTile = TryBreakTiles(f, filter.Entity, physicsObject, stage);
                        if (!brokeTile) {
                            Span<PhysicsObjectSystem.LocationTilePair> tiles = stackalloc PhysicsObjectSystem.LocationTilePair[64];
                            int count = PhysicsObjectSystem.GetTilesOverlappingHitbox(f, transform->Position, collider->Shape, tiles, stage);
                            for (int i = 0; i < count; i++) {
                                var pos = tiles[i].Position;
                                var instance = tiles[i].Tile;
                                if (f.FindAsset(instance.Tile) is IInteractableTile interactable && interactable.Interact(f, filter.Entity, InteractionDirection.Left, pos, instance, out _)) {
                                    brokeTile = true;
                                }
                            }
                        }
                    }

                    if (brokeTile) {
                        // Keep them alive
                        return;
                    } else {
                        Destroy(f, filter.Entity, asset.DestroyParticleEffect);
                        return;
                    }
                }
                projectile->CheckedCollision = true;
            }

            HandleTileCollision(f, ref filter, asset, stage);

            physicsObject->Velocity.X = projectile->Speed * (projectile->FacingRight ? 1 : -1);

            if (asset.LockTo45Degrees) {
                physicsObject->TerminalVelocity = -projectile->Speed;
            }

            if (asset.Effect == ProjectileEffectType.Boomerang) {
                projectile->UpdateBoomerang(f, filter.Entity, physicsObject, stage);
            }
        }

        public void HandleTileCollision(Frame f, ref Filter filter, ProjectileAsset asset, VersusStageData stage) {
            var projectile = filter.Projectile;
            var physicsObject = filter.PhysicsObject;

            if (!physicsObject->DisableCollision) {
                if (physicsObject->IsTouchingLeftWall
                    || physicsObject->IsTouchingRightWall
                    || physicsObject->IsTouchingCeiling
                    || (physicsObject->IsTouchingGround && (!asset.Bounce || (projectile->HasBounced && asset.DestroyOnSecondBounce)))
                    || PhysicsObjectSystem.BoxInGround(f, filter.Transform->Position, filter.PhysicsCollider->Shape)) {

                    // Destroy tiles
                    if (asset.BreakBreakableTiles && TryBreakTiles(f, filter.Entity, physicsObject, stage)) {
                        return;
                    }

                    if (asset.Effect == ProjectileEffectType.Boomerang) {
                        if (projectile->Combo < 2) {
                            if (physicsObject->IsTouchingGround) {
                                // Slope bug
                                physicsObject->IsTouchingGround = false;
                            } else {
                                // "Ricochet" effect if not in return state
                                projectile->Combo = 2;
                            }
                        } else if (projectile->Combo == 2) {
                            Destroy(f, filter.Entity, asset.DestroyParticleEffect);
                            return;
                        }
                    } else {
                        Destroy(f, filter.Entity, asset.DestroyParticleEffect);
                        return;
                    }
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

        private static bool TryBreakTiles(Frame f, EntityRef entity, PhysicsObject* physicsObject, VersusStageData stage) {
            bool broke = false;
            var contacts = f.ResolveList(physicsObject->Contacts);

            foreach (var contact in contacts) {
                if (f.Exists(contact.Entity)) {
                    continue;
                }
                var instance = stage.GetTileRelative(f, contact.Tile);
                if (f.FindAsset(instance.Tile) is not IInteractableTile tile) {
                    continue;
                }
                InteractionDirection direction = contact.Normal.Y > FP._0_50
                ? InteractionDirection.Down : contact.Normal.Y < -FP._0_50
                ? InteractionDirection.Up : contact.Normal.X > 0
                ? InteractionDirection.Left
                : InteractionDirection.Right;
                if (tile.Interact(f, entity, direction, contact.Tile, instance, out _)) {
                    broke = true;
                }
            }

            return broke;
        }

        private void OnProjectileProjectileInteraction(Frame f, EntityRef projectileEntityA, EntityRef projectileEntityB) {
            var projectileA = f.Unsafe.GetPointer<Projectile>(projectileEntityA);
            var projectileB = f.Unsafe.GetPointer<Projectile>(projectileEntityB);

            if (projectileA->Owner == projectileB->Owner) {
                return;
            }

            var assetA = f.FindAsset(projectileA->Asset);
            var assetB = f.FindAsset(projectileB->Asset);

            if ((assetA.Effect == ProjectileEffectType.Fire && assetB.Effect == ProjectileEffectType.Freeze)
                || (assetB.Effect == ProjectileEffectType.Fire && assetA.Effect == ProjectileEffectType.Freeze)) {
                // Fireball collided with Iceball. Destroy both.
                Destroy(f, projectileEntityA, assetA.DestroyParticleEffect);
                Destroy(f, projectileEntityB, assetB.DestroyParticleEffect);
                return;
            }

            bool aIsHammer = assetA.Effect == ProjectileEffectType.KillEnemiesAndSoftKnockbackPlayers && assetB.Effect == ProjectileEffectType.Boomerang;
            bool bIsHammer = assetB.Effect == ProjectileEffectType.KillEnemiesAndSoftKnockbackPlayers && assetA.Effect == ProjectileEffectType.Boomerang;

            if (aIsHammer || bIsHammer) {
                EntityRef hammerEntity = aIsHammer ? projectileEntityA : projectileEntityB;
                EntityRef boomerangEntity = aIsHammer ? projectileEntityB : projectileEntityA;

                // Hammer collided with Boomerang. Bounce hammer and turn around Boomerang.
                Bounce(f, hammerEntity);
                TurnAroundBoomerang(f, boomerangEntity, hammerEntity);
            }
        }

        public static void TurnAroundBoomerang(Frame f, EntityRef boomerangEntity, EntityRef otherEntity) {
            var projectile = f.Unsafe.GetPointer<Projectile>(boomerangEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(boomerangEntity);
            var asset = f.FindAsset(projectile->Asset);

            QuantumUtils.UnwrapWorldLocations(f,
                f.Unsafe.GetPointer<Transform2D>(otherEntity)->Position,
                f.Unsafe.GetPointer<Transform2D>(boomerangEntity)->Position,
                out FPVector2 otherPos, out FPVector2 boomerangPos);

            bool hitFromRight = otherPos.X < boomerangPos.X;

            projectile->Combo = 0;
            projectile->Frame = 0;
            physicsObject->Velocity.X = asset.Speed * (hitFromRight ? 1 : -1);
        }

        private void OnProjectileCoinInteraction(Frame f, EntityRef projectileEntity, EntityRef coinEntity) {
            var projectile = f.Unsafe.GetPointer<Projectile>(projectileEntity);
            var asset = f.FindAsset(projectile->Asset);

            if (asset.CollectCoins) {
                CoinSystem.TryCollectCoin(f, coinEntity, projectile->Owner);
            }
        }

        public static void Destroy(Frame f, EntityRef entity, ParticleEffect particle) {
            var transform = f.Unsafe.GetPointer<Transform2D>(entity);
            f.Events.ProjectileDestroyed(entity, particle, transform->Position);
            f.Destroy(entity);
        }

        public static void Bounce(Frame f, EntityRef projectileEntity) {
            var projectile = f.Unsafe.GetPointer<Projectile>(projectileEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(projectileEntity);

            projectile->Speed *= Constants._0_85;
            physicsObject->Gravity *= Constants._0_85;
            physicsObject->Velocity.Y = projectile->Speed;
        }

        public void OnProjectileHitEntity(Frame f, EntityRef projectileEntity, EntityRef hitEntity) {
            var projectile = f.Unsafe.GetPointer<Projectile>(projectileEntity);
            var asset = f.FindAsset(projectile->Asset);

            if (asset.DestroyOnHit) {
                Destroy(f, projectileEntity, asset.DestroyParticleEffect);
            } else {
                if (asset.Bounce) {
                    if (projectile->Speed < 1) {
                    Destroy(f, projectileEntity, asset.DestroyParticleEffect);
                    }

                    Bounce(f, projectileEntity);
                    f.Events.EnemyKicked(hitEntity, false);
                } else {
                    f.Events.BoomerangPierced(hitEntity);
                }
            }
        }
    }
}