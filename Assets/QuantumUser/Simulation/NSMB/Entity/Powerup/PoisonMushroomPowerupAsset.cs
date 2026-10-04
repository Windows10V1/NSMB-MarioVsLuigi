using Quantum;

public unsafe class PoisonMushroomPowerupAsset : PowerupAsset {

    public override int CountPlayersWithReserve(Frame f) {
        return 0;
    }

    public override int CountPlayersWithState(Frame f) {
        return 0;
    }

    public override PowerupReserveResult Collect(Frame f, EntityRef entity) {
        if (f.Unsafe.TryGetPointer(entity, out MarioPlayer* mario)) {
            bool damageable = mario->Lives > 1 && !mario->IsStarmanInvincible && mario->MegaMushroomFrames <= 0;
            if (damageable) {
                if (f.Global->Rules.IsLivesEnabled) {
                    mario->Death(f, entity, false, true, EntityRef.None);
                } else {
                    mario->Powerdown(f, entity, false, EntityRef.None);
                }
            } else {
                f.Events.EnemyKicked(entity, false);
            }
        }

        return PowerupReserveResult.CollectNewIgnoreOld;
    }
}
