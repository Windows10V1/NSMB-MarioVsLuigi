using Photon.Deterministic;

namespace Quantum {
    public unsafe class POWBlockSystem : SystemMainThreadEntityFilter<POWBlock, POWBlockSystem.Filter>, ISignalOnThrowHoldable, ISignalOnEntityBumped, ISignalOnEntityCrushed {

        private const byte DefaultMaxUses = 3;
        private static readonly FP ActivatorBounceVelocity = FP.FromFloat_UNSAFE(6);
        private static readonly FP ReExplosionLaunchVelocity = FP.FromFloat_UNSAFE(10);
        private const int KnockbackExtensionFrames = 15;

        public struct Filter {
            public EntityRef Entity;
            public POWBlock* POWBlock;
            public Transform2D* Transform;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* PhysicsCollider;
            public Holdable* Holdable;
            public CoinItem* CoinItem;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<MarioPlayer, POWBlock>(f, OnMarioPOWBlockInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var powBlock = filter.POWBlock;
            var coinItem = filter.CoinItem;

            if (!powBlock->SpawnOwner.IsValid && coinItem->ParentMarioPlayer.IsValid) {
                powBlock->SpawnOwner = coinItem->ParentMarioPlayer;
            }

            if (coinItem->SpawnAnimationFrames > 0) {
                return;
            }

            if (filter.PhysicsObject->DisableCollision) {
                filter.PhysicsObject->DisableCollision = false;
            }

            if (f.Exists(filter.Holdable->Holder)) {
                if (PhysicsObjectSystem.BoxInGround(f, filter.Transform->Position, filter.PhysicsCollider->Shape, stage: stage, entity: filter.Entity)) {
                    Activate(f, filter.Entity, EntityRef.None);
                }
                PhysicsObjectSystem.TryEject(f, filter.Entity, stage);
                return;
            }

            if (powBlock->CanGroundActivate && filter.PhysicsObject->IsTouchingGround && !filter.PhysicsObject->WasTouchingGround) {
                Activate(f, filter.Entity, powBlock->Activator);
            }
        }

        private static bool OnMarioPOWBlockInteraction(Frame f, EntityRef marioEntity, EntityRef powBlockEntity, PhysicsContact contact) {
            if (!f.Exists(powBlockEntity) || f.DestroyPending(powBlockEntity)) {
                return false;
            }

            var powHoldable = f.Unsafe.GetPointer<Holdable>(powBlockEntity);
            if (powHoldable->PreviousHolder == marioEntity && powHoldable->IgnoreOwnerFrames > 0) {
                return false;
            }

            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity);
            var powBlock = f.Unsafe.GetPointer<POWBlock>(powBlockEntity);
            var powTransform = f.Unsafe.GetPointer<Transform2D>(powBlockEntity);
            var powPhysics = f.Unsafe.GetPointer<PhysicsObject>(powBlockEntity);
            var coinItem = f.Unsafe.GetPointer<CoinItem>(powBlockEntity);

            FP upDot = FPVector2.Dot(contact.Normal, FPVector2.Up);
            bool held = f.Exists(powHoldable->Holder);
            bool active = coinItem->SpawnAnimationFrames == 0 && coinItem->IgnorePlayerFrames == 0;

            if (active && upDot >= Constants.PhysicsGroundMaxAngleCos && mario->IsGroundpoundActive) {
                EntityRef activator = powBlock->WasThrown && powBlock->Activator.IsValid ? powBlock->Activator : marioEntity;
                Activate(f, powBlockEntity, activator);
                return false;
            }

            if (active && !held && FPMath.Abs(upDot) < Constants.PhysicsGroundMaxAngleCos && powBlock->WasThrown) {
                if (mario->IsInShell) {
                    mario->FacingRight = contact.Normal.X < 0;
                    Activate(f, powBlockEntity, marioEntity);
                    return false;
                }

                if (mario->IsPenguinSliding) {
                    mario->IsPenguinSliding = false;
                    if (f.Unsafe.TryGetPointer(marioEntity, out PhysicsObject* marioPhysics)) {
                        marioPhysics->Velocity.X = 0;
                    }
                    Activate(f, powBlockEntity, marioEntity);
                    return false;
                }
            }

