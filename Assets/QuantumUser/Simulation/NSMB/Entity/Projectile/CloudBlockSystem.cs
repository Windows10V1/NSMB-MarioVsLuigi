using Photon.Deterministic;
using Quantum.Collections;

namespace Quantum {
    public unsafe class CloudBlockSystem : SystemMainThreadEntityFilter<CloudBlock, CloudBlockSystem.Filter> {

        public struct Filter {
            public EntityRef Entity;
            public CloudBlock* CloudBlock;
        }

        public override void OnInit(Frame f) {
            f.Context.RegisterPreContactCallback(f, OnPreContactCallback);
            f.Context.Interactions.Register<MarioPlayer, CloudBlock>(f, OnMarioCloudBlockInteraction);
            f.Context.Interactions.Register<Enemy, CloudBlock>(f, OnEnemyCloudBlockInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            CloudBlock* cloudBlock = filter.CloudBlock;
            CloudBlockProjectileAsset asset = f.FindAsset(cloudBlock->Asset);

            if (cloudBlock->ActionLockFrames > 0
                && QuantumUtils.Decrement(ref cloudBlock->ActionLockFrames)
                && !cloudBlock->Destroying
                && cloudBlock->Animation == CloudBlockAnimation.Summon) {
                cloudBlock->PlayAnimation(CloudBlockAnimation.Idle);
            }

            if (cloudBlock->Destroying) {
                UpdateContactMemory(f, cloudBlock);
                if (cloudBlock->DestroyFrames == 0 || QuantumUtils.Decrement(ref cloudBlock->DestroyFrames)) {
                    FinalDestroy(f, filter.Entity, cloudBlock, asset);
                }
                return;
            }

            if (cloudBlock->Lifetime > 0 && QuantumUtils.Decrement(ref cloudBlock->Lifetime)) {
                StartDestroy(f, filter.Entity, cloudBlock, asset, false);
            }
            UpdateContactMemory(f, cloudBlock);
        }

        public static void StartDestroy(Frame f, EntityRef cloudBlockEntity, CloudBlock* cloudBlock, CloudBlockProjectileAsset asset, bool immediate) {
            if (immediate) {
                FinalDestroy(f, cloudBlockEntity, cloudBlock, asset);
                return;
            }

            if (cloudBlock->Destroying) {
                return;
            }

            cloudBlock->Destroying = true;
            cloudBlock->ActionLockFrames = asset.DestroyInactiveFrames;
            cloudBlock->DestroyFrames = asset.DestroyAnimationFrames;
            cloudBlock->Animation = CloudBlockAnimation.Destroy;
            cloudBlock->AnimationCounter++;

            if (cloudBlock->DestroyFrames == 0) {
                FinalDestroy(f, cloudBlockEntity, cloudBlock, asset);
            }
        }

        private static void FinalDestroy(Frame f, EntityRef cloudBlockEntity, CloudBlock* cloudBlock, CloudBlockProjectileAsset asset) {
            if (f.Unsafe.TryGetPointer(cloudBlock->Owner, out MarioPlayer* owner)) {
                if (owner->ActiveCloudBlocks > 0) {
                    owner->ActiveCloudBlocks--;
                }
                TryStartRestoreCooldown(owner, asset);
            }

            f.Destroy(cloudBlockEntity);
        }

        public static void TryStartRestoreCooldown(MarioPlayer* owner, CloudBlockProjectileAsset asset) {
            if (owner->CloudBlocksUsed < GetMaxCloudBlocks(asset)
                || owner->ActiveCloudBlocks > 0
                || owner->CloudBlockCooldownFrames > 0) {
                return;
            }

            if (asset.RestoreCooldownFrames == 0) {
                ResetCloudBlockCharges(owner);
            } else {
                owner->CloudBlockCooldownFrames = asset.RestoreCooldownFrames;
            }
        }

        public static void ResetCloudBlockCharges(MarioPlayer* mario) {
            mario->CloudBlocksUsed = 0;
            mario->ActiveCloudBlocks = 0;
            mario->CloudBlockCooldownFrames = 0;
            mario->CloudBlockBaseHeight = default;
            mario->CloudBlockBaseHeightSet = false;
        }

        public static byte GetMaxCloudBlocks(CloudBlockProjectileAsset asset) {
            return asset.MaxCloudBlocks == 0 ? (byte) 3 : asset.MaxCloudBlocks;
        }

        public static byte GetMaxInstantCloudBlocks(CloudBlockProjectileAsset asset) {
            return asset.MaxInstantCloudBlocks == 0 ? (byte) 1 : asset.MaxInstantCloudBlocks;
        }

        private static bool OnMarioCloudBlockInteraction(Frame f, EntityRef marioEntity, EntityRef cloudBlockEntity, PhysicsContact contact) {
            CloudBlock* cloudBlock = f.Unsafe.GetPointer<CloudBlock>(cloudBlockEntity);
            CloudBlockProjectileAsset asset = f.FindAsset(cloudBlock->Asset);

            if (!IsTopContact(contact)) {
                return true;
            }

            MarioPlayer* mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            PhysicsObject* physicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);

            if (cloudBlock->CanRunActions) {
                if (mario->IsGroundpoundActive || mario->IsDrilling) {
                    cloudBlock->PlayAnimation(CloudBlockAnimation.HardSquish);

                    if (mario->CurrentPowerupState == PowerupState.MegaMushroom) {
                        StartDestroy(f, cloudBlockEntity, cloudBlock, asset, true);
                        return false;
                    }

                    if (CanUseCloudBlockLaunch(f, marioEntity, cloudBlock->Owner)) {
                        LaunchMarioFromCloudBlock(mario, physicsObject, asset);
                        cloudBlock->QueueSound(asset.GroundpoundBounceSound);
                    }
                    return false;
                }

                bool landed = !physicsObject->WasTouchingGround && physicsObject->PreviousFrameVelocity.Y <= 0;
                if (landed && RegisterContact(f, cloudBlock, marioEntity)) {
                    cloudBlock->PlayAnimation(mario->CurrentPowerupState == PowerupState.MegaMushroom
                        ? CloudBlockAnimation.HardSquish
                        : CloudBlockAnimation.SoftSquish);
                }
            } else {
                RegisterContact(f, cloudBlock, marioEntity);
            }

            return true;
        }

