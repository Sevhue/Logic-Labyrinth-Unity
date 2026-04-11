using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
	[RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
	[RequireComponent(typeof(PlayerInput))]
#endif
	public class FirstPersonController : MonoBehaviour
	{
		[Header("Player")]
		[Tooltip("Move speed of the character in m/s")]
		public float MoveSpeed = 4.0f;
		[Tooltip("Sprint speed of the character in m/s")]
		public float SprintSpeed = 6.0f;
		[Tooltip("Rotation speed of the character")]
		public float RotationSpeed = 1.0f;
		[Tooltip("Acceleration and deceleration")]
		public float SpeedChangeRate = 10.0f;

		[Space(10)]
		[Tooltip("The height the player can jump")]
		public float JumpHeight = 1.2f;
		[Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
		public float Gravity = -15.0f;

		[Space(10)]
		[Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
		public float JumpTimeout = 0.1f;
		[Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
		public float FallTimeout = 0.15f;

		[Header("Player Grounded")]
		[Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
		public bool Grounded = true;
		[Tooltip("Useful for rough ground")]
		public float GroundedOffset = -0.14f;
		[Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
		public float GroundedRadius = 0.5f;
		[Tooltip("What layers the character uses as ground")]
		public LayerMask GroundLayers;

		[Header("Collision Safety")]
		[Tooltip("Minimum CharacterController skin width enforced at runtime for stable wall contacts.")]
		public float SafeSkinWidth = 0.10f;
		[Tooltip("Target CharacterController step offset enforced at runtime.")]
		public float SafeStepOffset = 0.30f;
		[Tooltip("Maximum slope angle that is walkable. Raise this if sloped floors feel like invisible walls.")]
		public float SafeSlopeLimit = 70f;
		[Tooltip("Log one warning when bumping into a blocking collider with no visible renderer (helps find invisible walls).")]
		public bool DebugInvisibleWallHits = true;
		[Tooltip("Disable anti-warp correction in Level6 to avoid false position rewinds that can feel like invisible walls.")]
		public bool DisableAntiWarpInLevel6 = true;
		[Tooltip("Disable anti-warp correction in Level5-8 where complex Chapter 2 geometry can trigger false snap-back while walking.")]
		public bool DisableAntiWarpInLevel5To8 = true;

		[Header("Stamina")]
		[Tooltip("Maximum stamina")]
		public float MaxStamina = 100f;
		[Tooltip("How fast stamina drains per second while sprinting")]
		public float StaminaDrainRate = 25f;
		[Tooltip("How fast stamina regenerates per second while not sprinting")]
		public float StaminaRegenRate = 15f;
		[Tooltip("Delay in seconds before stamina starts regenerating after sprinting stops")]
		public float StaminaRegenDelay = 1.0f;
		[Tooltip("Minimum stamina needed to START sprinting (prevents flicker)")]
		public float MinStaminaToSprint = 10f;

		// Public read-only for UI
		public float CurrentStamina { get; private set; }
		public float StaminaPercent => MaxStamina > 0f ? CurrentStamina / MaxStamina : 0f;
		public bool IsExhausted { get; private set; }

		private float _staminaRegenTimer;

		[Header("Health")]
		[Tooltip("Maximum health (capped at 100).")]
		public float MaxHealth = 100f;
		[Tooltip("Small i-frame window after taking damage to prevent ultra-fast multi-hits.")]
		public float DamageCooldownSeconds = 0.25f;
		[Tooltip("How long the red damage flash lasts.")]
		public float DamageFlashDuration = 0.20f;
		[Tooltip("Peak alpha of the red damage flash.")]
		public float DamageFlashMaxAlpha = 0.30f;
		[Tooltip("How long hit camera shake lasts.")]
		public float DamageShakeDuration = 0.20f;
		[Tooltip("Strength of hit camera shake.")]
		public float DamageShakeIntensity = 0.09f;
		[Tooltip("Seconds to fade in the death darkening overlay.")]
		public float DeathOverlayFadeDuration = 0.45f;
		[Tooltip("Seconds to keep the death screen visible before respawn.")]
		public float DeathHoldDuration = 0.95f;
		[Tooltip("Maximum darkness of death background overlay.")]
		[Range(0f, 1f)]
		public float DeathOverlayMaxAlpha = 0.72f;

		public float CurrentHealth { get; private set; }
		public float HealthPercent => MaxHealth > 0f ? CurrentHealth / MaxHealth : 0f;
		public bool IsDead { get; private set; }
		/// <summary>Set to true before calling ApplyDamage to skip the gate-drop on death (e.g. puzzle game-over).</summary>
		public bool SuppressGateDrop { get; set; }
		private float _nextDamageAllowedTime;
		private CanvasGroup _damageFlashCanvasGroup;
		private float _damageFlashTimer;
		private float _damageShakeTimer;
		private Vector3 _cameraTargetBaseLocalPos;
		private bool _cameraTargetBaseCached;
		private bool _deathRoutineRunning;
		private CanvasGroup _deathOverlayCanvasGroup;
		private TextMeshProUGUI _deathTextTMP;
		private TextMeshProUGUI _respawnPromptTMP;
		private Vector3 _levelStartPosition;
		private float _levelStartYaw;
		private bool _hasLevelStartPose;

		[Header("Cinemachine")]
		[Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
		public GameObject CinemachineCameraTarget;
		[Tooltip("How far in degrees can you move the camera up")]
		public float TopClamp = 90.0f;
		[Tooltip("How far in degrees can you move the camera down")]
		public float BottomClamp = -90.0f;

		// cinemachine
		private float _cinemachineTargetPitch;

		// player
		private float _speed;
		private float _rotationVelocity;
		private float _verticalVelocity;
		private float _terminalVelocity = 53.0f;
		private Vector3 _lastStableGroundedPosition;
		private bool _hasStableGroundedPosition;
		private float _antiWarpCooldown;
		private readonly HashSet<int> _loggedHiddenBlockerColliders = new HashSet<int>();
		private float _blockedInputTimer;
		private float _nextBlockerLogTime;
		private Vector3 _lastGroundNormal = Vector3.up;
		private bool _hasLastGroundNormal;

		// timeout deltatime
		private float _jumpTimeoutDelta;
		private float _fallTimeoutDelta;

	
#if ENABLE_INPUT_SYSTEM
		private PlayerInput _playerInput;
#endif
		private CharacterController _controller;
		private StarterAssetsInputs _input;
		private GameObject _mainCamera;
		private bool _tabCursorVisible;

		private const float _threshold = 0.01f;
		private const float MaxVerticalSnapPerFrame = 4.0f;
		private const float MaxHorizontalSnapPerFrame = 5.0f;
		private const float MaxTotalSnapPerFrame = 7.0f;
		private const float AbsurdCoordinateLimit = 5000.0f;
		private const float Level6AbsurdCoordinateLimit = 50000.0f;
		private const float MinAllowedY = -150.0f;
		private const float Level6MinAllowedY = -1000.0f;
		private const float MaxAllowedDropFromLevelStart = 25.0f;
		private const float HardSingleFrameVerticalWarpLimit = 20.0f;
		private const float HardSingleFrameTotalWarpLimit = 25.0f;

		private bool IsLevel5To8Scene(string sceneName)
		{
			return sceneName == "Level5" || sceneName == "Level6" || sceneName == "Level7" || sceneName == "Level8";
		}

		private bool IsCurrentDeviceMouse
		{
			get
			{
				#if ENABLE_INPUT_SYSTEM
				return _playerInput.currentControlScheme == "KeyboardMouse";
				#else
				return false;
				#endif
			}
		}

		private void Awake()
		{
			// get a reference to our main camera
			if (_mainCamera == null)
			{
				_mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
			}
		}

		private void Start()
		{
			_controller = GetComponent<CharacterController>();
			_input = GetComponent<StarterAssetsInputs>();
#if ENABLE_INPUT_SYSTEM
			_playerInput = GetComponent<PlayerInput>();
#else
			Debug.LogError( "Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif

			// reset our timeouts on start
			_jumpTimeoutDelta = JumpTimeout;
			_fallTimeoutDelta = FallTimeout;

			// Initialize stamina
			CurrentStamina = MaxStamina;
			IsExhausted = false;

			// Initialize health
			MaxHealth = Mathf.Clamp(MaxHealth, 1f, 100f);
			CurrentHealth = MaxHealth;
			IsDead = false;
			_nextDamageAllowedTime = 0f;

			_tabCursorVisible = false;
			SetGameplayCursorVisible(false);
			ApplyCollisionSafetySettings();
			EnsureDamageFlashOverlay();
			if (CinemachineCameraTarget != null)
			{
				_cameraTargetBaseLocalPos = CinemachineCameraTarget.transform.localPosition;
				_cameraTargetBaseCached = true;
			}
			_lastStableGroundedPosition = transform.position;
			_hasStableGroundedPosition = true;

			// Cache the real player start pose for this scene so death respawn is not tied
			// to gate SpawnPoint markers (SpawnPoint2..10, etc.).
			_levelStartPosition = transform.position;
			_levelStartYaw = transform.eulerAngles.y;
			// If a dedicated player spawn marker exists, use it as the authoritative respawn anchor.
			if (TryGetSpawnPoint(out Vector3 markerPos, out float markerYaw))
			{
				_levelStartPosition = markerPos;
				_levelStartYaw = markerYaw;
			}
			_hasLevelStartPose = true;
		}

		private void ApplyCollisionSafetySettings()
		{
			if (_controller == null) return;

			float targetSkin = Mathf.Clamp(SafeSkinWidth, 0.08f, 0.15f);
			if (_controller.skinWidth < targetSkin)
				_controller.skinWidth = targetSkin;

			_controller.stepOffset = Mathf.Clamp(SafeStepOffset, 0.2f, 0.5f);
			_controller.slopeLimit = Mathf.Clamp(SafeSlopeLimit, 45f, 89f);
			_controller.minMoveDistance = 0f;

			Debug.Log($"[FirstPersonController] Collision safety applied: skinWidth={_controller.skinWidth:F3}, stepOffset={_controller.stepOffset:F3}, slopeLimit={_controller.slopeLimit:F1}, minMoveDistance={_controller.minMoveDistance:F3}");
		}

		private void Update()
		{
			UpdateDamageFeedback();

			if (IsDead)
				return;

			// If pause is open, clear tab-toggle state so resume returns to normal locked gameplay cursor.
			if (PauseMenuController.IsPaused)
			{
				_tabCursorVisible = false;
				return;
			}

			if (PuzzleTableController.IsOpen || TruthTableDisplay.IsOpen)
			{
				_tabCursorVisible = false;
				SetGameplayCursorVisible(true);

				if (_input != null)
				{
					_input.cursorInputForLook = false;
					_input.cursorLocked = false;
					_input.MoveInput(Vector2.zero);
					_input.LookInput(Vector2.zero);
					_input.JumpInput(false);
					_input.SprintInput(false);
				}

				return;
			}

#if ENABLE_INPUT_SYSTEM
			// TAB toggles gameplay cursor visibility/lock so players can quickly use the mouse.
			if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
			{
				_tabCursorVisible = !_tabCursorVisible;
				SetGameplayCursorVisible(_tabCursorVisible);
			}
#endif

			// Don't process input while game is paused
			// (already handled above)

			// Full cutscene block (Cutscene1/2: black screens) — freeze everything
			if (CutsceneController.IsPlaying) return;

			// Camera-only cutscene (Cutscene3/4): allow gravity but block movement
			if (CutsceneController.CameraOnlyMode)
			{
				GroundedCheck();
				JumpAndGravity();
				// Apply only gravity, no lateral movement
				_controller.Move(new Vector3(0f, _verticalVelocity, 0f) * Time.deltaTime);
				return;
			}

			if (TryRecoverFromAbsurdPosition("pre-move-check"))
				return;

			JumpAndGravity();
			GroundedCheck();
			Move();
		}

		private void SetGameplayCursorVisible(bool visible)
		{
			Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
			Cursor.visible = visible;

			if (_input != null)
			{
				_input.cursorInputForLook = !visible;
				_input.cursorLocked = !visible;
				_input.LookInput(Vector2.zero);
				if (visible)
				{
					_input.MoveInput(Vector2.zero);
				}
			}

			_rotationVelocity = 0f;
		}

		private void LateUpdate()
		{
			// Don't rotate camera while paused
			if (PauseMenuController.IsPaused) return;
			if (PuzzleTableController.IsOpen || TruthTableDisplay.IsOpen) return;
			if (_tabCursorVisible) return;

			// Full cutscene block — no camera rotation
			if (CutsceneController.IsPlaying) return;

			// Camera-only mode — camera look IS allowed
			CameraRotation();
		}

		private void GroundedCheck()
		{
			// Use a combination of a small sphere check (to avoid walls/ceilings triggering false grounded)
			// and the CharacterController's built-in isGrounded for reliability.
			// The sphere check alone can clip into walls/ceilings in tight dungeon environments.
			
			float ccBottom = _controller.center.y - _controller.height / 2f;
			Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y + ccBottom, transform.position.z);
			
			// Use a smaller radius than the CC to avoid wall clipping, and cast downward
			float checkRadius = _controller.radius * 0.5f; // Half the CC radius - much less likely to clip walls
			float checkDistance = 0.3f; // How far below feet to check
			
			// SphereCast downward from the CC bottom - only detects ground BELOW the player
			bool sphereGrounded = Physics.SphereCast(
				spherePosition + Vector3.up * checkDistance, // Start slightly above the feet
				checkRadius,
				Vector3.down,
				out RaycastHit hit,
				checkDistance + 0.1f, // Cast distance
				GroundLayers,
				QueryTriggerInteraction.Ignore
			);

			if (sphereGrounded)
			{
				_lastGroundNormal = hit.normal;
				_hasLastGroundNormal = true;
			}
			else
			{
				_hasLastGroundNormal = false;
			}
			
			// Also check CC's built-in grounded (handles edge cases)
			Grounded = sphereGrounded || _controller.isGrounded;
		}

		private void CameraRotation()
		{
			// if there is an input
			if (_input.look.sqrMagnitude >= _threshold)
			{
				//Don't multiply mouse input by Time.deltaTime
				float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;
				
				_cinemachineTargetPitch += _input.look.y * RotationSpeed * deltaTimeMultiplier;
				_rotationVelocity = _input.look.x * RotationSpeed * deltaTimeMultiplier;

				// clamp our pitch rotation
				_cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

				// Update Cinemachine camera target pitch
				CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch, 0.0f, 0.0f);

				// rotate the player left and right
				transform.Rotate(Vector3.up * _rotationVelocity);
			}
		}

		private void Move()
		{
			Vector3 preMovePosition = transform.position;
			string sceneName = gameObject.scene.name;
			bool isLevel5To8 = IsLevel5To8Scene(sceneName);
			bool antiWarpDisabledForLevel =
				(DisableAntiWarpInLevel6 && sceneName == "Level6") ||
				(DisableAntiWarpInLevel5To8 && isLevel5To8);
			bool antiWarpEnabled = !antiWarpDisabledForLevel;

			if (antiWarpEnabled && _antiWarpCooldown > 0f)
			{
				_antiWarpCooldown -= Time.deltaTime;
				_controller.Move(new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);
				return;
			}

			// --- Stamina logic ---
			bool wantsSprint = _input.sprint && _input.move != Vector2.zero;
			bool canSprint = !IsExhausted && CurrentStamina > 0f;

			// If exhausted, need to recharge past the threshold before sprinting again
			if (IsExhausted && CurrentStamina >= MinStaminaToSprint)
				IsExhausted = false;

			bool isSprinting = wantsSprint && canSprint;

			if (isSprinting)
			{
				CurrentStamina -= StaminaDrainRate * Time.deltaTime;
				CurrentStamina = Mathf.Max(0f, CurrentStamina);
				_staminaRegenTimer = StaminaRegenDelay;

				if (CurrentStamina <= 0f)
					IsExhausted = true;
			}
			else
			{
				// Countdown regen delay
				if (_staminaRegenTimer > 0f)
					_staminaRegenTimer -= Time.deltaTime;
				else
				{
					CurrentStamina += StaminaRegenRate * Time.deltaTime;
					CurrentStamina = Mathf.Min(CurrentStamina, MaxStamina);
				}
			}

			// set target speed based on move speed, sprint speed and if sprint is pressed
			float targetSpeed = isSprinting ? SprintSpeed : MoveSpeed;

			// a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

			// note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
			// if there is no input, set the target speed to 0
			if (_input.move == Vector2.zero) targetSpeed = 0.0f;

			// a reference to the players current horizontal velocity
			float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

			float speedOffset = 0.1f;
			float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

			// accelerate or decelerate to target speed
			if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
			{
				// creates curved result rather than a linear one giving a more organic speed change
				// note T in Lerp is clamped, so we don't need to clamp our speed
				_speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);

				// round speed to 3 decimal places
				_speed = Mathf.Round(_speed * 1000f) / 1000f;
			}
			else
			{
				_speed = targetSpeed;
			}

			// normalise input direction
			Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

			// note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
			// if there is a move input rotate player when the player is moving
			if (_input.move != Vector2.zero)
			{
				// move
				inputDirection = transform.right * _input.move.x + transform.forward * _input.move.y;
			}

			// move the player
			Vector3 desiredMovement = inputDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime;
			CollisionFlags moveFlags = _controller.Move(desiredMovement);

			Vector3 postMovePosition = transform.position;
			Vector3 frameDelta = postMovePosition - preMovePosition;
			bool hasSideContact = (moveFlags & CollisionFlags.Sides) != 0;

			// Last-resort protection against one-frame physics spikes (e.g., sudden ejection by bad mesh contact).
			// This runs even when anti-warp is disabled for Chapter 2 levels.
			bool hardWarpSpike =
				!_input.jump &&
				!hasSideContact &&
				(Mathf.Abs(frameDelta.y) > HardSingleFrameVerticalWarpLimit || frameDelta.magnitude > HardSingleFrameTotalWarpLimit);

			if (hardWarpSpike)
			{
				Vector3 restorePos = preMovePosition;
				if (_hasStableGroundedPosition && IsReasonableSafePosition(_lastStableGroundedPosition))
					restorePos = _lastStableGroundedPosition;

				bool wasEnabled = _controller.enabled;
				if (wasEnabled) _controller.enabled = false;
				transform.position = restorePos;
				Physics.SyncTransforms();
				if (wasEnabled) _controller.enabled = true;

				_verticalVelocity = -2f;
				_speed = 0f;
				_antiWarpCooldown = 0.35f;

				Debug.LogWarning($"[FirstPersonController] Hard warp guard blocked one-frame spike (delta=({frameDelta.x:F2},{frameDelta.y:F2},{frameDelta.z:F2}), mag={frameDelta.magnitude:F2}) in scene '{sceneName}'. Restored to ({restorePos.x:F2},{restorePos.y:F2},{restorePos.z:F2}).");
				return;
			}
			
			// ===== DEBUG: Detect invisible walls / slope blockers =====
			float horizontalDesiredMag = new Vector2(desiredMovement.x, desiredMovement.z).magnitude;
			float horizontalAchievedMag = new Vector2(frameDelta.x, frameDelta.z).magnitude;

			
			// Log only when horizontal movement is meaningfully blocked, or the controller reports side contact.
			bool movementBlocked = horizontalDesiredMag > 0.001f && horizontalAchievedMag < horizontalDesiredMag * 0.5f && _input.move != Vector2.zero;
			bool sideBlocked = (moveFlags & CollisionFlags.Sides) != 0 && _input.move != Vector2.zero;
			bool slopeBlocked = false;
			if (_hasLastGroundNormal && _input.move != Vector2.zero)
			{
				float slopeAngle = Vector3.Angle(_lastGroundNormal, Vector3.up);
				if (slopeAngle > _controller.slopeLimit - 0.5f)
				{
					Vector3 uphillDir = Vector3.ProjectOnPlane(Vector3.up, _lastGroundNormal).normalized;
					float uphillPush = Vector3.Dot(inputDirection.normalized, uphillDir);
					slopeBlocked = uphillPush > 0.15f;
				}
			}

			bool likelyBlocked = (movementBlocked || sideBlocked || slopeBlocked) && _input.move != Vector2.zero;
			if (likelyBlocked)
			{
				_blockedInputTimer += Time.deltaTime;
				if (_blockedInputTimer >= 0.12f && Time.time >= _nextBlockerLogTime)
				{
					Vector3 moveDir = inputDirection.normalized;
					LogCurrentBlockers(moveDir, moveFlags, horizontalDesiredMag, horizontalAchievedMag, movementBlocked, sideBlocked, slopeBlocked);
					_nextBlockerLogTime = Time.time + 0.35f;
				}
			}
			else
			{
				_blockedInputTimer = 0f;
			}
			// =========== END DEBUG ===========
			
			float verticalDelta = postMovePosition.y - preMovePosition.y;
			float horizontalDelta = new Vector2(frameDelta.x, frameDelta.z).magnitude;

			bool suspiciousVerticalPop =
				verticalDelta > MaxVerticalSnapPerFrame &&
				Grounded &&
				!_input.jump &&
				!hasSideContact;

			bool suspiciousPositionWarp =
				!_input.jump &&
				(horizontalDelta > MaxHorizontalSnapPerFrame || frameDelta.magnitude > MaxTotalSnapPerFrame) &&
				!hasSideContact;

			if (antiWarpEnabled && (suspiciousVerticalPop || suspiciousPositionWarp))
			{
				Vector3 restorePos = preMovePosition;
				if (!IsReasonableSafePosition(restorePos))
				{
					if (_hasStableGroundedPosition && IsReasonableSafePosition(_lastStableGroundedPosition))
						restorePos = _lastStableGroundedPosition;
					else if (_hasLevelStartPose)
						restorePos = _levelStartPosition;
					else
						restorePos = Vector3.zero;
				}

				bool wasEnabled = _controller.enabled;
				if (wasEnabled) _controller.enabled = false;
				transform.position = restorePos;
				Physics.SyncTransforms();
				if (wasEnabled) _controller.enabled = true;

				_verticalVelocity = -2f;
				_speed = 0f;
				_antiWarpCooldown = 0.18f;

				if (suspiciousVerticalPop)
					Debug.LogWarning($"[FirstPersonController] Blocked suspicious vertical pop (dy={verticalDelta:F2}). Restored to ({restorePos.x:F2},{restorePos.y:F2},{restorePos.z:F2}).");
				else
					Debug.LogWarning($"[FirstPersonController] Blocked suspicious position warp (dh={horizontalDelta:F2}, d={frameDelta.magnitude:F2}). Restored to ({restorePos.x:F2},{restorePos.y:F2},{restorePos.z:F2}).");
			}
			else if (Grounded && !_input.jump)
			{
				float controllerVerticalSpeed = Mathf.Abs(_controller.velocity.y);
				if (controllerVerticalSpeed < 1.5f && IsReasonableSafePosition(transform.position))
				{
					_lastStableGroundedPosition = transform.position;
					_hasStableGroundedPosition = true;
				}
			}
		}

		private bool IsReasonableSafePosition(Vector3 pos)
		{
			if (float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z))
				return false;

			if (Mathf.Abs(pos.x) > AbsurdCoordinateLimit || Mathf.Abs(pos.z) > AbsurdCoordinateLimit)
				return false;

			float minReasonableY = MinAllowedY;
			if (_hasLevelStartPose)
				minReasonableY = Mathf.Max(MinAllowedY, _levelStartPosition.y - MaxAllowedDropFromLevelStart);

			return pos.y >= minReasonableY;
		}

		private void LogCurrentBlockers(
			Vector3 moveDir,
			CollisionFlags moveFlags,
			float desired,
			float achieved,
			bool movementBlocked,
			bool sideBlocked,
			bool slopeBlocked)
		{
			if (_controller == null)
				return;

			Vector3 center = transform.position + _controller.center;
			Vector3 rayStartPos = center;
			float castDistance = 3f;

			RaycastHit[] castHits = Physics.SphereCastAll(
				rayStartPos,
				Mathf.Max(0.05f, _controller.radius * 0.85f),
				moveDir,
				castDistance,
				~0,
				QueryTriggerInteraction.Collide);

			HashSet<int> seen = new HashSet<int>();
			string blockerInfo = "";
			int added = 0;

			for (int i = 0; i < castHits.Length; i++)
			{
				Collider col = castHits[i].collider;
				if (col == null) continue;
				if (col.transform.root == transform.root) continue;

				int id = col.GetInstanceID();
				if (seen.Contains(id)) continue;
				seen.Add(id);

				Renderer r = col.GetComponentInParent<Renderer>();
				bool visible = r != null && r.enabled && r.gameObject.activeInHierarchy;
				float hitSlope = Vector3.Angle(castHits[i].normal, Vector3.up);

				blockerInfo += $"[{added}] name={col.name}, trigger={col.isTrigger}, visible={visible}, dist={castHits[i].distance:F2}, slope={hitSlope:F1}, path={GetPath(col.transform)} | ";
				added++;
				if (added >= 8) break;
			}

			if (added == 0)
				blockerInfo = "no forward spherecast hits";

			float groundSlope = _hasLastGroundNormal ? Vector3.Angle(_lastGroundNormal, Vector3.up) : -1f;
			Debug.LogWarning($"[BLOCKER DEBUG] moveBlocked={movementBlocked} side={sideBlocked} slopeBlocked={slopeBlocked} desired={desired:F4} achieved={achieved:F4} flags={moveFlags} groundSlope={groundSlope:F1} slopeLimit={_controller.slopeLimit:F1} info={blockerInfo}");
			Debug.DrawRay(rayStartPos, moveDir * castDistance, Color.red, 0.8f);
		}

		private bool TryRecoverFromAbsurdPosition(string reason)
		{
			Vector3 pos = transform.position;
			bool isLevel6 = gameObject.scene.name == "Level6";
			float sceneAbsurdLimit = isLevel6 ? Level6AbsurdCoordinateLimit : AbsurdCoordinateLimit;
			float sceneMinY = isLevel6 ? Level6MinAllowedY : MinAllowedY;
			bool absurd =
				float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z) ||
				Mathf.Abs(pos.x) > sceneAbsurdLimit ||
				Mathf.Abs(pos.z) > sceneAbsurdLimit ||
				pos.y < sceneMinY;

			if (!absurd)
				return false;

			Vector3 restorePos = (_hasStableGroundedPosition && IsReasonableSafePosition(_lastStableGroundedPosition))
				? _lastStableGroundedPosition
				: (_hasLevelStartPose ? _levelStartPosition : Vector3.zero);

			bool wasEnabled = _controller != null && _controller.enabled;
			if (wasEnabled) _controller.enabled = false;
			transform.position = restorePos;
			Physics.SyncTransforms();
			if (wasEnabled) _controller.enabled = true;

			_verticalVelocity = -2f;
			_speed = 0f;
			_antiWarpCooldown = 0.2f;

			Debug.LogError($"[FirstPersonController] Recovered from absurd position at {reason}. pos=({pos.x:F2},{pos.y:F2},{pos.z:F2}) restore=({restorePos.x:F2},{restorePos.y:F2},{restorePos.z:F2})");
			return true;
		}

		private void JumpAndGravity()
		{
			if (Grounded)
			{
				// reset the fall timeout timer
				_fallTimeoutDelta = FallTimeout;

				// stop our velocity dropping infinitely when grounded
				if (_verticalVelocity < 0.0f)
				{
					_verticalVelocity = -2f;
				}

				// Jump
				if (_input.jump && _jumpTimeoutDelta <= 0.0f)
				{
					// the square root of H * -2 * G = how much velocity needed to reach desired height
					_verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
				}

				// jump timeout
				if (_jumpTimeoutDelta >= 0.0f)
				{
					_jumpTimeoutDelta -= Time.deltaTime;
				}
			}
			else
			{
				// reset the jump timeout timer
				_jumpTimeoutDelta = JumpTimeout;

				// fall timeout
				if (_fallTimeoutDelta >= 0.0f)
				{
					_fallTimeoutDelta -= Time.deltaTime;
				}

				// if we are not grounded, do not jump
				_input.jump = false;
			}

			// If we hit a ceiling while going up, immediately stop upward velocity
			// This prevents getting stuck against ceilings in enclosed dungeon environments
			if (_verticalVelocity > 0f && (_controller.collisionFlags & CollisionFlags.Above) != 0)
			{
				_verticalVelocity = 0f;
			}

			// apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
			if (_verticalVelocity < _terminalVelocity)
			{
				_verticalVelocity += Gravity * Time.deltaTime;
			}

			if (!float.IsFinite(_verticalVelocity))
				_verticalVelocity = -2f;

			_verticalVelocity = Mathf.Clamp(_verticalVelocity, -120f, 35f);
		}

		private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
		{
			if (lfAngle < -360f) lfAngle += 360f;
			if (lfAngle > 360f) lfAngle -= 360f;
			return Mathf.Clamp(lfAngle, lfMin, lfMax);
		}

		private void OnDrawGizmosSelected()
		{
			Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
			Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

			if (Grounded) Gizmos.color = transparentGreen;
			else Gizmos.color = transparentRed;

			// Draw the ground check sphere at the CC bottom
			CharacterController cc = GetComponent<CharacterController>();
			if (cc != null)
			{
				float ccBottom = cc.center.y - cc.height / 2f;
				float checkRadius = cc.radius * 0.5f;
				Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y + ccBottom, transform.position.z), checkRadius);
			}
			else
			{
				Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z), GroundedRadius);
			}
		}

		public void ApplyDamage(float damageAmount)
		{
			if (damageAmount <= 0f || IsDead) return;
			if (Time.time < _nextDamageAllowedTime) return;

			_nextDamageAllowedTime = Time.time + Mathf.Max(0f, DamageCooldownSeconds);
			CurrentHealth = Mathf.Clamp(CurrentHealth - damageAmount, 0f, MaxHealth);
			TriggerDamageFeedback();

			if (CurrentHealth <= 0f)
			{
				IsDead = true;
				Debug.LogWarning("[FirstPersonController] Player health reached 0.");
				if (!_deathRoutineRunning)
					StartCoroutine(DeathAndRespawnRoutine());
			}
		}

		public void Heal(float amount)
		{
			if (amount <= 0f) return;
			MaxHealth = Mathf.Clamp(MaxHealth, 1f, 100f);
			CurrentHealth = Mathf.Clamp(CurrentHealth + amount, 0f, MaxHealth);
			if (CurrentHealth > 0f) IsDead = false;
		}

		private void OnControllerColliderHit(ControllerColliderHit hit)
		{
			if (hit == null || hit.collider == null) return;

			SpikeTrapHazard hazard = hit.collider.GetComponentInParent<SpikeTrapHazard>();
			if (hazard != null)
				hazard.TryDamage(gameObject);

			if (!DebugInvisibleWallHits) return;
			if (hit.collider.isTrigger) return;

			Renderer r = hit.collider.GetComponentInParent<Renderer>();
			if (r != null && r.enabled && r.gameObject.activeInHierarchy) return;

			int colliderId = hit.collider.GetInstanceID();
			if (_loggedHiddenBlockerColliders.Contains(colliderId)) return;
			_loggedHiddenBlockerColliders.Add(colliderId);

			Debug.LogWarning($"[InvisibleWallCandidate] scene='{gameObject.scene.name}' collider='{hit.collider.name}' path='{GetPath(hit.collider.transform)}' point=({hit.point.x:F2},{hit.point.y:F2},{hit.point.z:F2})");
		}

		private static string GetPath(Transform t)
		{
			if (t == null) return "<null>";
			string path = t.name;
			while (t.parent != null)
			{
				t = t.parent;
				path = t.name + "/" + path;
			}
			return path;
		}

		private void EnsureDamageFlashOverlay()
		{
			if (_damageFlashCanvasGroup != null) return;

			Canvas targetCanvas = null;
			Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
			for (int i = 0; i < allCanvases.Length; i++)
			{
				Canvas c = allCanvases[i];
				if (c != null && c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.name == "LevelCanvas")
				{
					targetCanvas = c;
					break;
				}
			}

			if (targetCanvas == null)
			{
				for (int i = 0; i < allCanvases.Length; i++)
				{
					Canvas c = allCanvases[i];
					if (c != null && c.renderMode == RenderMode.ScreenSpaceOverlay && c.isRootCanvas)
					{
						targetCanvas = c;
						break;
					}
				}
			}

			if (targetCanvas == null)
			{
				GameObject canvasGO = new GameObject("DamageFlashCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
				targetCanvas = canvasGO.GetComponent<Canvas>();
				targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
				targetCanvas.sortingOrder = 1000;
				CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
				scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
				scaler.referenceResolution = new Vector2(1920f, 1080f);
			}

			GameObject flashGO = new GameObject("PlayerDamageFlash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
			flashGO.transform.SetParent(targetCanvas.transform, false);

			RectTransform rt = flashGO.GetComponent<RectTransform>();
			rt.anchorMin = Vector2.zero;
			rt.anchorMax = Vector2.one;
			rt.offsetMin = Vector2.zero;
			rt.offsetMax = Vector2.zero;

			Image img = flashGO.GetComponent<Image>();
			img.color = new Color(0.85f, 0.08f, 0.08f, 1f);
			img.raycastTarget = false;

			_damageFlashCanvasGroup = flashGO.GetComponent<CanvasGroup>();
			_damageFlashCanvasGroup.alpha = 0f;
		}

		private void EnsureDeathOverlay()
		{
			if (_deathOverlayCanvasGroup != null && _deathTextTMP != null && _respawnPromptTMP != null)
				return;

			Canvas targetCanvas = null;
			Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
			for (int i = 0; i < allCanvases.Length; i++)
			{
				Canvas c = allCanvases[i];
				if (c != null && c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.name == "LevelCanvas")
				{
					targetCanvas = c;
					break;
				}
			}

			if (targetCanvas == null)
			{
				for (int i = 0; i < allCanvases.Length; i++)
				{
					Canvas c = allCanvases[i];
					if (c != null && c.renderMode == RenderMode.ScreenSpaceOverlay && c.isRootCanvas)
					{
						targetCanvas = c;
						break;
					}
				}
			}

			if (targetCanvas == null)
			{
				GameObject canvasGO = new GameObject("DeathOverlayCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
				targetCanvas = canvasGO.GetComponent<Canvas>();
				targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
				targetCanvas.sortingOrder = 1100;
				CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
				scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
				scaler.referenceResolution = new Vector2(1920f, 1080f);
			}

			GameObject overlayGO = new GameObject("PlayerDeathOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
			overlayGO.transform.SetParent(targetCanvas.transform, false);

			RectTransform overlayRT = overlayGO.GetComponent<RectTransform>();
			overlayRT.anchorMin = Vector2.zero;
			overlayRT.anchorMax = Vector2.one;
			overlayRT.offsetMin = Vector2.zero;
			overlayRT.offsetMax = Vector2.zero;

			Image overlayImg = overlayGO.GetComponent<Image>();
			overlayImg.color = Color.black;
			overlayImg.raycastTarget = false;

			_deathOverlayCanvasGroup = overlayGO.GetComponent<CanvasGroup>();
			_deathOverlayCanvasGroup.alpha = 0f;

			GameObject textGO = new GameObject("DeathText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
			textGO.transform.SetParent(overlayGO.transform, false);

			RectTransform textRT = textGO.GetComponent<RectTransform>();
			textRT.anchorMin = new Vector2(0.5f, 0.5f);
			textRT.anchorMax = new Vector2(0.5f, 0.5f);
			textRT.pivot = new Vector2(0.5f, 0.5f);
			textRT.anchoredPosition = new Vector2(0f, 8f);
			textRT.sizeDelta = new Vector2(900f, 220f);

			_deathTextTMP = textGO.GetComponent<TextMeshProUGUI>();
			_deathTextTMP.text = "YOU DIED";
			_deathTextTMP.alignment = TextAlignmentOptions.Center;
			_deathTextTMP.fontSize = 84f;
			_deathTextTMP.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
			_deathTextTMP.color = new Color(0.92f, 0.12f, 0.12f, 0f);
			_deathTextTMP.raycastTarget = false;

			GameObject promptGO = new GameObject("RespawnPromptText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
			promptGO.transform.SetParent(overlayGO.transform, false);

			RectTransform promptRT = promptGO.GetComponent<RectTransform>();
			promptRT.anchorMin = new Vector2(0.5f, 0.5f);
			promptRT.anchorMax = new Vector2(0.5f, 0.5f);
			promptRT.pivot = new Vector2(0.5f, 0.5f);
			promptRT.anchoredPosition = new Vector2(0f, -90f);
			promptRT.sizeDelta = new Vector2(900f, 70f);

			_respawnPromptTMP = promptGO.GetComponent<TextMeshProUGUI>();
			_respawnPromptTMP.text = "Press Space to Respawn";
			_respawnPromptTMP.alignment = TextAlignmentOptions.Center;
			_respawnPromptTMP.fontSize = 28f;
			_respawnPromptTMP.fontStyle = FontStyles.Normal;
			_respawnPromptTMP.color = new Color(0.75f, 0.75f, 0.75f, 0f);
			_respawnPromptTMP.raycastTarget = false;
		}

		private IEnumerator DeathAndRespawnRoutine()
		{
			if (_deathRoutineRunning)
				yield break;

			_deathRoutineRunning = true;

			// Pause the level timer while dead
			if (LevelTimer.Instance != null)
				LevelTimer.Instance.PauseTimer();

			Vector3 deathPosition = transform.position;
			if (!SuppressGateDrop)
				DropAllCollectedGatesAtDeathPosition(deathPosition);
			SuppressGateDrop = false;

			EnsureDeathOverlay();

			float fadeDuration = Mathf.Max(0.05f, DeathOverlayFadeDuration);
			float holdDuration = Mathf.Max(0.05f, DeathHoldDuration);
			float maxDark = Mathf.Clamp01(DeathOverlayMaxAlpha);
			Color deathTextBase = new Color(0.92f, 0.12f, 0.12f, 1f);

			float t = 0f;
			while (t < fadeDuration)
			{
				t += Time.deltaTime;
				float a = Mathf.Clamp01(t / fadeDuration);
				if (_deathOverlayCanvasGroup != null)
					_deathOverlayCanvasGroup.alpha = maxDark * a;
				if (_deathTextTMP != null)
					_deathTextTMP.color = new Color(deathTextBase.r, deathTextBase.g, deathTextBase.b, a);
				yield return null;
			}

			if (_deathOverlayCanvasGroup != null)
				_deathOverlayCanvasGroup.alpha = maxDark;
			if (_deathTextTMP != null)
				_deathTextTMP.color = deathTextBase;

			yield return new WaitForSeconds(holdDuration);

			// Flash the respawn prompt and wait for the player to press Space
			while (!Input.GetKeyDown(KeyCode.Space))
			{
				if (_respawnPromptTMP != null)
				{
					float flashAlpha = Mathf.Abs(Mathf.Sin(Time.time * 2.5f));
					_respawnPromptTMP.color = new Color(0.75f, 0.75f, 0.75f, flashAlpha);
				}
				yield return null;
			}

			if (_respawnPromptTMP != null)
				_respawnPromptTMP.color = new Color(0.75f, 0.75f, 0.75f, 0f);

			// Reload the scene completely for a fresh start — clear transient items before reload
			if (InventoryManager.Instance != null)
				InventoryManager.Instance.SetHasCandle(false);
			SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
		}

		private void RespawnAtSpawnPoint()
		{
			Vector3 spawnPos;
			float spawnYaw;

			// Prefer the level-entry spawn anchor first (what the player actually used when entering the level).
			if (_hasLevelStartPose)
			{
				spawnPos = _levelStartPosition;
				spawnYaw = _levelStartYaw;
			}
			else if (!TryGetSpawnPoint(out spawnPos, out spawnYaw))
			{
				spawnPos = transform.position + Vector3.up * 0.3f;
				spawnYaw = transform.eulerAngles.y;
			}

			bool wasEnabled = _controller != null && _controller.enabled;
			if (wasEnabled) _controller.enabled = false;
			transform.position = spawnPos;
			transform.rotation = Quaternion.Euler(0f, spawnYaw, 0f);
			Physics.SyncTransforms();
			if (wasEnabled) _controller.enabled = true;

			CurrentHealth = MaxHealth;
			IsDead = false;
			_nextDamageAllowedTime = Time.time + 0.35f;
			_verticalVelocity = -2f;
			_speed = 0f;
			_antiWarpCooldown = 0.2f;
			_jumpTimeoutDelta = JumpTimeout;
			_fallTimeoutDelta = FallTimeout;

			if (_damageFlashCanvasGroup != null)
				_damageFlashCanvasGroup.alpha = 0f;

			// Restart the level timer from zero on respawn
			if (LevelTimer.Instance != null)
				LevelTimer.Instance.StartTimer(LevelTimer.Instance.CurrentLevel);
		}

		private bool TryGetSpawnPoint(out Vector3 spawnPos, out float spawnYaw)
		{
			spawnPos = Vector3.zero;
			spawnYaw = 0f;

			// Prefer explicit player start marker names when present.
			string[] preferredNames = new string[]
			{
				"PlayerSpawn",
				"PlayerSpawnPoint",
				"PlayerStart",
				"StartPoint",
				"RespawnPoint",
				"Respawn"
			};

			for (int i = 0; i < preferredNames.Length; i++)
			{
				GameObject named = GameObject.Find(preferredNames[i]);
				if (named != null)
				{
					spawnPos = named.transform.position + Vector3.up * 0.2f;
					spawnYaw = named.transform.eulerAngles.y;
					return true;
				}
			}

			// Do NOT use generic SpawnPoint1/SpawnPoint names here; those are gate spawner markers.
			return false;
		}

		private void DropAllCollectedGatesAtDeathPosition(Vector3 deathPos)
		{
			InventoryManager inv = InventoryManager.Instance;
			if (inv == null)
				return;

			SimpleGateCollector collector = GetComponent<SimpleGateCollector>();
			if (collector == null)
				collector = FindFirstObjectByType<SimpleGateCollector>();

			GameObject andPrefab = collector != null ? collector.andGatePrefab : null;
			GameObject orPrefab = collector != null ? collector.orGatePrefab : null;
			GameObject notPrefab = collector != null ? collector.notGatePrefab : null;

			int andCount = inv.GetGateCount("AND");
			int orCount = inv.GetGateCount("OR");
			int notCount = inv.GetGateCount("NOT");
			int spawnIndex = 0;

			for (int i = 0; i < andCount; i++)
			{
				if (!inv.RemoveGate("AND")) break;
				SpawnDroppedGateAt(andPrefab, "AND", deathPos, spawnIndex++);
			}

			for (int i = 0; i < orCount; i++)
			{
				if (!inv.RemoveGate("OR")) break;
				SpawnDroppedGateAt(orPrefab, "OR", deathPos, spawnIndex++);
			}

			for (int i = 0; i < notCount; i++)
			{
				if (!inv.RemoveGate("NOT")) break;
				SpawnDroppedGateAt(notPrefab, "NOT", deathPos, spawnIndex++);
			}
		}

		private void SpawnDroppedGateAt(GameObject prefab, string gateType, Vector3 center, int spawnIndex)
		{
			if (prefab == null)
				return;

			float angle = spawnIndex * 0.85f;
			float radius = 0.8f + (0.18f * (spawnIndex % 4));
			Vector3 ringOffset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
			Vector3 probeStart = center + ringOffset + Vector3.up * 2f;
			Vector3 spawnPos = center + ringOffset + Vector3.up * 0.25f;

			if (Physics.Raycast(probeStart, Vector3.down, out RaycastHit floorHit, 5f, ~0, QueryTriggerInteraction.Ignore))
				spawnPos = floorHit.point + Vector3.up * 0.3f;

			GameObject dropped = Instantiate(prefab, spawnPos, Quaternion.identity);
			dropped.name = $"Dropped_{gateType.ToUpper()}_Gate";

			IgnoreDroppedGateWithPlayer(dropped);

			GateSpawnDelay spawnDelay = dropped.AddComponent<GateSpawnDelay>();
			spawnDelay.delay = 1.2f;
		}

		private void IgnoreDroppedGateWithPlayer(GameObject dropped)
		{
			if (dropped == null)
				return;

			Collider[] gateCols = dropped.GetComponentsInChildren<Collider>(true);
			Collider[] playerCols = GetComponentsInChildren<Collider>(true);

			if (gateCols == null || playerCols == null || gateCols.Length == 0 || playerCols.Length == 0)
				return;

			for (int i = 0; i < gateCols.Length; i++)
			{
				Collider gateCol = gateCols[i];
				if (gateCol == null) continue;

				for (int j = 0; j < playerCols.Length; j++)
				{
					Collider playerCol = playerCols[j];
					if (playerCol == null) continue;
					Physics.IgnoreCollision(gateCol, playerCol, true);
				}
			}
		}

		private void TriggerDamageFeedback()
		{
			if (_damageFlashCanvasGroup == null)
				EnsureDamageFlashOverlay();

			if (_damageFlashCanvasGroup != null)
			{
				_damageFlashTimer = Mathf.Max(0.05f, DamageFlashDuration);
				_damageFlashCanvasGroup.alpha = Mathf.Clamp01(DamageFlashMaxAlpha);
			}

			if (CinemachineCameraTarget != null && !_cameraTargetBaseCached)
			{
				_cameraTargetBaseLocalPos = CinemachineCameraTarget.transform.localPosition;
				_cameraTargetBaseCached = true;
			}

			_damageShakeTimer = Mathf.Max(_damageShakeTimer, Mathf.Max(0.05f, DamageShakeDuration));
		}

		/// <summary>Trigger a camera shake without any damage/flash — used by puzzle wrong-answer feedback.</summary>
		public void TriggerCameraShake(float duration = 0.25f, float intensity = 0.12f)
		{
			if (CinemachineCameraTarget != null && !_cameraTargetBaseCached)
			{
				_cameraTargetBaseLocalPos = CinemachineCameraTarget.transform.localPosition;
				_cameraTargetBaseCached = true;
			}
			float safeDuration = Mathf.Max(0.05f, duration);
			_damageShakeTimer = Mathf.Max(_damageShakeTimer, safeDuration);
			// Temporarily override intensity to caller's value for this shake
			DamageShakeIntensity = Mathf.Max(DamageShakeIntensity, intensity);
		}

		private void UpdateDamageFeedback()
		{
			if (_damageFlashCanvasGroup != null)
			{
				if (_damageFlashTimer > 0f)
				{
					_damageFlashTimer -= Time.deltaTime;
					float t = Mathf.Clamp01(_damageFlashTimer / Mathf.Max(0.05f, DamageFlashDuration));
					_damageFlashCanvasGroup.alpha = Mathf.Clamp01(DamageFlashMaxAlpha) * t;
				}
				else if (_damageFlashCanvasGroup.alpha > 0f)
				{
					_damageFlashCanvasGroup.alpha = 0f;
				}
			}

			if (CinemachineCameraTarget == null)
				return;

			if (!_cameraTargetBaseCached)
			{
				_cameraTargetBaseLocalPos = CinemachineCameraTarget.transform.localPosition;
				_cameraTargetBaseCached = true;
			}

			if (_damageShakeTimer > 0f)
			{
				_damageShakeTimer -= Time.deltaTime;
				float t = Mathf.Clamp01(_damageShakeTimer / Mathf.Max(0.05f, DamageShakeDuration));
				float amp = Mathf.Clamp(DamageShakeIntensity, 0f, 0.5f) * t;
				Vector3 jitter = Random.insideUnitSphere * amp;
				CinemachineCameraTarget.transform.localPosition = _cameraTargetBaseLocalPos + jitter;
			}
			else
			{
				CinemachineCameraTarget.transform.localPosition = _cameraTargetBaseLocalPos;
			}
		}
	}
}