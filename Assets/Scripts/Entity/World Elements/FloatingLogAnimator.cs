using Quantum;
using UnityEngine;

namespace NSMB.Entities.World {
    public unsafe class FloatingLogAnimator : QuantumEntityViewComponent {

        //---Serialized Variables
        [SerializeField] private Transform logModel;

        public override void OnUpdateView() {
            Frame f = PredictedFrame;
            Frame fp = PredictedPreviousFrame;

            if (!f.Unsafe.TryGetPointer(EntityRef, out FloatingLog* log)
                || !fp.Unsafe.TryGetPointer(EntityRef, out FloatingLog* logPrev)) {
                return;
            }

            float angle = Mathf.LerpAngle(logPrev->SpinAngle.AsFloat, log->SpinAngle.AsFloat, Game.InterpolationFactor);
            if (logModel != null) {
                logModel.localRotation = Quaternion.Euler(-angle, 0f, 0f);
            }
        }
    }
}
