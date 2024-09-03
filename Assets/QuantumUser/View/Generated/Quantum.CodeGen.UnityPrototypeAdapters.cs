// <auto-generated>
// This code was auto-generated by a tool, every time
// the tool executes this code will be reset.
//
// If you need to extend the classes generated to add
// fields or methods to them, please create partial
// declarations in another file.
// </auto-generated>
#pragma warning disable 0109
#pragma warning disable 1591


namespace Quantum.Prototypes.Unity {
  using Photon.Deterministic;
  using Quantum;
  using Quantum.Core;
  using Quantum.Collections;
  using Quantum.Inspector;
  using Quantum.Physics2D;
  using Quantum.Physics3D;
  using Byte = System.Byte;
  using SByte = System.SByte;
  using Int16 = System.Int16;
  using UInt16 = System.UInt16;
  using Int32 = System.Int32;
  using UInt32 = System.UInt32;
  using Int64 = System.Int64;
  using UInt64 = System.UInt64;
  using Boolean = System.Boolean;
  using String = System.String;
  using Object = System.Object;
  using FlagsAttribute = System.FlagsAttribute;
  using SerializableAttribute = System.SerializableAttribute;
  using MethodImplAttribute = System.Runtime.CompilerServices.MethodImplAttribute;
  using MethodImplOptions = System.Runtime.CompilerServices.MethodImplOptions;
  using FieldOffsetAttribute = System.Runtime.InteropServices.FieldOffsetAttribute;
  using StructLayoutAttribute = System.Runtime.InteropServices.StructLayoutAttribute;
  using LayoutKind = System.Runtime.InteropServices.LayoutKind;
  #if QUANTUM_UNITY //;
  using TooltipAttribute = UnityEngine.TooltipAttribute;
  using HeaderAttribute = UnityEngine.HeaderAttribute;
  using SpaceAttribute = UnityEngine.SpaceAttribute;
  using RangeAttribute = UnityEngine.RangeAttribute;
  using HideInInspectorAttribute = UnityEngine.HideInInspector;
  using PreserveAttribute = UnityEngine.Scripting.PreserveAttribute;
  using FormerlySerializedAsAttribute = UnityEngine.Serialization.FormerlySerializedAsAttribute;
  using MovedFromAttribute = UnityEngine.Scripting.APIUpdating.MovedFromAttribute;
  using CreateAssetMenu = UnityEngine.CreateAssetMenuAttribute;
  using RuntimeInitializeOnLoadMethodAttribute = UnityEngine.RuntimeInitializeOnLoadMethodAttribute;
  #endif //;
  
