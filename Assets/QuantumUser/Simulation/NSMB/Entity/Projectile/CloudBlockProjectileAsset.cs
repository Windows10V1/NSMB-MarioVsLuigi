using Photon.Deterministic;
using Quantum;
using System;
using System.Collections.Generic;

public class CloudBlockProjectileAsset : AssetObject, ISoundOverrideProvider {
    public ushort LifetimeFrames = 0;
    public ushort RestoreCooldownFrames = 0;
    public byte MaxCloudBlocks = 0;
    public byte MaxInstantCloudBlocks = 0;
    public byte SummonDelayFrames = 0;

    public FPVector2 SpawnOffset = new(0, FF(0f));
    public FP SummonBounceVelocity = FF(0f);
    public FP GroundpoundLaunchVelocity = FF(0f);

    public byte SummonInactiveFrames = 0;
    public byte DestroyInactiveFrames = 0;
    public byte DestroyAnimationFrames = 0;

    public SoundEffect SpawnSound = SoundEffect.Powerup_CloudFlower_Spawn;
    public SoundEffect GroundpoundBounceSound = SoundEffect.Powerup_CloudFlower_Bounce;

    public SoundEffectOverride[] SfxOverrides;

    [NonSerialized] private Dictionary<SoundEffect, SoundEffectOverride> overridesDict;

    public override void Loaded(IResourceManager resourceManager, Native.Allocator allocator) {
        overridesDict = new();
        if (SfxOverrides == null) {
            return;
        }

        foreach (var @override in SfxOverrides) {
            overridesDict[@override.SoundEffect] = @override;
        }
    }

    public SoundEffectOverride GetOverride(SoundEffect sfx) {
        if (overridesDict != null && overridesDict.TryGetValue(sfx, out var result)) {
            return result;
        }

        return null;
    }

    private static FP FF(float value) {
        return FP.FromFloat_UNSAFE(value);
    }
}
