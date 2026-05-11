using Photon.Deterministic;
using Quantum;

public class CloudBlockAsset : AssetObject, ISoundOverrideProvider {
    public int LifetimeFrames = 0;
    public int SummonAnimationFrames = 0;
    public int DestroyAnimationFrames = 0;
    public byte MaxCloudCount = 0;
    public int CooldownFrames = 0;
    public int InstantCooldownFrames = 0;
    public FP BounceVelocity = FP.FromFloat_UNSAFE(0f);
    public SoundEffect SummonSound = SoundEffect.Enemy_Generic_Freeze;
    public SoundEffectOverride[] SfxOverrides;

    public SoundEffectOverride GetOverride(SoundEffect sfx) {
        return null;
    }
}