        private static bool OnEnemyCloudBlockInteraction(Frame f, EntityRef enemyEntity, EntityRef cloudBlockEntity, PhysicsContact contact) {
            if (!IsTopContact(contact) || f.Has<BulletBill>(enemyEntity)) {
                return true;
            }

            Enemy* enemy = f.Unsafe.GetPointer<Enemy>(enemyEntity);
            if (enemy->IsDead) {
                return true;
            }

            CloudBlock* cloudBlock = f.Unsafe.GetPointer<CloudBlock>(cloudBlockEntity);
            if (cloudBlock->CanRunActions && RegisterContact(f, cloudBlock, enemyEntity)) {
                cloudBlock->PlayAnimation(CloudBlockAnimation.SoftSquish);
            }
            return true;
        }

        private static bool RegisterContact(Frame f, CloudBlock* cloudBlock, EntityRef entity) {
            QHashSet<EntityRef> currentContacts = f.ResolveHashSet(cloudBlock->CurrentContacts);
            if (currentContacts.Contains(entity)) {
                return false;
            }

            currentContacts.Add(entity);
            QHashSet<EntityRef> previousContacts = f.ResolveHashSet(cloudBlock->PreviousContacts);
            return !previousContacts.Contains(entity);
        }

        private static void UpdateContactMemory(Frame f, CloudBlock* cloudBlock) {
            QHashSet<EntityRef> currentContacts = f.ResolveHashSet(cloudBlock->CurrentContacts);
            QHashSet<EntityRef> previousContacts = f.ResolveHashSet(cloudBlock->PreviousContacts);

            previousContacts.Clear();
            foreach (EntityRef entity in currentContacts) {
                previousContacts.Add(entity);
            }
            currentContacts.Clear();
        }

        private void OnPreContactCallback(Frame f, VersusStageData stage, EntityRef entity, PhysicsContact contact, ref bool keepContacts) {
            if (!f.Unsafe.TryGetPointer(contact.Entity, out CloudBlock* cloudBlock)) {
                return;
            }

            keepContacts &= IsTopContact(contact);
        }

        private static bool IsTopContact(PhysicsContact contact) {
            return FPVector2.Dot(contact.Normal, FPVector2.Up) > Constants.PhysicsGroundMaxAngleCos;
        }

        private static bool CanUseCloudBlockLaunch(Frame f, EntityRef marioEntity, EntityRef ownerEntity) {
            if (marioEntity == ownerEntity) {
                return true;
            }

            if (!f.Global->Rules.TeamsEnabled
                || !f.Unsafe.TryGetPointer(marioEntity, out MarioPlayer* mario)
                || !f.Unsafe.TryGetPointer(ownerEntity, out MarioPlayer* owner)) {
                return false;
            }

            byte? marioTeam = mario->GetTeam(f);
            byte? ownerTeam = owner->GetTeam(f);
            return marioTeam.HasValue && ownerTeam.HasValue && marioTeam.Value == ownerTeam.Value;
        }

        private static void LaunchMarioFromCloudBlock(MarioPlayer* mario, PhysicsObject* physicsObject, CloudBlockProjectileAsset asset) {
            mario->IsGroundpounding = false;
            mario->IsGroundpoundActive = false;
            mario->GroundpoundStartFrames = 0;
            mario->GroundpoundStandFrames = 0;
            mario->IsDrilling = false;
            mario->IsSpinnerFlying = false;
            mario->IsPropellerFlying = false;
            mario->JumpState = JumpState.None;
            mario->CoyoteTimeFrames = 0;

            physicsObject->Velocity.Y = asset.GroundpoundLaunchVelocity;
            physicsObject->IsTouchingGround = false;
            physicsObject->WasTouchingGround = false;
            physicsObject->HoverFrames = 0;
        }
    }
}
