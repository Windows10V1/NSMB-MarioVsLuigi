using Photon.Deterministic;

namespace Quantum {
    public unsafe class POWBlockSystem : SystemMainThreadEntityFilter<POWBlock, POWBlockSystem.Filter>, ISignalOnThrowHoldable {

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public POWBlock* POWBlock;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* PhysicsCollider;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<MarioPlayer, POWBlock>(f, OnPOWBlockMarioInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var entity = filter.Entity;
            var powBlock = filter.POWBlock;
            var physicsObject = filter.PhysicsObject;

            // Handle throw state
            if (powBlock->IsBeingThrown) {
                // Stop being thrown once it lands
                if (physicsObject->IsTouchingGround) {
                    powBlock->IsBeingThrown = false;
                    powBlock->HasLanded = true;

                    // Explode on floor landing (unless it's a coin item)
                    if (!powBlock->IsCoinItem) {
                        Explode(f, entity);
                    }
                    return;
                }

                physicsObject->Velocity.X = powBlock->FacingRight ? FP._2 : -FP._2;
            }

            // Decrement ignore owner frames
            if (powBlock->IgnoreOwnerFrames > 0) {
                powBlock->IgnoreOwnerFrames--;
            }
        }

        private void Explode(Frame f, EntityRef entity) {
            var powBlock = f.Unsafe.GetPointer<POWBlock>(entity);
            var transform = f.Unsafe.GetPointer<Transform2D>(entity);

            // Damage all players except thrower and teammates
            var players = f.Filter<MarioPlayer, Transform2D>();
            while (players.NextUnsafe(out EntityRef playerEntity, out MarioPlayer* mario, out Transform2D* playerTransform)) {
                // Skip thrower
                if (playerEntity == powBlock->PreviousHolder) {
                    continue;
                }

                // Skip teammates
                if (f.Unsafe.TryGetPointer(powBlock->PreviousHolder, out MarioPlayer* throwerMario)) {
                    if (mario->GetTeam(f) == throwerMario->GetTeam(f)) {
                        continue;
                    }
                }

                // Damage: 2 stars, normal knockback
                bool damaged = mario->DoKnockback(f, playerEntity, playerTransform->Position.X < transform->Position.X, 2, KnockbackStrength.Normal, entity);
                if (damaged) {
                    FPVector2 particlePos = (playerTransform->Position + transform->Position) / 2;
                    f.Events.PlayKnockbackEffect(playerEntity, entity, KnockbackStrength.Normal, particlePos);
                }
            }

            f.Events.POWBlockActivated(entity, powBlock->PreviousHolder);
            f.Destroy(entity);
        }

        public static void Destroy(Frame f, EntityRef entity) {
            f.Destroy(entity);
        }

        #region Interactions
        public static bool OnPOWBlockMarioInteraction(Frame f, EntityRef marioEntity, EntityRef powBlockEntity, PhysicsContact contact) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var powBlock = f.Unsafe.GetPointer<POWBlock>(powBlockEntity);

            // If invincible or mega, break the block
            if (mario->IsStarmanInvincible || mario->CurrentPowerupState == PowerupState.MegaMushroom) {
                Destroy(f, powBlockEntity);
                return true;
            }

            // Allow pickup if not being thrown and not held
            if (!powBlock->IsBeingThrown && !f.Exists(powBlock->Holder)) {
                if (powBlock->HoldAboveHead && mario->CanPickupItem(f, marioEntity, powBlockEntity)) {
                    powBlock->Pickup(f, powBlockEntity, marioEntity);
                }
            }
            return false;
        }
        #endregion

        #region Signals
        public void OnThrowHoldable(Frame f, EntityRef entity, EntityRef marioEntity, QBoolean crouching, QBoolean dropped) {
            if (!f.Unsafe.TryGetPointer(entity, out POWBlock* powBlock)
                || !f.Unsafe.TryGetPointer(marioEntity, out MarioPlayer* mario)
                || !f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)) {
                return;
            }

            powBlock->IsBeingThrown = !dropped;
            powBlock->FacingRight = mario->FacingRight;
            physicsObject->Velocity.Y = 0;
            powBlock->IgnoreOwnerFrames = 60;
            powBlock->HasLanded = false;

            if (!dropped) {
                f.Events.MarioPlayerThrewObject(marioEntity, entity);
            }

            f.Signals.OnThrowPOWBlock(entity, marioEntity, crouching, dropped);
        }
        #endregion
    }
}
