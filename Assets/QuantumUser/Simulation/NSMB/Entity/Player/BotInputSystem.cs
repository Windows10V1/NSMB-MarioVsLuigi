using Photon.Deterministic;
using Quantum.Collections;

namespace Quantum {
    /// <summary>
    /// Deterministic bot input system that autonomously controls MarioPlayer entities.
    /// Runs inside Quantum's simulation loop to provide input for bot-controlled players.
    /// </summary>
    public unsafe class BotInputSystem : SystemMainThread {
        
        // Bot behavior configuration
        private static readonly FP JumpCheckDistance = FP.FromString("3.0");
        private static readonly FP WallCheckDistance = FP.FromString("2.0");
        private static readonly FP GapCheckDistance = FP.FromString("2.5");
        private static readonly FP StarChaseDistance = FP.FromString("10.0");
        private static readonly FP FpZeroPointOne = FP.FromString("0.1");
        private static readonly FP FpOne = FP.FromString("1.0");
        private static readonly FP FpOnePointFive = FP.FromString("1.5");
        private static readonly FP FpTwo = FP.FromString("2.0");
        private static readonly FP FpThree = FP.FromString("3.0");
        private static readonly FP FpZeroPointFive = FP.FromString("0.5");
        private static readonly FP FpFive = FP.FromString("5.0");
        private static readonly FP FpFour = FP.FromString("4.0");
        private const byte JumpBufferFrames = 5;
        private const byte ReactionDelayFrames = 3;

        public override void Update(Frame f) {
            // Get the active BigStar objective
            EntityRef bigStarEntity = f.Global->MainBigStar;
            FPVector2? targetPosition = null;
            
            if (f.Exists(bigStarEntity)) {
                var starTransform = f.Unsafe.GetPointer<Transform2D>(bigStarEntity);
                targetPosition = starTransform->Position;
            }

            // Iterate through all MarioPlayer entities
            var filter = f.Filter<MarioPlayer, Transform2D, PhysicsObject>();
            while (filter.NextUnsafe(out EntityRef entity, out var mario, out var transform, out var physicsObject)) {
                // Skip if not a bot or if player is invalid/dead
                if (!IsBotPlayer(f, mario) || !mario->IsValid(f) || mario->IsDead || mario->IsRespawning) {
                    continue;
                }

                // Skip if player is in states where input should be ignored
                if (mario->IsInKnockback || mario->TauntFrames > 0 || f.Exists(mario->CurrentPipe)) {
                    ClearBotInput(f, mario->PlayerRef);
                    continue;
                }

                // Calculate bot input
                Input botInput = CalculateBotInput(f, entity, mario, transform, physicsObject, targetPosition);
                
                // Set the input for this bot player
                f.SetPlayerInput(mario->PlayerRef, botInput);
            }
        }

        /// <summary>
        /// Determines if a MarioPlayer is controlled by a bot.
        /// This can be customized based on your game's bot identification logic.
        /// 
        /// Options for bot identification:
        /// 1. Add an IsBot field to PlayerData.qtn and check playerData->IsBot
        /// 2. Check if player is disconnected (mario->Disconnected)
        /// 3. Maintain a global list of bot PlayerRefs
        /// 4. Check if player is not verified/local (f.IsPlayerVerifiedOrLocal)
        /// </summary>
        private bool IsBotPlayer(Frame f, MarioPlayer* mario) {
            // Option 1: Check if player is marked as bot in PlayerData
            var playerData = QuantumUtils.GetPlayerData(f, mario->PlayerRef);
            if (playerData != null && playerData->IsBot) {
                return true;
            }

            // Check if player is disconnected (no human input)
            // This treats disconnected players as bots
            if (mario->Disconnected) {
                return true;
            }

            // Check if player is not verified (AI-controlled)
            if (!f.IsPlayerVerifiedOrLocal(mario->PlayerRef)) {
                return true;
            }

            // Add custom bot player reference list in globals
            if (f.Global->BotPlayers.IsSet((int)mario->PlayerRef)) {
                return true;
            }

            // Default: no bots
            return false;
        }

