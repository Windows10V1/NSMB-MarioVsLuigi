using UnityEngine;

public class LoadingPower : MonoBehaviour {

    public int marioX = -296, peachX = 296, minX = -410;
    public Vector3 movementSpeed;
    public MarioLoader mario;
    private Animator animator;
    private RectTransform rect;
    private bool goomba, goombaHit;
    private float goombaTimer;

    public void Start() {
        animator = GetComponent<Animator>();
        rect = GetComponent<RectTransform>();
    }

    public void Update() {
        animator.SetBool("goomba", goomba);
        if ((goombaTimer -= Time.deltaTime) < 0)
            rect.localPosition += movementSpeed * Time.deltaTime;

        if (rect.localPosition.x <= marioX) {
            if (goomba) {
                if (!goombaHit) {
                    mario.scale--;
                    goombaHit = true;
                    mario.scaleTimer = 0f;
                    goombaTimer = 0.5f;
                }
                if (rect.localPosition.x <= minX)
                    TeleportToBeginning();
            } else {
                mario.scale++;
                mario.scaleTimer = 0f;
                TeleportToBeginning();
            }
        }
    }

    private void TeleportToBeginning() {
        goombaHit = false;
        goomba = mario.scale > 0 && (mario.scale >= 2 || Random.value < 0.5f);
        rect.localPosition = new Vector2(peachX, rect.localPosition.y);
    }
}
