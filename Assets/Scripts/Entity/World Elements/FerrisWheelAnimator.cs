using Quantum;
using UnityEngine;

namespace NSMB.Entities.World {
    public unsafe class FerrisWheelAnimator : QuantumEntityViewComponent {

        //---Serialized Variables
        [SerializeField] private Transform RotatingObject;

        public override void OnUpdateView() {
            Frame f = PredictedFrame;
            if (!f.Unsafe.TryGetPointer(EntityRef, out FerrisWheel* ferrisWheel)) {
                return;
            }

            float maxRotation = 2000f * Time.deltaTime;
            RotatingObject.rotation = Quaternion.Euler(0, 0, ferrisWheel->Rotation.AsFloat * Mathf.Rad2Deg);
        }
    }
}