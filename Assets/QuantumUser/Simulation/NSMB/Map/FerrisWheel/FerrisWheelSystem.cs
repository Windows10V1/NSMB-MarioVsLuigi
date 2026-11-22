using Photon.Deterministic;

namespace Quantum {
    public unsafe class FerrisWheelSystem : SystemMainThreadEntityFilter<FerrisWheel, FerrisWheelSystem.Filter> {

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public FerrisWheel* Ferris;
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var transform = filter.Transform;
            var ferrisController = filter.Ferris;

            var platforms = f.ResolveList(ferrisController->Platforms);

            //rotate
            ferrisController->Rotation += ferrisController->BaseSpeed;
            if (ferrisController->Rotation > FP.Pi) {
                ferrisController->Rotation -= FP.Pi*2;
            } else if (ferrisController->Rotation < -FP.Pi) {
                ferrisController->Rotation += FP.Pi*2;
            }

            FP PlacementOffset = (FP.Pi*2)/platforms.Count;
            for (int i = 0; i < platforms.Count; i++) {
                var entity = platforms[i];
                var rot = ferrisController->Rotation + (PlacementOffset * i);
                var platTransform = f.Unsafe.GetPointer<Transform2D>(entity);
                var movingPlatform = f.Unsafe.GetPointer<MovingPlatform>(entity);

                //move platform
                var PreviousPosition = platTransform->Position;
                platTransform->Position = transform->Position + new FPVector2(FPMath.Cos(rot), FPMath.Sin(rot)) * ferrisController->DistanceFromCenter;
                movingPlatform->Velocity = ((platTransform->Position - PreviousPosition) * f.UpdateRate);
                //hacky fix
                if (FPMath.Abs(movingPlatform->Velocity.Y) < Constants.PhysicsSkin) //0.005
                    movingPlatform->Velocity.Y = FPMath.RoundToInt(movingPlatform->Velocity.Y) * Constants.PhysicsSkin;
            }
        }
    }
}