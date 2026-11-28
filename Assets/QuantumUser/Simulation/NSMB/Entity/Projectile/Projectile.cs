using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct Projectile {

        public void Initialize(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, bool right) {
            var asset = f.FindAsset(Asset);
            var transform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);

            // Vars
            Owner = owner;
            FacingRight = right;

            // Speed
            Speed = asset.Speed;
            physicsObject->Gravity = asset.Gravity;
            if (asset.InheritShooterVelocity
                && f.Unsafe.TryGetPointer(owner, out PhysicsObject* ownerPhysicsObject)
                // Moving in same direction
                && FPMath.Sign(ownerPhysicsObject->Velocity.X) == 1 == FacingRight) { 

                Speed += FPMath.Abs(ownerPhysicsObject->Velocity.X / 3);
            }

            if (asset.LockTo45Degrees) {
                physicsObject->TerminalVelocity = -Speed;
            }

            // Physics
            transform->Position = spawnpoint;
            physicsObject->Velocity = new(Speed * (FacingRight ? 1 : -1), -Speed);
        }

        public void InitializeHammer(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, bool right, bool playerHoldingUp) {
            var asset = f.FindAsset(Asset);
            var transform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);

            // Vars
            Owner = owner;
            FacingRight = right;

            // Initial Velocity
            FPVector2 velocity = playerHoldingUp ? new FPVector2(FP.FromString("3.8822"), FP.FromString("14.4888")) : new FPVector2(FP.FromString("6.25"), FP.FromString("7.5"));
            Speed = velocity.X;
            
            // Apply
            transform->Position = spawnpoint;
            physicsObject->Velocity = velocity;
            physicsObject->Gravity = FPVector2.Up * (playerHoldingUp ? FP.FromString("-37.512") : FP.FromString("-28.125"));
        }

        public void InitializeBoomerang(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, bool right) {
            var asset = f.FindAsset(Asset);
            var transform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);

            // Vars
            Owner = owner;
            FacingRight = right;

            // Speed
            Speed = asset.Speed;
            physicsObject->Gravity = asset.Gravity;
            if (asset.InheritShooterVelocity
                && f.Unsafe.TryGetPointer(owner, out PhysicsObject* ownerPhysicsObject)
                // Moving in same direction
                && FPMath.Sign(ownerPhysicsObject->Velocity.X) == 1 == FacingRight) { 

                Speed += FPMath.Abs(ownerPhysicsObject->Velocity.X / 3);
            }

            // Physics
            Combo = 0; // There are 4 phases for Boomerang: 0 = going, 1 = pause, 2 = return, 3 = overshoot.
            Frame = 0; // Lifetime frames counter
            OvershootFrames = 20; // Length of the next overshoot, halves after every overshoot
            transform->Position = spawnpoint;
            // The prefab's terminal velocity is 0, which clamps any downward velocity to 0 after every physics step
            physicsObject->TerminalVelocity = -asset.Speed;
            physicsObject->Velocity = new(Speed * (FacingRight ? 1 : -1), 0);
        }

        public void UpdateBoomerang(Frame f, EntityRef thisEntity, PhysicsObject* physicsObject, VersusStageData stage) {
            var asset = f.FindAsset(Asset);

            if (!f.Exists(thisEntity) || f.DestroyPending(thisEntity)) {
                return;
            }

            Frame++;

            if (Combo == 0) {
                if (Frame >= 25) {
                    Combo = 1;
                    Frame = 0;
                } 
            } else if (Combo == 1) {
                Speed = 0;
                physicsObject->Velocity.Y = 0;
                if (Frame >= 20) {
                    Combo = 2;
                    Frame = 0;
                    UpdateBoomerangHeight(f, thisEntity, physicsObject);
                }
            } else {
                Speed = asset.Speed;

                if (!f.Unsafe.TryGetPointer(thisEntity, out Transform2D* transform) || !f.Unsafe.TryGetPointer(thisEntity, out PhysicsCollider2D* collider)) {
                    return;
                }

                // Touching the owner despawns it
                var hits = f.Physics2D.OverlapShape(transform->Position, 0, collider->Shape, f.Context.PlayerOnlyMask);
                for (int i = 0; i < hits.Count; i++) {
                    if (hits[i].Entity == Owner) {
                        ProjectileSystem.Destroy(f, thisEntity, ParticleEffect.None);
                        return;
                    }
                }

                physicsObject->Gravity = FPVector2.Zero;

                if (Combo == 3) {
                    // Keep flying past the owner (FacingRight isn't changed), then hover again
                    if (Frame >= OvershootFrames) {
                        OvershootFrames /= 2;
                        Combo = 1;
                        Frame = 0;
                    }
                    return;
                }

                if (f.Unsafe.TryGetPointer(Owner, out Transform2D* ownerTransform)) {
                    QuantumUtils.UnwrapWorldLocations(stage, transform->Position, ownerTransform->Position, out _, out FPVector2 closestOwner);
                    FP distanceX = closestOwner.X - transform->Position.X;

                    if (FPMath.Abs(distanceX) > Speed * f.DeltaTime) {
                        // Keep track of the owner's X position
                        FacingRight = distanceX > 0;
                    } else if (OvershootFrames < 5) {
                        // Reached the owner's X, and there's no overshoot left
                        ProjectileSystem.Destroy(f, thisEntity, asset.DestroyParticleEffect);
                        return;
                    } else {
                        // Reached the owner's X, fly past it
                        Combo = 3;
                        Frame = 0;
                        UpdateBoomerangHeight(f, thisEntity, physicsObject);
                    }
                }

                physicsObject->Velocity.X = Speed * (FacingRight ? 1 : -1);
            }
        }

        // Checks if the owner is decently above/below the boomerang, and sets its vertical velocity (up, down or none)
        private void UpdateBoomerangHeight(Frame f, EntityRef thisEntity, PhysicsObject* physicsObject) {
            var asset = f.FindAsset(Asset);

            physicsObject->Velocity.Y = 0;

            if (!f.Unsafe.TryGetPointer(thisEntity, out Transform2D* transform)
                || !f.Unsafe.TryGetPointer(thisEntity, out PhysicsCollider2D* collider)
                || !f.Unsafe.TryGetPointer(Owner, out Transform2D* ownerTransform)
                || !f.Unsafe.TryGetPointer(Owner, out PhysicsCollider2D* ownerCollider)) {
                return;
            }

            FP heightDifference = (ownerTransform->Position.Y + ownerCollider->Shape.Centroid.Y) - (transform->Position.Y + collider->Shape.Centroid.Y);

            // "Decent" = the hitboxes can't overlap vertically
            FP threshold = ownerCollider->Shape.Box.Extents.Y + collider->Shape.Box.Extents.Y;

            if (heightDifference > threshold) {
                physicsObject->Velocity.Y = asset.Speed / 2;
            } else if (heightDifference < -threshold) {
                physicsObject->Velocity.Y = -asset.Speed / 2;
            }
        }
    }
}