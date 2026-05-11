using Photon.Deterministic;

namespace Quantum {
    public unsafe class CloudBlockSystem : SystemMainThreadEntityFilter<CloudBlock, CloudBlockSystem.Filter>, ISignalOnEntityBumped, ISignalOnEntityCrushed, ISignalOnComponentRemoved<CloudBlock> {

        private static readonly FP CLOUD_SPAWN_VERTICAL_OFFSET = FP._0_10;
        private static readonly FP SEMISOLID_MARGIN = FP._0_05;
        private const byte SQUISH_COOLDOWN = 10;

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public CloudBlock* CloudBlock;
            public PhysicsCollider2D* Collider;
        }

        public override void OnInit(Frame f) {
            f.Context.RegisterPreContactCallback(f, OnPreContactCallback);
            f.Context.Interactions.Register<MarioPlayer, CloudBlock>(f, OnMarioCloudBlockInteraction);
            f.Context.Interactions.Register<Enemy, CloudBlock>(f, OnEnemyCloudBlockInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var cloudBlock = filter.CloudBlock;
            var asset = f.FindAsset(cloudBlock->Asset);

            if (cloudBlock->SquishCooldownFrames > 0) {
                cloudBlock->SquishCooldownFrames--;
            }

            if (cloudBlock->IsSummoning) {
                if (cloudBlock->Lifetime > asset.LifetimeFrames - asset.SummonAnimationFrames) {
                    // Still in summon animation phase; keep IsSummoning true
                } else {
                    cloudBlock->IsSummoning = false;
                }
            }

            if (cloudBlock->IsDestroying) {
                if (QuantumUtils.Decrement(ref cloudBlock->Lifetime)) {
                    f.Destroy(filter.Entity);
                }
                return;
            }

            if (QuantumUtils.Decrement(ref cloudBlock->Lifetime)) {
                cloudBlock->StartDestroying(f, filter.Entity);
            }
        }

        public void OnRemoved(Frame f, EntityRef entity, CloudBlock* component) {
            DecrementOwnerCloudCount(f, component->Owner);
        }

        private void OnPreContactCallback(Frame f, VersusStageData stage, EntityRef entity, PhysicsContact contact, ref bool keepContacts) {
            if (!f.Has<CloudBlock>(contact.Entity)) {
                return;
            }

            if (!f.Unsafe.TryGetPointer(entity, out Transform2D* entityTransform)
                || !f.Unsafe.TryGetPointer(contact.Entity, out Transform2D* cloudTransform)
                || !f.Unsafe.TryGetPointer(entity, out PhysicsCollider2D* entityCollider)
                || !f.Unsafe.TryGetPointer(contact.Entity, out PhysicsCollider2D* cloudCollider)) {
                return;
            }

            // Semisolids use Edge shape for the platform itself
            if (cloudCollider->Shape.Type != Shape2DType.Edge) {
                return;
            }

            // Entity bottom edge (entity colliders are typically Box shapes)
            FP entityBottom = entityCollider->Shape.Type == Shape2DType.Box
                ? entityTransform->Position.Y + entityCollider->Shape.Centroid.Y - entityCollider->Shape.Box.Extents.Y
                : entityTransform->Position.Y;

            // Edge shape centroid is the midpoint of the edge; for a horizontal edge that's the surface Y
            FP cloudSurfaceY = cloudTransform->Position.Y + cloudCollider->Shape.Centroid.Y;

            if (entityBottom < cloudSurfaceY - SEMISOLID_MARGIN) {
                // Entity is below the cloud; allow passing through
                keepContacts = false;
            } else {
                // Entity is landing on top of the cloud; trigger squish
                var cloudBlock = f.Unsafe.GetPointer<CloudBlock>(contact.Entity);
                if (!cloudBlock->IsDestroying && cloudBlock->SquishCooldownFrames == 0) {
                    cloudBlock->SquishCooldownFrames = SQUISH_COOLDOWN;
                    f.Events.CloudBlockSquished(contact.Entity);
                }
            }
        }

        public static bool OnMarioCloudBlockInteraction(Frame f, EntityRef marioEntity, EntityRef cloudBlockEntity, PhysicsContact contact) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var cloudBlock = f.Unsafe.GetPointer<CloudBlock>(cloudBlockEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);
            var asset = f.FindAsset(cloudBlock->Asset);

            FP upDot = FPVector2.Dot(contact.Normal, FPVector2.Up);
            if (upDot >= Constants.PhysicsGroundMaxAngleCos) {
                // Landing on top of the cloud
                if (mario->IsGroundpoundActive) {
                    if (mario->CurrentPowerupState == PowerupState.MegaMushroom) {
                        // Mega Mushroom groundpound destroys cloud immediately with destroy particle
                        var transform = f.Unsafe.GetPointer<Transform2D>(cloudBlockEntity);
                        f.Events.CloudBlockDestroyed(cloudBlockEntity, transform->Position);
                        f.Destroy(cloudBlockEntity);
                        return true; // Continue groundpound
                    } else {
                        // Trigger hard-squish animation and bounce player upwards
                        f.Events.CloudBlockHardSquished(cloudBlockEntity);
                        physicsObject->Velocity.Y = asset.BounceVelocity;
                        physicsObject->IsTouchingGround = false;
                        physicsObject->WasTouchingGround = false;
                        mario->IsGroundpounding = false;
                        mario->IsGroundpoundActive = false;
                        mario->DoEntityBounce = true;
                        mario->JumpState = JumpState.None;
                        mario->PropellerDrillCooldown = 30;
                        return false; // Stop groundpound
                    }
                } else {
                    // Regular landing (not groundpound)
                    if (mario->CurrentPowerupState == PowerupState.MegaMushroom) {
                        // Trigger hard-squish animation but no bouncing
                        f.Events.CloudBlockHardSquished(cloudBlockEntity);
                    } else if (!cloudBlock->IsDestroying && cloudBlock->SquishCooldownFrames == 0) {
                        cloudBlock->SquishCooldownFrames = SQUISH_COOLDOWN;
                        f.Events.CloudBlockSquished(cloudBlockEntity);
                    }
                }
            }

            return true;
        }

