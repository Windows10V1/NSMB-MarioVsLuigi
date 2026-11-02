using Photon.Deterministic;
using Quantum.Collections;

namespace Quantum {
    public unsafe class FloatingLogSystem : SystemMainThreadEntityFilter<FloatingLog, FloatingLogSystem.Filter> {

        public struct Filter {
            public EntityRef Entity;
            public FloatingLog* FloatingLog;
            public Transform2D* Transform;
            public PhysicsCollider2D* Collider;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<MarioPlayer, FloatingLog>(f, OnMarioFloatingLogInteraction);
            f.Context.RegisterPreContactCallback(f, OnPreContactCallback);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var log = filter.FloatingLog;
            QHashSet<EntityRef> riders = f.ResolveHashSet(log->Riders);

            bool hasRiders = riders.Count > 0;

            if (hasRiders) {
                // Accelerate spin based on rider count
                FP targetCap = log->MaxSpinSpeed + (riders.Count - 1) * log->MaxSpinSpeedPerExtraRider;
                if (targetCap > log->AbsoluteMaxSpinSpeed) {
                    targetCap = log->AbsoluteMaxSpinSpeed;
                }

                log->SpinSpeed += log->SpinAcceleration * f.DeltaTime;
                if (log->SpinSpeed > targetCap) {
                    log->SpinSpeed = targetCap;
                }
            } else {
                // Decelerate when no riders
                log->SpinSpeed -= log->SpinDeceleration * f.DeltaTime;
                if (log->SpinSpeed < FP._0) {
                    log->SpinSpeed = FP._0;
                }
            }

            // Advance spin angle
            log->SpinAngle += log->SpinSpeed * f.DeltaTime;
            log->SpinAngle = ((log->SpinAngle % 360) + 360) % 360;

            // Process each rider: apply sinking, check fall-through, check jump-cancel
            FP logTopY = GetLogTopY(filter.Transform, filter.Collider);

            // Collect riders to remove (can't modify hash_set while iterating)
            EntityRef[] toRemove = new EntityRef[riders.Count];
            int removeCount = 0;

            foreach (EntityRef riderEntity in riders) {
                if (!f.Unsafe.TryGetPointer(riderEntity, out MarioPlayer* mario)
                    || !f.Unsafe.TryGetPointer(riderEntity, out PhysicsObject* marioPhysics)
                    || !f.Unsafe.TryGetPointer(riderEntity, out Transform2D* marioTransform)) {
                    toRemove[removeCount++] = riderEntity;
                    continue;
                }

                // Skip dead/dying players
                if (mario->IsDead || mario->IsRespawning) {
                    toRemove[removeCount++] = riderEntity;
                    continue;
                }

                FP playerFeetY = marioTransform->Position.Y;
                FP sinkDepth = logTopY - playerFeetY;

                // If player has jumped (velocity moving upward), cancel the ride
                if (marioPhysics->Velocity.Y > FP._0) {
                    toRemove[removeCount++] = riderEntity;
                    continue;
                }

                // If player feet have sunk to or past the drop threshold, stop tracking (they fall)
                if (sinkDepth >= log->DropSinkDepth) {
                    toRemove[removeCount++] = riderEntity;
                    continue;
                }

                // Only sink if spin speed is above threshold
                if (log->SpinSpeed > log->SinkStartSpinSpeed) {
                    FP sinkRate;
                    if (mario->IsGroundpoundActive) {
                        sinkRate = log->GroundpoundSinkRate;
                    } else {
                        // Scale sink rate by how far above threshold we are
                        FP range = log->MaxSpinSpeed - log->SinkStartSpinSpeed;
                        FP t = range > FP._0
                            ? FPMath.Clamp01((log->SpinSpeed - log->SinkStartSpinSpeed) / range)
                            : FP._1;
                        sinkRate = log->SinkRate * t;
                    }

                    // Push player downward at the sink rate
                    marioPhysics->Velocity.Y = -sinkRate;

                    // If within jumpable depth, keep ground state so player can jump
                    if (sinkDepth <= log->JumpableSinkDepth) {
                        marioPhysics->IsTouchingGround = true;
                        mario->ForceJumpTimer = 3;
                    }
                }
            }

            for (int i = 0; i < removeCount; i++) {
                riders.Remove(toRemove[i]);
            }
        }

        // Called each frame for each contact between MarioPlayer and FloatingLog
        public static bool OnMarioFloatingLogInteraction(Frame f, EntityRef marioEntity, EntityRef logEntity, PhysicsContact contact) {
            // Only process downward contacts (player landing on top surface)
            if (FPVector2.Dot(contact.Normal, FPVector2.Up) < Constants.PhysicsGroundMaxAngleCos) {
                return true;
            }

            if (!f.Unsafe.TryGetPointer(marioEntity, out PhysicsObject* marioPhysics)) {
                return true;
            }

            // Skip underwater players
            if (marioPhysics->IsUnderwater) {
                return true;
            }

            if (!f.Unsafe.TryGetPointer(logEntity, out FloatingLog* log)) {
                return true;
            }

            // If player is moving upward (jumping through while inside), don't re-add
            if (marioPhysics->Velocity.Y > FP._0) {
                return true;
            }

            QHashSet<EntityRef> riders = f.ResolveHashSet(log->Riders);
            riders.Add(marioEntity);

            // If this is a groundpound, immediately ramp spin to groundpound speed
            if (f.Unsafe.TryGetPointer(marioEntity, out MarioPlayer* mario) && mario->IsGroundpoundActive) {
                if (log->SpinSpeed < log->GroundpoundMaxSpinSpeed) {
                    log->SpinSpeed = log->GroundpoundMaxSpinSpeed;
                }
            }

            return true;
        }

        private void OnPreContactCallback(Frame f, VersusStageData stage, EntityRef entity, PhysicsContact contact, ref bool keepContacts) {
            // Only care about MarioPlayer contacting a FloatingLog
            if (!f.Has<MarioPlayer>(entity) || !f.Has<FloatingLog>(contact.Entity)) {
                return;
            }

            if (!f.Unsafe.TryGetPointer(contact.Entity, out FloatingLog* log)) {
                return;
            }

            QHashSet<EntityRef> riders = f.ResolveHashSet(log->Riders);

            // Not yet a rider — allow normal contact (this is the landing moment)
            if (!riders.Contains(entity)) {
                return;
            }

            // Player is a rider; check how far they have sunk
            if (!f.Unsafe.TryGetPointer(entity, out Transform2D* marioTransform)
                || !f.Unsafe.TryGetPointer(contact.Entity, out Transform2D* logTransform)
                || !f.Unsafe.TryGetPointer(contact.Entity, out PhysicsCollider2D* logCollider)) {
                return;
            }

            FP logTopY = GetLogTopY(logTransform, logCollider);
            FP playerFeetY = marioTransform->Position.Y;
            FP sinkDepth = logTopY - playerFeetY;

            if (sinkDepth >= log->DropSinkDepth) {
                // Feet have reached the bottom — remove the contact so the player falls through
                keepContacts = false;
            }
            // Otherwise keep contact so physics keeps pushing the player up against feet level
        }

        private static FP GetLogTopY(Transform2D* logTransform, PhysicsCollider2D* logCollider) {
            return logTransform->Position.Y + logCollider->Shape.Centroid.Y + logCollider->Shape.Box.Extents.Y;
        }

        private static FP GetLogBottomY(Transform2D* logTransform, PhysicsCollider2D* logCollider) {
            return logTransform->Position.Y + logCollider->Shape.Centroid.Y - logCollider->Shape.Box.Extents.Y;
        }
    }
}