        /// <summary>
        /// Clears input for a bot player (sets all buttons to false).
        /// </summary>
        private void ClearBotInput(Frame f, PlayerRef playerRef) {
            f.SetPlayerInput(playerRef, default);
        }

        /// <summary>
        /// Calculates the appropriate input for a bot player based on game state.
        /// </summary>
        private Input CalculateBotInput(Frame f, EntityRef entity, MarioPlayer* mario, Transform2D* transform, 
                                       PhysicsObject* physicsObject, FPVector2? targetPosition) {
            Input input = default;
            
            // If no target (no BigStar), move toward center of stage or patrol
            if (!targetPosition.HasValue) {
                var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
                if (stage != null) {
                    targetPosition = (stage.StageWorldMin + stage.StageWorldMax) * FP.FromString("0.5");
                } else {
                    // Fallback: stay in place
                    return input;
                }
            }

            FPVector2 myPosition = transform->Position;
            FPVector2 target = targetPosition.Value;
            FPVector2 toTarget = target - myPosition;
            FP distanceToTarget = toTarget.Magnitude;

            // Determine horizontal direction
            bool shouldMoveRight = toTarget.X > 0;
            bool shouldMoveLeft = toTarget.X < 0;

            // Basic movement toward target
            if (distanceToTarget > FpZeroPointOne) {
                input.Right = shouldMoveRight;
                input.Left = shouldMoveLeft;
            }

            // Sprint when moving toward distant targets
            bool shouldSprint = distanceToTarget > StarChaseDistance && 
                               (shouldMoveRight || shouldMoveLeft) &&
                               !mario->IsCrouching &&
                               !mario->IsGroundpounding;
            input.Sprint = shouldSprint;

            // Jump logic
            bool shouldJump = ShouldJump(f, entity, mario, transform, physicsObject, target, shouldMoveRight, shouldMoveLeft);
            input.Jump = shouldJump;

            // Vertical movement (up/down for swimming or climbing)
            if (physicsObject->IsUnderwater) {
                if (target.Y > myPosition.Y + FpOne) {
                    input.Up = true;
                } else if (target.Y < myPosition.Y - FpOne) {
                    input.Down = true;
                }
            }

            // Power-up actions based on current powerup state
            HandlePowerupActions(f, entity, mario, ref input, target, distanceToTarget);

            // Groundpound for aerial attacks or fast descent
            if (mario->JumpState >= JumpState.DoubleJump && 
                target.Y < myPosition.Y - FpThree &&
                !physicsObject->IsTouchingGround) {
                input.Down = true;
                input.Jump = true; // Trigger groundpound
            }

            return input;
        }

