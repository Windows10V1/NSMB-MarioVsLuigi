using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct Projectile {

        // Boomerang state tracking using Combo byte
        // 0 = not a boomerang
        // 1 = boomerang in "going" phase
        // 2 = boomerang in "returning" phase
        // Lifetime is used as a frame counter for boomerangs

        public void Initialize(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, bool right) {
            var asset = f.FindAsset(Asset);
            var transform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);

            // Vars
            Owner = owner;
            FacingRight = right;

            // Speed
            Speed = asset.Speed;
            
            // Super Ball uses its own physics system
            if (asset.IsSuperBall) {
                // Super Ball moves at 45 degrees diagonally
                // Disable physics completely - we'll move it manually
                physicsObject->Gravity = FPVector2.Zero;
                physicsObject->DisableCollision = true;  // Disable physics collisions, we handle manually
                
                // Set position
                transform->Position = spawnpoint;
                // Set velocity to zero - we'll move manually
                physicsObject->Velocity = FPVector2.Zero;
                
                // Store diagonal direction:
                // Combo bits: bit 0 = horizontal direction (0=left, 1=right)
                //             bit 1 = vertical direction (0=down, 1=up)
                byte dirByte = 0;
                if (right) dirByte |= 1;  // bit 0 = horizontal
                dirByte |= 2;             // bit 1 = vertical (always start going up)
                Combo = dirByte;
                
                // Initialize boomerang state if applicable (Super Ball can also be a boomerang)
                if (asset.IsBoomerang) {
                    // Already set in Combo, just mark lifetime
                    Lifetime = 0;
                }
                return;
            }

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

            // Initialize boomerang state if applicable
            if (asset.IsBoomerang) {
                Combo = 1; // Boomerang in "going" phase
                Lifetime = 0; // Reset frame counter
            }
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

        // Helper methods for boomerang state
        public bool IsBoomerang() {
            return Combo > 0;
        }

        public bool IsReturning() {
            return Combo == 2;
        }

        public void SetReturning() {
            Combo = 2;
            Lifetime = 0; // Reset frame counter for return phase
        }
    }
}