  [System.SerializableAttribute()]
  public unsafe partial class EnterablePipePrototype : Quantum.QuantumUnityPrototypeAdapter<Quantum.Prototypes.EnterablePipePrototype> {
    public Quantum.QuantumEntityPrototype OtherPipe;
    public QBoolean IsEnterable;
    public QBoolean IsCeilingPipe;
    public QBoolean IsMiniOnly;
    partial void ConvertUser(Quantum.QuantumEntityPrototypeConverter converter, ref Quantum.Prototypes.EnterablePipePrototype prototype);
    public override Quantum.Prototypes.EnterablePipePrototype Convert(Quantum.QuantumEntityPrototypeConverter converter) {
      var result = new Quantum.Prototypes.EnterablePipePrototype();
      converter.Convert(this.OtherPipe, out result.OtherPipe);
      converter.Convert(this.IsEnterable, out result.IsEnterable);
      converter.Convert(this.IsCeilingPipe, out result.IsCeilingPipe);
      converter.Convert(this.IsMiniOnly, out result.IsMiniOnly);
      ConvertUser(converter, ref result);
      return result;
    }
  }
  [System.SerializableAttribute()]
  public unsafe partial class MarioPlayerPrototype : Quantum.QuantumUnityPrototypeAdapter<Quantum.Prototypes.MarioPlayerPrototype> {
    public AssetRef<MarioPlayerPhysicsInfo> PhysicsAsset;
    public AssetRef<CharacterAsset> CharacterAsset;
    public PlayerRef PlayerRef;
    public Byte SpawnpointIndex;
    public Byte Team;
    public PowerupState CurrentPowerupState;
    public PowerupState PreviousPowerupState;
    public AssetRef<PowerupAsset> ReserveItem;
    public Byte Stars;
    public Byte Coins;
    public Byte Lives;
    public QBoolean Disconnected;
    public QBoolean IsDead;
    public QBoolean FireDeath;
    public QBoolean IsRespawning;
    public Byte DeathAnimationFrames;
    public Byte PreRespawnFrames;
    public Byte RespawnFrames;
    public Byte NoLivesStarDirection;
    public QBoolean FacingRight;
    public QBoolean IsSkidding;
    public QBoolean IsTurnaround;
    public Byte FastTurnaroundFrames;
    public Byte SlowTurnaroundFrames;
    public JumpState JumpState;
    public JumpState PreviousJumpState;
    public Byte JumpLandingFrames;
    public Byte JumpBufferFrames;
    public Byte CoyoteTimeFrames;
    public Int32 LandedFrame;
    public QBoolean WasTouchingGroundLastFrame;
    public QBoolean DoEntityBounce;
    public QBoolean WallslideLeft;
    public QBoolean WallslideRight;
    public Byte WallslideEndFrames;
    public Byte WalljumpFrames;
    public QBoolean IsGroundpounding;
    public QBoolean IsGroundpoundActive;
    public Byte GroundpoundStartFrames;
    public Byte GroundpoundCooldownFrames;
    public Byte GroundpoundStandFrames;
    public Byte WaterColliderCount;
    public QBoolean SwimExitForceJump;
    public QBoolean IsInKnockback;
    public QBoolean IsInWeakKnockback;
    public QBoolean KnockbackWasOriginallyFacingRight;
    public Int32 KnockbackTick;
    public Byte DamageInvincibilityFrames;
    public QBoolean IsCrouching;
    public QBoolean IsSliding;
    public QBoolean IsSpinnerFlying;
    public QBoolean IsDrilling;
    public Byte Combo;
    public UInt16 InvincibilityFrames;
    public Byte MegaMushroomStartFrames;
    public UInt16 MegaMushroomFrames;
    public Byte MegaMushroomEndFrames;
    public QBoolean MegaMushroomStationaryEnd;
    public Byte ProjectileDelayFrames;
    public Byte ProjectileVolleyFrames;
    public Byte CurrentProjectiles;
    public Byte CurrentVolley;
    public QBoolean IsInShell;
    public Byte ShellSlowdownFrames;
    public QBoolean IsPropellerFlying;
    public Byte PropellerLaunchFrames;
    public Byte PropellerSpinFrames;
    public QBoolean UsedPropellerThisJump;
    public Byte PropellerDrillCooldown;
    public Quantum.QuantumEntityPrototype HeldEntity;
    public Quantum.QuantumEntityPrototype CurrentPipe;
    public FPVector2 PipeDirection;
    public QBoolean PipeEntering;
    public Byte PipeFrames;
    public Byte PipeCooldownFrames;
    partial void ConvertUser(Quantum.QuantumEntityPrototypeConverter converter, ref Quantum.Prototypes.MarioPlayerPrototype prototype);
    public override Quantum.Prototypes.MarioPlayerPrototype Convert(Quantum.QuantumEntityPrototypeConverter converter) {
      var result = new Quantum.Prototypes.MarioPlayerPrototype();
      converter.Convert(this.PhysicsAsset, out result.PhysicsAsset);
      converter.Convert(this.CharacterAsset, out result.CharacterAsset);
      converter.Convert(this.PlayerRef, out result.PlayerRef);
      converter.Convert(this.SpawnpointIndex, out result.SpawnpointIndex);
      converter.Convert(this.Team, out result.Team);
      converter.Convert(this.CurrentPowerupState, out result.CurrentPowerupState);
      converter.Convert(this.PreviousPowerupState, out result.PreviousPowerupState);
      converter.Convert(this.ReserveItem, out result.ReserveItem);
      converter.Convert(this.Stars, out result.Stars);
      converter.Convert(this.Coins, out result.Coins);
      converter.Convert(this.Lives, out result.Lives);
      converter.Convert(this.Disconnected, out result.Disconnected);
      converter.Convert(this.IsDead, out result.IsDead);
      converter.Convert(this.FireDeath, out result.FireDeath);
      converter.Convert(this.IsRespawning, out result.IsRespawning);
      converter.Convert(this.DeathAnimationFrames, out result.DeathAnimationFrames);
      converter.Convert(this.PreRespawnFrames, out result.PreRespawnFrames);
      converter.Convert(this.RespawnFrames, out result.RespawnFrames);
      converter.Convert(this.NoLivesStarDirection, out result.NoLivesStarDirection);
      converter.Convert(this.FacingRight, out result.FacingRight);
      converter.Convert(this.IsSkidding, out result.IsSkidding);
      converter.Convert(this.IsTurnaround, out result.IsTurnaround);
      converter.Convert(this.FastTurnaroundFrames, out result.FastTurnaroundFrames);
      converter.Convert(this.SlowTurnaroundFrames, out result.SlowTurnaroundFrames);
      converter.Convert(this.JumpState, out result.JumpState);
      converter.Convert(this.PreviousJumpState, out result.PreviousJumpState);
      converter.Convert(this.JumpLandingFrames, out result.JumpLandingFrames);
      converter.Convert(this.JumpBufferFrames, out result.JumpBufferFrames);
      converter.Convert(this.CoyoteTimeFrames, out result.CoyoteTimeFrames);
      converter.Convert(this.LandedFrame, out result.LandedFrame);
      converter.Convert(this.WasTouchingGroundLastFrame, out result.WasTouchingGroundLastFrame);
      converter.Convert(this.DoEntityBounce, out result.DoEntityBounce);
      converter.Convert(this.WallslideLeft, out result.WallslideLeft);
      converter.Convert(this.WallslideRight, out result.WallslideRight);
      converter.Convert(this.WallslideEndFrames, out result.WallslideEndFrames);
      converter.Convert(this.WalljumpFrames, out result.WalljumpFrames);
      converter.Convert(this.IsGroundpounding, out result.IsGroundpounding);
      converter.Convert(this.IsGroundpoundActive, out result.IsGroundpoundActive);
      converter.Convert(this.GroundpoundStartFrames, out result.GroundpoundStartFrames);
      converter.Convert(this.GroundpoundCooldownFrames, out result.GroundpoundCooldownFrames);
      converter.Convert(this.GroundpoundStandFrames, out result.GroundpoundStandFrames);
      converter.Convert(this.WaterColliderCount, out result.WaterColliderCount);
      converter.Convert(this.SwimExitForceJump, out result.SwimExitForceJump);
      converter.Convert(this.IsInKnockback, out result.IsInKnockback);
      converter.Convert(this.IsInWeakKnockback, out result.IsInWeakKnockback);
      converter.Convert(this.KnockbackWasOriginallyFacingRight, out result.KnockbackWasOriginallyFacingRight);
      converter.Convert(this.KnockbackTick, out result.KnockbackTick);
      converter.Convert(this.DamageInvincibilityFrames, out result.DamageInvincibilityFrames);
      converter.Convert(this.IsCrouching, out result.IsCrouching);
      converter.Convert(this.IsSliding, out result.IsSliding);
      converter.Convert(this.IsSpinnerFlying, out result.IsSpinnerFlying);
      converter.Convert(this.IsDrilling, out result.IsDrilling);
      converter.Convert(this.Combo, out result.Combo);
      converter.Convert(this.InvincibilityFrames, out result.InvincibilityFrames);
      converter.Convert(this.MegaMushroomStartFrames, out result.MegaMushroomStartFrames);
      converter.Convert(this.MegaMushroomFrames, out result.MegaMushroomFrames);
      converter.Convert(this.MegaMushroomEndFrames, out result.MegaMushroomEndFrames);
      converter.Convert(this.MegaMushroomStationaryEnd, out result.MegaMushroomStationaryEnd);
      converter.Convert(this.ProjectileDelayFrames, out result.ProjectileDelayFrames);
      converter.Convert(this.ProjectileVolleyFrames, out result.ProjectileVolleyFrames);
      converter.Convert(this.CurrentProjectiles, out result.CurrentProjectiles);
      converter.Convert(this.CurrentVolley, out result.CurrentVolley);
      converter.Convert(this.IsInShell, out result.IsInShell);
      converter.Convert(this.ShellSlowdownFrames, out result.ShellSlowdownFrames);
      converter.Convert(this.IsPropellerFlying, out result.IsPropellerFlying);
      converter.Convert(this.PropellerLaunchFrames, out result.PropellerLaunchFrames);
      converter.Convert(this.PropellerSpinFrames, out result.PropellerSpinFrames);
      converter.Convert(this.UsedPropellerThisJump, out result.UsedPropellerThisJump);
      converter.Convert(this.PropellerDrillCooldown, out result.PropellerDrillCooldown);
      converter.Convert(this.HeldEntity, out result.HeldEntity);
      converter.Convert(this.CurrentPipe, out result.CurrentPipe);
      converter.Convert(this.PipeDirection, out result.PipeDirection);
      converter.Convert(this.PipeEntering, out result.PipeEntering);
      converter.Convert(this.PipeFrames, out result.PipeFrames);
      converter.Convert(this.PipeCooldownFrames, out result.PipeCooldownFrames);
      ConvertUser(converter, ref result);
      return result;
    }
  }
  [System.SerializableAttribute()]
  public unsafe partial class PhysicsContactPrototype : Quantum.QuantumUnityPrototypeAdapter<Quantum.Prototypes.PhysicsContactPrototype> {
    public FPVector2 Position;
    public FPVector2 Normal;
    public FP Distance;
    public Int32 Frame;
    public Int32 TileX;
    public Int32 TileY;
    public Quantum.QuantumEntityPrototype Entity;
    partial void ConvertUser(Quantum.QuantumEntityPrototypeConverter converter, ref Quantum.Prototypes.PhysicsContactPrototype prototype);
    public override Quantum.Prototypes.PhysicsContactPrototype Convert(Quantum.QuantumEntityPrototypeConverter converter) {
      var result = new Quantum.Prototypes.PhysicsContactPrototype();
      converter.Convert(this.Position, out result.Position);
      converter.Convert(this.Normal, out result.Normal);
      converter.Convert(this.Distance, out result.Distance);
      converter.Convert(this.Frame, out result.Frame);
      converter.Convert(this.TileX, out result.TileX);
      converter.Convert(this.TileY, out result.TileY);
      converter.Convert(this.Entity, out result.Entity);
      ConvertUser(converter, ref result);
      return result;
    }
  }
  [System.SerializableAttribute()]
  public unsafe partial class PhysicsObjectPrototype : Quantum.QuantumUnityPrototypeAdapter<Quantum.Prototypes.PhysicsObjectPrototype> {
    public FPVector2 Gravity;
    public FP TerminalVelocity;
    public QBoolean IsFrozen;
    public QBoolean DisableCollision;
    partial void ConvertUser(Quantum.QuantumEntityPrototypeConverter converter, ref Quantum.Prototypes.PhysicsObjectPrototype prototype);
    public override Quantum.Prototypes.PhysicsObjectPrototype Convert(Quantum.QuantumEntityPrototypeConverter converter) {
      var result = new Quantum.Prototypes.PhysicsObjectPrototype();
      converter.Convert(this.Gravity, out result.Gravity);
      converter.Convert(this.TerminalVelocity, out result.TerminalVelocity);
      converter.Convert(this.IsFrozen, out result.IsFrozen);
      converter.Convert(this.DisableCollision, out result.DisableCollision);
      ConvertUser(converter, ref result);
      return result;
    }
  }
  [System.SerializableAttribute()]
  public unsafe partial class PiranhaPlantPrototype : Quantum.QuantumUnityPrototypeAdapter<Quantum.Prototypes.PiranhaPlantPrototype> {
    public Quantum.QuantumEntityPrototype Pipe;
    partial void ConvertUser(Quantum.QuantumEntityPrototypeConverter converter, ref Quantum.Prototypes.PiranhaPlantPrototype prototype);
    public override Quantum.Prototypes.PiranhaPlantPrototype Convert(Quantum.QuantumEntityPrototypeConverter converter) {
      var result = new Quantum.Prototypes.PiranhaPlantPrototype();
      converter.Convert(this.Pipe, out result.Pipe);
      ConvertUser(converter, ref result);
      return result;
    }
  }
}
#pragma warning restore 0109
#pragma warning restore 1591
