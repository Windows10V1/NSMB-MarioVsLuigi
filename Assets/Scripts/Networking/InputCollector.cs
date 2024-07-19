using Photon.Deterministic;
using Quantum;
using UnityEngine;
using UnityEngine.InputSystem;
using Input = Quantum.Input;

public class InputCollector : MonoBehaviour {

    //---Serailized
    [SerializeField] private InputActionReference movementAction, jumpAction, sprintAction, powerupAction, reservePowerupAction;

    public void Start() {
        QuantumCallback.Subscribe<CallbackPollInput>(this, PollInput);
        reservePowerupAction.action.performed += OnPowerupAction;
    }

    public void OnDestroy() {
        reservePowerupAction.action.performed -= OnPowerupAction;
    }

    public void OnPowerupAction(InputAction.CallbackContext context) {
        QuantumRunner.DefaultGame.SendCommand(new CommandSpawnReserveItem());
    }

    public void PollInput(CallbackPollInput callback) {

        jumpAction.action.actionMap.Enable();

        Vector2 stick = movementAction.action.ReadValue<Vector2>();
        Vector2 normalizedJoystick = stick.normalized;
        //TODO: changeable deadzone?
        bool up = Vector2.Dot(normalizedJoystick, Vector2.up) > 0.6f;
        bool down = Vector2.Dot(normalizedJoystick, Vector2.down) > 0.6f;
        bool left = Vector2.Dot(normalizedJoystick, Vector2.left) > 0.4f;
        bool right = Vector2.Dot(normalizedJoystick, Vector2.right) > 0.4f;

        Input i = new() {
            Up = up,
            Down = down,
            Left = left,
            Right = right,
            Jump = jumpAction.action.ReadValue<float>() > 0.5f,
            Sprint = sprintAction.action.ReadValue<float>() > 0.5f ^ Settings.Instance.controlsAutoSprint,
            PowerupAction = powerupAction.action.ReadValue<float>() > 0.5f,
        };

        callback.SetInput(i, DeterministicInputFlags.Repeatable);
    }
}
