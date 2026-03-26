using Photon.Deterministic;
using Quantum;
using System;
using System.Collections.Generic;

public class ProjectileAsset : AssetObject, ISoundOverrideProvider {
    public ProjectileEffectType Effect;
    public bool Bounce = true;
    public FP Speed;
    public FP BounceStrength;
    public FPVector2 Gravity;
    public bool DestroyOnSecondBounce;
    public bool DestroyOnHit = true;
    public bool LockTo45Degrees = true;
    public bool InheritShooterVelocity;
    public bool HasCollision = true;
    public bool DoesntEffectBlueShell = true;
    public bool CollectCoins = false;

    public ParticleEffect DestroyParticleEffect = ParticleEffect.None;
    public SoundEffect ShootSound = SoundEffect.Powerup_Fireball_Shoot;

    // Boomerang-specific properties
    public bool IsBoomerang = false;
    public FP BoomerangReturnDelay = FP.FromString("0.5"); // Delay before return force activates
    public FP BoomerangReturnAcceleration = FP.FromString("0.5"); // How quickly force ramps up

    // Super Ball-specific properties
    public bool IsSuperBall = false; // When true, uses custom 45-degree bounce physics

    public SoundEffectOverride[] SfxOverrides;

    [NonSerialized] private Dictionary<SoundEffect, SoundEffectOverride> overridesDict;
    public override void Loaded(IResourceManager resourceManager, Native.Allocator allocator) {
        overridesDict = new();
        if (SfxOverrides != null) {
            foreach (var @override in SfxOverrides) {
                overridesDict[@override.SoundEffect] = @override;
            }
        }
    }

    public SoundEffectOverride GetOverride(SoundEffect sfx) {
        overridesDict.TryGetValue(sfx, out var result);
        return result;
    }
}

public enum ProjectileEffectType {
    Fire,
    Freeze,
    KillEnemiesAndSoftKnockbackPlayers,
}