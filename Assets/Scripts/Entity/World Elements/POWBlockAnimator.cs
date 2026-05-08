using NSMB.Utilities.Extensions;
using Quantum;
using UnityEngine;

namespace NSMB.Entities.World {
    public unsafe class POWBlockAnimator : QuantumEntityViewComponent {

        //---Serialized Variables
        [SerializeField] private SpriteRenderer sRenderer;
        [SerializeField] private Sprite activeSprite, inactiveSprite;

        //---Private Variables
        private QuantumEntityView holderView;

        public void OnValidate() {
            this.SetIfNull(ref sRenderer);
        }

        public void Start() {
            QuantumEvent.Subscribe<EventMarioPlayerPickedUpObject>(this, OnPOWBlockPickedUp, onlyIfEntityViewBound: true);
        }

        public override void OnUpdateView() {
            Frame f = PredictedFrame;
            if (!f.Unsafe.TryGetPointer(EntityRef, out POWBlock* powBlock)) {
                return;
            }

            // Update sprite based on held state
            sRenderer.sprite = f.Exists(powBlock->Holder) ? activeSprite : inactiveSprite;

            // If being held, follow the holder
            if (f.Exists(powBlock->Holder)) {
                if (!holderView) {
                    holderView = EntityView.EntityViewUpdater.GetView(powBlock->Holder);
                }

                if (holderView) {
                    transform.position = holderView.transform.position + (Vector3.up * 0.75f);
                }
            }
        }

        private void OnPOWBlockPickedUp(EventMarioPlayerPickedUpObject e) {
            if (e.OtherEntity != EntityRef) {
                return;
            }

            holderView = EntityView.EntityViewUpdater.GetView(e.Entity);
        }
    }
}