            if (active && !held && ShouldHardDamageMario(f, marioEntity, powBlock, powBlockEntity)) {
                bool fallingOntoMario = upDot <= -Constants.PhysicsGroundMaxAngleCos && powPhysics->Velocity.Y < -FP._0_10;
                bool thrownSideHit = FPMath.Abs(upDot) < Constants.PhysicsGroundMaxAngleCos && powBlock->WasThrown && powBlock->CanGroundActivate;

                if ((fallingOntoMario || thrownSideHit) && ApplyHardDamage(f, marioEntity, mario, marioTransform, powBlockEntity, powTransform, powPhysics)) {
                    return false;
                }
            }

            if (active && !held && mario->CanPickupItem(f, marioEntity, powBlockEntity)) {
                powHoldable->Pickup(f, powBlockEntity, marioEntity);
                powBlock->WasThrown = false;
                powBlock->CanGroundActivate = false;
                powBlock->Activator = EntityRef.None;
            }

            return false;
        }

        private static void Activate(Frame f, EntityRef powBlockEntity, EntityRef activator) {
            if (!f.Exists(powBlockEntity) || f.DestroyPending(powBlockEntity)) {
                return;
            }

            var powBlock = f.Unsafe.GetPointer<POWBlock>(powBlockEntity);
            var transform = f.Unsafe.GetPointer<Transform2D>(powBlockEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(powBlockEntity);
            var holdable = f.Unsafe.GetPointer<Holdable>(powBlockEntity);

            if (!activator.IsValid && powBlock->Activator.IsValid) {
                activator = powBlock->Activator;
            }

            if (activator.IsValid && !f.Unsafe.TryGetPointer(activator, out MarioPlayer* _)) {
                return;
            }

            ReleaseFromHolder(f, holdable);

            bool wasThrown = powBlock->WasThrown;
            powBlock->Activator = activator;
            powBlock->WasThrown = false;
            powBlock->CanGroundActivate = false;
            powBlock->Uses++;

            f.Events.POWBlockActivated(powBlockEntity, activator, transform->Position, powBlock->Uses);
            ApplyExplosion(f, powBlockEntity, activator, transform->Position, wasThrown);

            if (f.Unsafe.TryGetPointer(powBlockEntity, out PhysicsCollider2D* collider)) {
                FPVector2 extents = collider->Shape.Box.Extents;
                extents.Y /= 2;
                collider->Shape.Box.Extents = extents;
                collider->Shape.Centroid.Y = extents.Y;
            }

            byte maxUses = powBlock->MaxUses == 0 ? DefaultMaxUses : powBlock->MaxUses;
            if (powBlock->Uses >= maxUses) {
                f.Events.CollectableDespawned(powBlockEntity, transform->Position, false);
                f.Destroy(powBlockEntity);
                return;
            }

            physicsObject->Velocity.X = 0;
            physicsObject->Velocity.Y = Constants._5_50;
            physicsObject->IsTouchingGround = false;
            physicsObject->WasTouchingGround = false;
            physicsObject->HoverFrames = 0;
        }

        private static void ApplyExplosion(Frame f, EntityRef powBlockEntity, EntityRef activator, FPVector2 position, bool thrown) {
            if (activator.IsValid && thrown) {
                ApplyGroundBounceForTeam(f, activator);
            }

            var players = f.Filter<MarioPlayer, PhysicsObject, Transform2D>();
            while (players.NextUnsafe(out EntityRef marioEntity, out MarioPlayer* mario, out PhysicsObject* physicsObject, out Transform2D* marioTransform)) {
                if (marioEntity == activator) {
                    continue;
                }

                bool wasHardDamaged = mario->CurrentKnockback != KnockbackStrength.None && mario->LastAttacker == powBlockEntity;
                if (wasHardDamaged) {
                    mario->IsGroundpounding = false;

                    physicsObject->Velocity.Y = ReExplosionLaunchVelocity;
                    physicsObject->IsTouchingGround = false;
                    physicsObject->WasTouchingGround = false;
                    physicsObject->HoverFrames = 0;

                    mario->KnockbackTick -= KnockbackExtensionFrames;

                    if (activator.IsValid) {
                        f.Signals.OnMarioPlayerDropObjective(marioEntity, 1, activator);
                    }
                    f.Events.PlayKnockbackEffect(marioEntity, powBlockEntity, KnockbackStrength.Normal, position);
                    continue;
                }

                bool fromRight = GetExplosionKnockbackFromRight(mario, physicsObject);
                EntityRef attacker = activator.IsValid ? activator : powBlockEntity;

                if (mario->IsInKnockback) {
                    mario->CurrentKnockback = KnockbackStrength.None;
                    mario->IsInWeakKnockback = false;
                }

                bool belowPOW = marioTransform->Position.Y < position.Y;

                bool damaged;
                if (belowPOW) {
                    int starsToDrop = activator.IsValid && SameTeam(f, activator, marioEntity) ? 0 : 1;
                    damaged = mario->DoKnockback(f, marioEntity, fromRight, starsToDrop, KnockbackStrength.Groundpound, attacker, bypassDamageInvincibility: true);
                    if (damaged) {
                        f.Events.PlayKnockbackEffect(marioEntity, powBlockEntity, KnockbackStrength.Groundpound, position);
                    }
                } else {
                    int starsToDrop = activator.IsValid && SameTeam(f, activator, marioEntity) ? 0 : 1;
                    damaged = mario->DoKnockback(f, marioEntity, fromRight, starsToDrop, KnockbackStrength.Normal, attacker, bypassDamageInvincibility: true);
                    if (damaged) {
                        f.Events.PlayKnockbackEffect(marioEntity, powBlockEntity, KnockbackStrength.Normal, position);
                    }
                }
            }
        }

        private static void ApplyGroundBounceForTeam(Frame f, EntityRef activator) {
            if (!f.Unsafe.TryGetPointer(activator, out MarioPlayer* activatorMario)
                || !f.Unsafe.TryGetPointer(activator, out PhysicsObject* activatorPhysics)) {
                return;
            }

            var teamPlayers = f.Filter<MarioPlayer, PhysicsObject, Transform2D>();
            while (teamPlayers.NextUnsafe(out EntityRef playerEntity, out MarioPlayer* playerMario, out PhysicsObject* playerPhysics, out _)) {
                if (playerEntity == activator) {
                    if (activatorPhysics->IsTouchingGround) {
                        ApplyGroundBounce(activatorMario, activatorPhysics);
                    }
                } else if (SameTeam(f, activator, playerEntity) && playerPhysics->IsTouchingGround) {
                    ApplyGroundBounce(playerMario, playerPhysics);
                }
            }
        }

        private static void ApplyGroundBounce(MarioPlayer* mario, PhysicsObject* physicsObject) {
            mario->POWBounceFrames = 1;
            mario->IsGroundpounding = false;
            mario->IsCrouching = false;
            physicsObject->Velocity.X = 0;
            physicsObject->Velocity.Y = ActivatorBounceVelocity;
            physicsObject->IsTouchingGround = false;
            physicsObject->WasTouchingGround = false;
            physicsObject->HoverFrames = 0;
        }

        private static bool ApplyHardDamage(Frame f, EntityRef marioEntity, MarioPlayer* mario, Transform2D* marioTransform, EntityRef powBlockEntity, Transform2D* powTransform, PhysicsObject* powPhysics) {
            bool fromRight = GetImpactKnockbackFromRight(f, marioTransform, powTransform, powPhysics, mario);
            bool damaged = mario->DoKnockback(f, marioEntity, fromRight, 1, KnockbackStrength.Groundpound, powBlockEntity);
            if (damaged) {
                f.Events.PlayKnockbackEffect(marioEntity, powBlockEntity, KnockbackStrength.Groundpound, (marioTransform->Position + powTransform->Position) / 2);
            }
            return damaged;
        }

        private static bool ShouldHardDamageMario(Frame f, EntityRef marioEntity, POWBlock* powBlock, EntityRef powBlockEntity) {
            if (powBlock->Activator.IsValid && powBlock->Activator == marioEntity) {
                return false;
            }

            if (f.Unsafe.TryGetPointer(powBlockEntity, out Holdable* holdable)
                && holdable->PreviousHolder == marioEntity && holdable->IgnoreOwnerFrames > 0) {
                return false;
            }

            return true;
        }

        private static bool GetExplosionKnockbackFromRight(MarioPlayer* mario, PhysicsObject* physicsObject) {
            if (physicsObject->Velocity.X < -FP._0_05) {
                return true;
            }
            if (physicsObject->Velocity.X > FP._0_05) {
                return false;
            }
            return !mario->FacingRight;
        }

        private static bool GetImpactKnockbackFromRight(Frame f, Transform2D* marioTransform, Transform2D* powTransform, PhysicsObject* powPhysics, MarioPlayer* mario) {
            QuantumUtils.UnwrapWorldLocations(f, marioTransform->Position, powTransform->Position, out FPVector2 marioPos, out FPVector2 powPos);
            FP xDelta = powPos.X - marioPos.X;
            if (FPMath.Abs(xDelta) > FP._0_05) {
                return xDelta > 0;
            }
            if (powPhysics->Velocity.X < -FP._0_05) {
                return true;
            }
            if (powPhysics->Velocity.X > FP._0_05) {
                return false;
            }
            return !mario->FacingRight;
        }

        private static bool SameTeam(Frame f, EntityRef a, EntityRef b) {
            if (!f.Unsafe.TryGetPointer(a, out MarioPlayer* marioA)
                || !f.Unsafe.TryGetPointer(b, out MarioPlayer* marioB)) {
                return false;
            }

            byte? teamA = marioA->GetTeam(f);
            byte? teamB = marioB->GetTeam(f);
            return teamA.HasValue && teamB.HasValue && teamA.Value == teamB.Value;
        }

        private static void ReleaseFromHolder(Frame f, Holdable* holdable) {
            if (f.Unsafe.TryGetPointer(holdable->Holder, out MarioPlayer* holderMario)) {
                holderMario->HeldEntity = EntityRef.None;
            }

            if (holdable->Holder.IsValid) {
                holdable->PreviousHolder = holdable->Holder;
            }
            holdable->Holder = EntityRef.None;
        }

        public void OnThrowHoldable(Frame f, EntityRef entity, EntityRef marioEntity, QBoolean crouching, QBoolean dropped) {
            if (!f.Unsafe.TryGetPointer(entity, out POWBlock* powBlock)
                || !f.Unsafe.TryGetPointer(entity, out Holdable* holdable)
                || !f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)
                || !f.Unsafe.TryGetPointer(entity, out PhysicsCollider2D* collider)
                || !f.Unsafe.TryGetPointer(entity, out Transform2D* transform)
                || !f.Unsafe.TryGetPointer(marioEntity, out MarioPlayer* mario)
                || !f.Unsafe.TryGetPointer(marioEntity, out PhysicsObject* marioPhysics)) {
                return;
            }

            if (!dropped) {
                powBlock->WasThrown = true;
                powBlock->CanGroundActivate = true;
                powBlock->Activator = marioEntity;

                if (holdable->HoldAboveHead) {
                    var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity);
                    var marioShape = f.Unsafe.GetPointer<PhysicsCollider2D>(marioEntity)->Shape;
                    FP holdableYOffset = collider->Shape.Box.Extents.Y - collider->Shape.Centroid.Y;
                    FP pickupFrames = 27;
                    FP time = FPMath.Clamp01((f.Number - mario->HoldStartFrame) / pickupFrames);
                    FP alpha = 1 - QuantumUtils.EaseOut(1 - time);
                    transform->Position = marioTransform->Position + new FPVector2(
                        0,
                        (marioShape.Box.Extents.Y * (2 - FP._0_05) * alpha) + holdableYOffset
                    );
                }
            }

