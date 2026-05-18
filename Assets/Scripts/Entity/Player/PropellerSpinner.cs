using Quantum;
using UnityEngine;

namespace NSMB.Entities.Player {
    public unsafe class PropellerSpinner : QuantumEntityViewComponent {

        //---Serialized Variables
        [SerializeField] private Vector3 slowRotationSpeed = new(0, 0, -300), fastRotationSpeed = new(0, 0, -2500);
        [SerializeField] private float acceleration = 1200;

        //---Private Variables
        private Vector3 currentRotationSpeed;

        public override void OnUpdateView() {
            Frame f = PredictedFrame;
            Freezable* freezable = f.Unsafe.GetPointer<Freezable>(EntityRef);

            if (freezable->IsFrozen(f)) {
                return;
            }

            MarioPlayer* mario = PredictedFrame.Unsafe.GetPointer<MarioPlayer>(EntityRef);
            if (mario->UsedPropellerThisJump) {
                currentRotationSpeed = fastRotationSpeed;
            } else {
                currentRotationSpeed = Vector3.MoveTowards(currentRotationSpeed, slowRotationSpeed, acceleration * Time.deltaTime);
            }

            transform.rotation *= Quaternion.Euler(currentRotationSpeed * Time.deltaTime);
        }
    }
}
