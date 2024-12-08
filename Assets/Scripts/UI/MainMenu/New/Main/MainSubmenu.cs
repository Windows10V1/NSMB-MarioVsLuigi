using System.Collections;
using UnityEngine;

namespace NSMB.UI.MainMenu.Submenus {
    public class MainSubmenu : MainMenuSubmenu {

        //---Private Variables
        private bool wasOptionsOpen;
        private Coroutine quitCoroutine;

        public void Update() {
            wasOptionsOpen = GlobalController.Instance.optionsManager.IsActive();
        }

        public void OpenOptions() {
            if (!wasOptionsOpen) {
                GlobalController.Instance.optionsManager.OpenMenu();
                canvas.PlayConfirmSound();
            }
        }

        public void QuitGame() {
            if (quitCoroutine == null) {
                quitCoroutine = canvas.StartCoroutine(QuitCorotuine());
            }
        }

        private IEnumerator QuitCorotuine() {
            AudioClip clip = SoundEffect.UI_Quit.GetClip();
            canvas.PlaySound(SoundEffect.UI_Quit);
            yield return new WaitForSeconds(clip.length);

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}