        public static bool OnEnemyCloudBlockInteraction(Frame f, EntityRef enemyEntity, EntityRef cloudBlockEntity, PhysicsContact contact) {
            var cloudBlock = f.Unsafe.GetPointer<CloudBlock>(cloudBlockEntity);

            FP upDot = FPVector2.Dot(contact.Normal, FPVector2.Up);
            if (upDot >= Constants.PhysicsGroundMaxAngleCos) {
                // Enemy landing on top of the cloud; trigger squish
                if (!cloudBlock->IsDestroying && cloudBlock->SquishCooldownFrames == 0) {
                    cloudBlock->SquishCooldownFrames = SQUISH_COOLDOWN;
                    f.Events.CloudBlockSquished(cloudBlockEntity);
                }
            }
            return true;
        }

        public void OnEntityBumped(Frame f, EntityRef entity, FPVector2 tileWorldPosition, EntityRef bumpOwner, QBoolean fromBelow) {
            if (!f.Has<CloudBlock>(entity)) {
                return;
            }

            var cloudBlock = f.Unsafe.GetPointer<CloudBlock>(entity);
            if (!cloudBlock->IsDestroying && cloudBlock->SquishCooldownFrames == 0) {
                cloudBlock->SquishCooldownFrames = SQUISH_COOLDOWN;
                f.Events.CloudBlockSquished(entity);
            }
        }

        public void OnEntityCrushed(Frame f, EntityRef entity) {
            if (f.Has<CloudBlock>(entity)) {
                f.Destroy(entity);
            }
        }

        public static void DecrementOwnerCloudCount(Frame f, EntityRef owner) {
            if (!f.Unsafe.TryGetPointer(owner, out MarioPlayer* mario)) {
                return;
            }
            if (mario->CloudCount > 0) {
                mario->CloudCount--;
            }
            if (mario->CloudCount == 0) {
                if (mario->CloudSetMaxReached) {
                    var asset = f.FindAsset(f.SimulationConfig.CloudBlockAsset);
                    if (asset != null) {
                        mario->CloudCooldownFrames = (ushort) asset.CooldownFrames;
                    }
                    mario->CloudSetMaxReached = false;
                }
                mario->CloudHeightLocked = false;
            }
        }

        public static void SummonCloudBlock(Frame f, EntityRef owner, MarioPlayer* mario, Transform2D* ownerTransform, MarioPlayerPhysicsInfo physics) {
            var assetRef = f.SimulationConfig.CloudBlockAsset;
            if (!assetRef.IsValid) {
                return;
            }
            var asset = f.FindAsset(assetRef);

            // Check cooldown
            if (mario->CloudCooldownFrames > 0) {
                return;
            }

            // Check max clouds
            if (mario->CloudCount >= asset.MaxCloudCount) {
                return;
            }

            // Determine spawn height
            FP potentialSpawnY = ownerTransform->Position.Y - CLOUD_SPAWN_VERTICAL_OFFSET;
            FP spawnY;
            if (mario->CloudCount == 0) {
                spawnY = potentialSpawnY;
                mario->CloudFirstSummonY = spawnY;
                mario->CloudHeightLocked = true;
            } else {
                if (potentialSpawnY > mario->CloudFirstSummonY) {
                    // Higher than locked height - lock to previous height
                    spawnY = mario->CloudFirstSummonY;
                } else {
                    // Lower than or equal to locked height - spawn at actual height
                    spawnY = potentialSpawnY;
                    mario->CloudFirstSummonY = spawnY;
                }
            }

            FPVector2 spawnPos = new FPVector2(ownerTransform->Position.X, spawnY);

            EntityRef cloudEntity = f.Create(f.SimulationConfig.CloudBlockPrototype);
            var cloudBlock = f.Unsafe.GetPointer<CloudBlock>(cloudEntity);
            cloudBlock->Initialize(f, cloudEntity, owner, spawnPos, assetRef);

            mario->CloudCount++;
            if (mario->CloudCount >= asset.MaxCloudCount) {
                mario->CloudSetMaxReached = true;
            }

            // Instant cooldown before next cloud can be spawned
            mario->CloudCooldownFrames = (ushort) asset.InstantCooldownFrames;

            // Reset momentum + upward bounce
            var ownerPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(owner);
            ownerPhysicsObject->Velocity = new FPVector2(0, asset.BounceVelocity);
            ownerPhysicsObject->IsTouchingGround = false;
            ownerPhysicsObject->WasTouchingGround = false;
            mario->JumpBufferFrames = 0;

            // Player jump animation + spin
            mario->CloudSpinFrames = 15; // 0.25 seconds at 60 FPS
            mario->JumpState = JumpState.None;
            f.Events.MarioPlayerJumped(owner, mario->CurrentPowerupState, JumpState.None, false, false);
            f.Events.MarioPlayerSummonedCloudBlock(owner);
        }
    }
}
