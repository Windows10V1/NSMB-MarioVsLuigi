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
                    // but tile-breaking projectiles get an exception when spawned inside breakable tiles.
                    if (asset.BreakBreakableTiles) {
                        brokeTile = TryBreakTiles(f, filter.Entity, physicsObject, stage);
                        if (!brokeTile) {
                            Span<PhysicsObjectSystem.LocationTilePair> tiles = stackalloc PhysicsObjectSystem.LocationTilePair[64];
                            int count = PhysicsObjectSystem.GetTilesOverlappingHitbox(f, transform->Position, collider->Shape, tiles, stage);
                            for (int i = 0; i < count; i++) {
                                var tilePos = tiles[i].Position;
                                var tileInstance = tiles[i].Tile;
                                if (f.FindAsset(tileInstance.Tile) is IInteractableTile interactable && interactable.Interact(f, filter.Entity, InteractionDirection.Left, tilePos, tileInstance, out _)) {
                                    brokeTile = true;
                                }
                            }
                        }
                    }

                    if (brokeTile) {
                        // So keep them alive!
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
            } else if (asset.Effect == ProjectileEffectType.Superball) {
                projectile->UpdateSuperball(f, physicsObject);
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
                        if (projectile->Frame < 30) {
                            if (physicsObject->IsTouchingGround) {
                                // Slope bug
                                physicsObject->IsTouchingGround = false;
                            } else {
                                projectile->Combo = 2;
                            }
                        } else if (projectile->Combo == 2) {
                            // Avoid getting stuck 24/7
                            Destroy(f, filter.Entity, asset.DestroyParticleEffect);
                            return;
                        }
                    }

                    if (asset.DestroyOnHitTerrain) {
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
            bool isGoldball = f.Unsafe.TryGetPointer(entity, out Projectile* projectile)
                && f.FindAsset(projectile->Asset) is ProjectileAsset projectileAsset
                && projectileAsset.Effect == ProjectileEffectType.Gold;
            var contacts = f.ResolveList(physicsObject->Contacts);
            foreach (var contact in contacts) {
                if (f.Exists(contact.Entity)) {
                    continue;
                }
                var tileInstance = stage.GetTileRelative(f, contact.Tile);
                if (f.FindAsset(tileInstance.Tile) is not IInteractableTile tile) {
                    continue;
                }
                InteractionDirection direction = contact.Normal.Y > FP._0_50 ? InteractionDirection.Down
                    : contact.Normal.Y < -FP._0_50 ? InteractionDirection.Up
                    : contact.Normal.X > 0 ? InteractionDirection.Left : InteractionDirection.Right;
                if (tile.Interact(f, entity, direction, contact.Tile, tileInstance, out _)) {
                    broke = true;
                    if (isGoldball) {
                        SpawnGoldTileCoin(f, stage, contact.Tile);
                    }
                }
            }
            if (!broke && contacts.Count == 0 && projectile != null
                && f.Unsafe.TryGetPointer(entity, out Transform2D* transform)
                && f.Unsafe.TryGetPointer(entity, out PhysicsCollider2D* collider)) {
                // No contacts yet (spawned inside a wall): check if tiles inside
                // its hitbox are breakable, if so break and continue.
                Span<PhysicsObjectSystem.LocationTilePair> tiles = stackalloc PhysicsObjectSystem.LocationTilePair[16];
                int count = PhysicsObjectSystem.GetTilesOverlappingHitbox(f, transform->Position, collider->Shape, tiles, stage);
                InteractionDirection direction = projectile->FacingRight ? InteractionDirection.Left : InteractionDirection.Right;
                for (int i = 0; i < count; i++) {
                    IntVector2 tilePos = tiles[i].Position;
                    var tileInstance = stage.GetTileRelative(f, tilePos);
                    if (f.FindAsset(tileInstance.Tile) is not IInteractableTile tile) {
                        continue;
                    }
                    if (tile.Interact(f, entity, direction, tilePos, tileInstance, out _)) {
                        broke = true;
                        if (isGoldball) {
                            SpawnGoldTileCoin(f, stage, tilePos);
                        }
                    }
                }
            }
            return broke;
        }

        private static void SpawnGoldTileCoin(Frame f, VersusStageData stage, IntVector2 tilePosition) {
            var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
            if (gamemode == null || !gamemode.LooseCoinPrototype.IsValid) {
                return;
            }

            EntityRef coinEntity = f.Create(gamemode.LooseCoinPrototype);
            f.Unsafe.GetPointer<Transform2D>(coinEntity)->Position = QuantumUtils.RelativeTileToWorldRounded(stage, tilePosition);

            var coin = f.Unsafe.GetPointer<Coin>(coinEntity);
            coin->CoinType |= CoinType.BakedInStage;
            coin->Lifetime = 0;
            coin->UncollectableFrames = 0;

            if (f.Unsafe.TryGetPointer(coinEntity, out PhysicsObject* coinPhysics)) {
                coinPhysics->Velocity = FPVector2.Zero;
                coinPhysics->IsFrozen = true;
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
                // Fireball collided with Iceball, or Hammer collided with Boomerang. Destroy both.
                Destroy(f, projectileEntityA, projectileAssetA.DestroyParticleEffect);
                Destroy(f, projectileEntityB, projectileAssetB.DestroyParticleEffect);
            }
        }

        private void OnProjectileCoinInteraction(Frame f, EntityRef projectileEntity, EntityRef coinEntity) {
            var projectile = f.Unsafe.GetPointer<Projectile>(projectileEntity);
            var projectileAsset = f.FindAsset(projectile->Asset);

            if (projectileAsset.CollectCoins) {
                CoinSystem.TryCollectCoin(f, coinEntity, projectile->Owner);
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

            bool destroyOnHit = f.Has<MarioPlayer>(hitEntity)
                ? projectileAsset.DestroyOnHitPlayers
                : projectileAsset.DestroyOnHitEnemies;
            if (destroyOnHit) {
                Destroy(f, projectileEntity, projectileAsset.DestroyParticleEffect);
            } else {
                if (projectileAsset.Bounce) {
                    var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(projectileEntity);
                    projectile->Speed *= Constants._0_85;
                    physicsObject->Gravity *= Constants._0_85;
                    physicsObject->Velocity.Y = projectile->Speed;

                    f.Events.EnemyKicked(hitEntity, false);
                    if (projectile->Speed < 1) {
                        Destroy(f, projectileEntity, projectileAsset.DestroyParticleEffect);
                    }
                } else {
                    f.Events.EnemyPierced(hitEntity);
                }
            }
        }
    }
}