        /// <summary>
        /// Determines if the bot should jump based on terrain and obstacles.
        /// </summary>
        private bool ShouldJump(Frame f, EntityRef entity, MarioPlayer* mario, Transform2D* transform, 
                                PhysicsObject* physicsObject, FPVector2 target, 
                                bool movingRight, bool movingLeft) {
            // Don't jump if already in the air with jumps available (unless needed)
            if (!physicsObject->IsTouchingGround) {
                // Allow jump if mid-air and has jumps remaining for double/triple jump
                bool hasJumpRemaining = mario->JumpState < JumpState.TripleJump && mario->CantJumpTimer == 0;
                
                // Jump if target is above and we need extra height
                if (hasJumpRemaining && target.Y > transform->Position.Y + FpTwo) {
                    return true;
                }
                
                // Walljump if sliding on wall
                if (mario->IsWallsliding && mario->WalljumpFrames == 0) {
                    return true;
                }
                
                return false;
            }

            // Check if we need to jump over a gap ahead
            if (ShouldJumpOverGap(f, transform, physicsObject, movingRight, movingLeft)) {
                return true;
            }

            // Check if we need to jump over a wall/obstacle
            if (ShouldJumpOverWall(f, transform, physicsObject, movingRight, movingLeft)) {
                return true;
            }

            // Jump if target is significantly above current position
            if (target.Y > transform->Position.Y + FpOnePointFive) {
                return true;
            }

            // Random jump buffer for variety (deterministic based on frame number)
            if (mario->JumpBufferFrames > 0) {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if there's a gap ahead that requires jumping.
        /// </summary>
        private bool ShouldJumpOverGap(Frame f, Transform2D* transform, PhysicsObject* physicsObject, 
                                       bool movingRight, bool movingLeft) {
            if (!movingRight && !movingLeft) {
                return false;
            }

            FP direction = movingRight ? FP._1 : -FP._1;
            FPVector2 checkPosition = transform->Position;
            checkPosition.X += direction * GapCheckDistance;

            // Cast a ray downward to check for ground
            FPVector2 rayStart = checkPosition;
            rayStart.Y += FpZeroPointFive; // Start slightly above position
            FPVector2 rayEnd = checkPosition;
            rayEnd.Y -= FpFive; // Check down 5 units

            // Use Quantum's Physics2D to raycast
            var hit = f.Physics2D.Linecast(rayStart, rayEnd, f.Context.ExcludeEntityAndPlayerMask);
            
            // If no hit, there's a gap
            if (!hit.HasValue) {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if there's a wall ahead that requires jumping.
        /// </summary>
        private bool ShouldJumpOverWall(Frame f, Transform2D* transform, PhysicsObject* physicsObject, 
                                        bool movingRight, bool movingLeft) {
            // Check if we're already touching a wall
            if (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall) {
                return true;
            }

            if (!movingRight && !movingLeft) {
                return false;
            }

            // Check if there's a wall ahead by raycasting
            FP direction = movingRight ? FP._1 : -FP._1;
            FPVector2 checkPosition = transform->Position;
            checkPosition.X += direction * WallCheckDistance;

            // Cast a ray horizontally to check for walls
            FPVector2 rayStart = checkPosition;
            rayStart.Y += FpZeroPointFive; // Check at chest height
            FPVector2 rayEnd = checkPosition;
            rayEnd.X += direction * FpZeroPointFive; // Short check ahead

            // Use Quantum's Physics2D to raycast
            var hit = f.Physics2D.Linecast(rayStart, rayEnd, f.Context.ExcludeEntityAndPlayerMask);
            
            // If hit, there's a wall
            if (hit.HasValue) {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Handles power-up specific actions (fireball, propeller, etc.).
        /// </summary>
        private void HandlePowerupActions(Frame f, EntityRef entity, MarioPlayer* mario, ref Input input, 
                                          FPVector2 target, FP distanceToTarget) {
            var transform = f.Unsafe.GetPointer<Transform2D>(entity);
            FPVector2 myPosition = transform->Position;

            switch (mario->CurrentPowerupState) {
                case PowerupState.FireFlower:
                case PowerupState.IceFlower:
                    // Use fireball/ice action when target is in range and in front
                    if (distanceToTarget < FpFive && distanceToTarget > FpOne) {
                        bool targetInFront = (target.X > myPosition.X) == mario->FacingRight;
                        if (targetInFront) {
                            input.FireballPowerupAction = true;
                        }
                    }
                    break;

                case PowerupState.PropellerMushroom:
                    // Use propeller to fly toward target if it's above
                    if (target.Y > myPosition.Y + FpThree) {
                        input.PropellerPowerupAction = true;
                        input.Jump = true;
                    }
                    break;

                case PowerupState.HammerSuit:
                    // Use hammer action when target is in range
                    if (distanceToTarget < FpFour) {
                        input.PowerupAction = true;
                    }
                    break;

                case PowerupState.BlueShell:
                    // Groundpound with shell for attack
                    if (distanceToTarget < FpThree && !mario->IsGroundpounding) {
                        input.Down = true;
                        input.Jump = true;
                    }
                    break;
            }
        }
    }
}
