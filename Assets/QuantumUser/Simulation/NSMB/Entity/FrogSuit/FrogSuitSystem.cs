namespace Quantum {
    public unsafe class FrogSuitSystem : SystemMainThreadEntityFilter<CoinItem, FrogSuitSystem.Filter> {

        public struct Filter {
            public EntityRef Entity;
            public CoinItem* CoinItem;
            public PhysicsObject* PhysicsObject;
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            if (filter.CoinItem->SpawnAnimationFrames > 0) {
                return;
            }

            var physicsObject = filter.PhysicsObject;
            if (!physicsObject->WasTouchingGround && physicsObject->IsTouchingGround
                && f.FindAsset(filter.CoinItem->Scriptable) is PowerupAsset asset
                && asset.State == PowerupState.FrogSuit) {
                f.Events.FrogSuitBounce(filter.Entity);
            }
        }
    }
}