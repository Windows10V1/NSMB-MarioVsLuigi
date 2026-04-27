using Quantum;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NSMB.Entities.Player {
    [Serializable]
    public class PowerupVisuals {

        public PowerupState State;
        public List<GameObject> AdditionalGameObjects;

        public GameObject BaseModel;
        public Vector3 ModelScale = Vector3.one;

        public Avatar AnimationAvatar;
        public RuntimeAnimatorController AnimatorOverrides;

        public void Enable(MarioPlayerAnimator marioAnimator) {
            foreach (var gameObject in AdditionalGameObjects) {
                gameObject.SetActive(true);
            }
            BaseModel.SetActive(true);

            if (AnimationAvatar != marioAnimator.Animator.avatar) {
                // Preserve Animations
                int[] layers = { 0, 1, 3 };
                AnimatorStateInfo[] layerInfo = new AnimatorStateInfo[marioAnimator.Animator.layerCount];
                foreach (int i in layers) {
                    layerInfo[i] = marioAnimator.Animator.GetCurrentAnimatorStateInfo(i);
                }

                marioAnimator.Animator.avatar = AnimationAvatar;
                marioAnimator.Animator.runtimeAnimatorController = AnimatorOverrides;

                // Push back state 
                marioAnimator.Animator.Rebind();

                foreach (int i in layers) {
                    marioAnimator.Animator.Play(layerInfo[i].fullPathHash, i, layerInfo[i].normalizedTime);
                }
            }
        }

        public void Disable() {
            foreach (var gameObject in AdditionalGameObjects) {
                gameObject.SetActive(false);
            }
            BaseModel.SetActive(false);
        }
    }
}