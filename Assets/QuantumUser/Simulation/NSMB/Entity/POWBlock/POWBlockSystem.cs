using Photon.Deterministic;

namespace Quantum {
  public unsafe class PowBlockSystem : SystemMainThreadEntityFilter<PowBlock, PowBlockSystem.Filter> {
    private const byte POW_MAX_CHARGES = 3;
    private const byte POW_STARS_TO_DROP = 2;

    public struct Filter {
      public EntityRef Entity;  
      public PowBlock* PowBlock;
      public PhysicsObject* PhysicsObject;
      public Transform2D* Transform;
      public Holdable* Holdable;
    }

    public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
      var physicsObject = filter.PhysicsObject;
      if (!physicsObject->WasTouchingGround && physicsObject->IsTouchingGround) {
        // POW has JUST hit the ground.
        var holdable = filter.Holdable;
        if (holdable->PreviousHolder != EntityRef.None) {
          // POW had a previous holder- was thrown
          Explode(f, ref filter);

          // Reset the holder to indicate we arent thrown anymore
          holdable->PreviousHolder = EntityRef.None;
        }
      }
    }

    public void Explode(Frame f, ref Filter filter) {
      var pow = filter.PowBlock;
      var activator = filter.Holdable->PreviousHolder;

      // Trigger screen shake (same as Mega Mushroom footstep)
      f.Signals.OnMarioPlayerMegaMushroomFootstep();

      // Spawn explosion particle effect at POW position
      // Unity will handle spawning MegaGroundpoundDust prefab based on this event
      f.Events.PlayKnockbackEffect(filter.Entity, EntityRef.None, KnockbackStrength.Groundpound, filter.Transform->Position);

      // Hit all players except activator
      HitAllPlayers(f, ref filter, activator);

      // Update sprite state based on remaining charges
      pow->SpriteState = (byte)(POW_MAX_CHARGES - pow->RemainingCharges);

      // Unity event - Unity will play Powerup_MegaMushroom_Groundpound sound globally
      f.Events.PowBlockExploded(filter.Entity);

      if (--pow->RemainingCharges == 0) {
        // Out of charges. Destroy
        f.Destroy(filter.Entity);
      }
    }

    private void HitAllPlayers(Frame f, ref Filter filter, EntityRef activator) {
      var playerFilter = f.Filter<MarioPlayer, Transform2D, PhysicsObject>();
      while (playerFilter.NextUnsafe(out var playerEntity, out var mario, out var playerTransform, out var playerPhysics)) {
        // Skip the activator
        if (playerEntity == activator) {
          continue;
        }

        // Skip dead players
        if (mario->IsDead || mario->IsRespawning) {
          continue;
        }

        // Apply knockback with star dropping
        ApplyPowKnockback(f, playerEntity, mario, playerPhysics, filter.Entity);
      }
    }

    private void ApplyPowKnockback(Frame f, EntityRef playerEntity, MarioPlayer* mario, PhysicsObject* playerPhysics, EntityRef powEntity) {
      // Determine knockback direction based on momentum
      bool knockbackFromRight;
      FP velocityX = playerPhysics->Velocity.X;

      if (FPMath.Abs(velocityX) > FP._0_10) {
        // Has momentum - knockback in direction of movement
        knockbackFromRight = velocityX < 0;
      } else {
        // No momentum - knockback opposite to facing direction
        knockbackFromRight = !mario->FacingRight;
      }

      // Apply normal knockback with 2 stars dropped
      mario->DoKnockback(f, playerEntity, knockbackFromRight, POW_STARS_TO_DROP, KnockbackStrength.Normal, powEntity);

      // Drop stars if in Star Chasers gamemode
      if (f.FindAsset(f.Global->Rules.Gamemode) is StarChasersGamemode && mario->GamemodeData.StarChasers->Stars > 0) {
        int starsToDrop = (int) FPMath.Min(POW_STARS_TO_DROP, mario->GamemodeData.StarChasers->Stars);
        f.Signals.OnMarioPlayerDropObjective(playerEntity, starsToDrop, powEntity);
      }
    }
  }
}