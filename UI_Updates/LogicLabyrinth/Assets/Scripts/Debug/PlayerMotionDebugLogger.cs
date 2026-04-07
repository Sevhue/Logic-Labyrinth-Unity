using UnityEngine;
using StarterAssets;

/// <summary>
/// Runtime diagnostics for player motion and UI state.
/// Logs only on suspicious jumps and state transitions to keep console noise manageable.
/// </summary>
public class PlayerMotionDebugLogger : MonoBehaviour
{
    [Header("Debug Thresholds")]
    [Tooltip("If the player moves more than this in one frame, print a warning.")]
    public float suspiciousFrameJumpDistance = 1.6f;
    [Tooltip("If camera-to-player offset changes more than this in one frame, print a warning.")]
    public float suspiciousCameraOffsetDelta = 0.9f;
    [Tooltip("Print a compact heartbeat every N seconds.")]
    public float heartbeatInterval = 2.0f;

    private CharacterController _cc;
    private StarterAssetsInputs _inputs;
    private Vector3 _lastPlayerPos;
    private Vector3 _lastCamPos;
    private Vector3 _lastCamOffset;
    private float _nextHeartbeatTime;

    private bool _lastPause;
    private bool _lastPuzzle;
    private bool _lastSwap;
    private CursorLockMode _lastCursorLock;
    private bool _lastCursorVisible;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _inputs = GetComponent<StarterAssetsInputs>();
    }

    private void Start()
    {
        _lastPlayerPos = transform.position;

        Camera cam = Camera.main;
        _lastCamPos = cam != null ? cam.transform.position : Vector3.zero;
        _lastCamOffset = cam != null ? (cam.transform.position - transform.position) : Vector3.zero;

        _lastPause = PauseMenuController.IsPaused;
        _lastPuzzle = PuzzleTableController.IsOpen;
        _lastSwap = SwapGateUI.IsOpen;
        _lastCursorLock = Cursor.lockState;
        _lastCursorVisible = Cursor.visible;
        _nextHeartbeatTime = Time.unscaledTime + heartbeatInterval;

        Debug.Log($"[MotionDebug] Attached to '{gameObject.name}' at ({transform.position.x:F2},{transform.position.y:F2},{transform.position.z:F2}).");
    }

    private void Update()
    {
        Camera cam = Camera.main;
        Vector3 playerPos = transform.position;
        Vector3 camPos = cam != null ? cam.transform.position : _lastCamPos;
        Vector3 camOffset = cam != null ? (camPos - playerPos) : _lastCamOffset;

        float frameMove = Vector3.Distance(playerPos, _lastPlayerPos);
        float camOffsetDelta = Vector3.Distance(camOffset, _lastCamOffset);

        if (frameMove >= suspiciousFrameJumpDistance)
        {
            Debug.LogWarning($"[MotionDebug] Suspicious player jump: d={frameMove:F2} in one frame. pos=({playerPos.x:F2},{playerPos.y:F2},{playerPos.z:F2}) prev=({_lastPlayerPos.x:F2},{_lastPlayerPos.y:F2},{_lastPlayerPos.z:F2}) state={BuildStateSummary()}");
        }

        if (cam != null && camOffsetDelta >= suspiciousCameraOffsetDelta)
        {
            Debug.LogWarning($"[MotionDebug] Camera/player offset changed abruptly: d={camOffsetDelta:F2}. cam=({camPos.x:F2},{camPos.y:F2},{camPos.z:F2}) offset=({camOffset.x:F2},{camOffset.y:F2},{camOffset.z:F2}) prevOffset=({_lastCamOffset.x:F2},{_lastCamOffset.y:F2},{_lastCamOffset.z:F2}) state={BuildStateSummary()}");
        }

        bool pause = PauseMenuController.IsPaused;
        bool puzzle = PuzzleTableController.IsOpen;
        bool swap = SwapGateUI.IsOpen;

        if (pause != _lastPause || puzzle != _lastPuzzle || swap != _lastSwap)
        {
            Debug.Log($"[MotionDebug] UI state changed -> paused={pause}, puzzle={puzzle}, swap={swap}, state={BuildStateSummary()}");
            _lastPause = pause;
            _lastPuzzle = puzzle;
            _lastSwap = swap;
        }

        if (Cursor.lockState != _lastCursorLock || Cursor.visible != _lastCursorVisible)
        {
            Debug.Log($"[MotionDebug] Cursor changed -> lock={Cursor.lockState}, visible={Cursor.visible}, state={BuildStateSummary()}");
            _lastCursorLock = Cursor.lockState;
            _lastCursorVisible = Cursor.visible;
        }

        if (Time.unscaledTime >= _nextHeartbeatTime)
        {
            Debug.Log($"[MotionDebug] Heartbeat: pos=({playerPos.x:F2},{playerPos.y:F2},{playerPos.z:F2}), state={BuildStateSummary()}");
            _nextHeartbeatTime = Time.unscaledTime + heartbeatInterval;
        }

        _lastPlayerPos = playerPos;
        _lastCamPos = camPos;
        _lastCamOffset = camOffset;
    }

    private string BuildStateSummary()
    {
        Vector3 vel = _cc != null ? _cc.velocity : Vector3.zero;
        bool ccEnabled = _cc != null && _cc.enabled;
        bool grounded = _cc != null && _cc.isGrounded;
        string flags = _cc != null ? _cc.collisionFlags.ToString() : "None";
        bool sprint = _inputs != null && _inputs.sprint;
        Vector2 move = _inputs != null ? _inputs.move : Vector2.zero;

        return $"ccEnabled={ccEnabled}, grounded={grounded}, flags={flags}, vel=({vel.x:F2},{vel.y:F2},{vel.z:F2}), sprint={sprint}, move=({move.x:F2},{move.y:F2}), cutscene={CutsceneController.IsPlaying}, cameraOnly={CutsceneController.CameraOnlyMode}";
    }
}