            if (!powBlock->SpawnOwner.IsValid) {
                powBlock->SpawnOwner = marioEntity;
            }

            physicsObject->Velocity.Y = 0;
            if (dropped) {
                physicsObject->Velocity.X = 0;
                powBlock->WasThrown = false;
                powBlock->CanGroundActivate = false;
                powBlock->Activator = EntityRef.None;
            } else if (crouching) {
                physicsObject->Velocity.X = mario->FacingRight ? 1 : -1;
            } else {
                physicsObject->Velocity.X = (Constants._4_50 + FPMath.Abs(marioPhysics->Velocity.X / 3)) * (mario->FacingRight ? 1 : -1);
                f.Events.MarioPlayerThrewObject(marioEntity, entity);
            }

            holdable->IgnoreOwnerFrames = 30;
        }

        public void OnEntityBumped(Frame f, EntityRef entity, FPVector2 tileWorldPosition, EntityRef blockBump, QBoolean fromBelow) {
            if (!f.Unsafe.TryGetPointer(entity, out POWBlock* powBlock)
                || !f.Unsafe.TryGetPointer(entity, out Holdable* holdable)
                || !f.Unsafe.TryGetPointer(entity, out CoinItem* coinItem)
                || !f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)
                || coinItem->SpawnAnimationFrames > 0
                || f.Exists(holdable->Holder)) {
                return;
            }

            powBlock->WasThrown = false;
            powBlock->CanGroundActivate = false;
            physicsObject->Velocity.Y = Constants._5_50;
            physicsObject->IsTouchingGround = false;
        }

        public void OnEntityCrushed(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out POWBlock* _)
                && f.Unsafe.TryGetPointer(entity, out Transform2D* transform)) {
                f.Events.CollectableDespawned(entity, transform->Position, false);
                f.Destroy(entity);
            }
        }
    }
}
