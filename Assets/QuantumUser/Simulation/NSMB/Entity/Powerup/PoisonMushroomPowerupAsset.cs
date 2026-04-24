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
            mario->Powerdown(f, entity, false, entity);
        }

        return PowerupReserveResult.CollectNewIgnoreOld;
    }
}
