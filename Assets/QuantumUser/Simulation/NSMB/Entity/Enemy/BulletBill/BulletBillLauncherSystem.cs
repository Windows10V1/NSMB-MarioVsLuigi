using Photon.Deterministic;

namespace Quantum {
    public unsafe class BulletBillLauncherSystem : SystemMainThreadEntityFilter<BulletBillLauncher, BulletBillLauncherSystem.Filter>, ISignalOnComponentRemoved<BulletBill>, ISignalOnComponentRemoved<BanzaiBill> {
        public struct Filter {
            public EntityRef Entity;
            public BulletBillLauncher* Launcher;
            public PhysicsCollider2D* Collider;
            public Transform2D* Transform;
        }

        private static readonly FPVector2 SpawnOffset = new FPVector2(0, FP.FromString("-0.45"));
        private static readonly FPVector2 BanzaiSpawnOffset = new FPVector2(0, FP.FromString("-0.80"));

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            // BreakableObject is optional: Banzai Bill launchers aren't breakable.
            if (f.Unsafe.TryGetPointer(filter.Entity, out BreakableObject* breakable)) {
                if (breakable->IsBroken) {
                    return;
                }
            }
            var launcher = filter.Launcher;
            bool isBanzaiLauncher = !f.Has<BreakableObject>(filter.Entity);
            // Regular launchers keep up to 3 bills alive at a time. A freed slot
            // (kill/destroy) can shoot again once the cooldown below elapses.
            if (!isBanzaiLauncher && launcher->BulletBillCount >= 3) {
                return;
            }

            var transform = filter.Transform;
            var collider = filter.Collider;
            // Banzai launchers fire a much bigger bill, nestled lower in the mouth.
            FPVector2 spawnOffset = f.Has<BreakableObject>(filter.Entity) ? SpawnOffset : BanzaiSpawnOffset;
            FPVector2 spawnpoint = transform->Position + FPVector2.Up * (collider->Shape.Box.Extents.Y * 2) + spawnOffset;

            var allPlayers = f.Filter<MarioPlayer, Transform2D>();
            FP smallestDistance = FP.UseableMax;
            bool tooClose = false;
            while (allPlayers.NextUnsafe(out _, out _, out Transform2D* marioTransform)) {
                QuantumUtils.WrappedDistance(stage, spawnpoint, marioTransform->Position, out FP distance);
                FP abs = FPMath.Abs(distance);

                // Player is too close
                if (abs < launcher->MinimumShootRadius) {
                    smallestDistance = FP.UseableMax;
                    tooClose = true;
                    break;
                }

                if (abs < FPMath.Abs(smallestDistance)) {
                    smallestDistance = distance;
                }
            }

            if (FPMath.Abs(smallestDistance) > launcher->MaximumShootRadius) {
                if (!tooClose) {
                    launcher->TimeToShootFrames = launcher->TimeToShoot;
                }
                return;
            }

            if (QuantumUtils.Decrement(ref launcher->TimeToShootFrames)) {
                // Attempt a shot
                var entity = filter.Entity;
                bool right = smallestDistance < 0;

                if (isBanzaiLauncher) {
                    // Fire the first dormant owned bill instead of spawning.
                    // At most 2 owned bills may be live (fired) at once.
                    EntityRef dormantBill = EntityRef.None;
                    int flyingBills = 0;
                    var ownedBills = f.Filter<BanzaiBill, Enemy>();
                    while (ownedBills.NextUnsafe(out EntityRef billEntity, out BanzaiBill* ownedBill, out Enemy* ownedEnemy)) {
                        if (ownedBill->BanzaiOwner != entity && ownedBill->Owner != entity) {
                            continue;
                        }
                        if (!ownedEnemy->IsAlive) {
                            continue;
                        }
                        if (ownedBill->HasFired) {
                            flyingBills++;
                        } else if (dormantBill == EntityRef.None
                            && (!f.Unsafe.TryGetPointer(billEntity, out Freezable* billFreezable) || !billFreezable->IsFrozen(f))) {
                            dormantBill = billEntity;
                        }
                    }

                    launcher->TimeToShootFrames = launcher->TimeToShoot;
                    if (dormantBill != EntityRef.None && flyingBills < 2) {
                        f.Unsafe.GetPointer<Enemy>(dormantBill)->FacingRight = right;
                        f.Unsafe.GetPointer<BanzaiBill>(dormantBill)->HasFired = true;
                        f.Events.BulletBillLauncherShoot(entity, dormantBill, right);
                    }
                    return;
                }

                EntityRef newBillEntity = f.Create(launcher->BulletBillPrototype);
                var newBillTransform = f.Unsafe.GetPointer<Transform2D>(newBillEntity);
                if (f.Unsafe.TryGetPointer(newBillEntity, out BulletBill* newBill)) {
                    newBill->Initialize(f, newBillEntity, entity, right);
                } else if (f.Unsafe.TryGetPointer(newBillEntity, out BanzaiBill* newBanzaiBill)) {
                    newBanzaiBill->Initialize(f, newBillEntity, entity, right);
                }
                newBillTransform->Position = spawnpoint;

                launcher->BulletBillCount++;
                launcher->TimeToShootFrames = launcher->TimeToShoot;

                f.Events.BulletBillLauncherShoot(entity, newBillEntity, right);
            }
        }

        #region Signals
        public void OnRemoved(Frame f, EntityRef entity, BulletBill* component) {
            if (f.Unsafe.TryGetPointer(component->Owner, out BulletBillLauncher* launcher)) {
                launcher->BulletBillCount--;
            }
        }

        public void OnRemoved(Frame f, EntityRef entity, BanzaiBill* component) {
            // Skipped when Kill already freed the slot (Owner cleared there).
            if (component->Owner != EntityRef.None
                && f.Unsafe.TryGetPointer(component->Owner, out BulletBillLauncher* launcher)
                && launcher->BulletBillCount > 0) {
                launcher->BulletBillCount--;
            }
        }

        #endregion
    }
}
