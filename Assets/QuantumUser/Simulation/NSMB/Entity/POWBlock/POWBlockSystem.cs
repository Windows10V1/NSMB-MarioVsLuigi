using Photon.Deterministic;

namespace Quantum {
    public unsafe class POWBlockSystem : SystemMainThreadEntityFilter<POWBlock, POWBlockSystem.Filter> {

        public struct Filter {
            public EntityRef Entity;
            public POWBlock* POWBlock;
            public Transform2D* Transform;
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            // TODO: Implement POW Block logic
        }
    }
}
