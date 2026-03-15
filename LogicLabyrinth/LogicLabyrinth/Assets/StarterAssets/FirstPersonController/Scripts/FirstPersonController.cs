using UnityEngine;
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
		private const float AbsurdCoordinateLimit = 500.0f;
		private const float MinAllowedY = -150.0f;

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

			_tabCursorVisible = false;
			SetGameplayCursorVisible(false);
			ApplyCollisionSafetySettings();
			_lastStableGroundedPosition = transform.position;
			_hasStableGroundedPosition = true;
		}

		private void ApplyCollisionSafetySettings()
		{
			if (_controller == null) return;

			float targetSkin = Mathf.Clamp(SafeSkinWidth, 0.08f, 0.15f);
			if (_controller.skinWidth < targetSkin)
				_controller.skinWidth = targetSkin;

			_controller.stepOffset = Mathf.Clamp(SafeStepOffset, 0.2f, 0.5f);
			_controller.minMoveDistance = 0f;

			Debug.Log($"[FirstPersonController] Collision safety applied: skinWidth={_controller.skinWidth:F3}, stepOffset={_controller.stepOffset:F3}, minMoveDistance={_controller.minMoveDistance:F3}");
		}

		private void Update()
		{
			// If pause is open, clear tab-toggle state so resume returns to normal locked gameplay cursor.
			if (PauseMenuController.IsPaused)
			{
				_tabCursorVisible = false;
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

			if (_antiWarpCooldown > 0f)
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
			_controller.Move(inputDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

			Vector3 postMovePosition = transform.position;
			Vector3 frameDelta = postMovePosition - preMovePosition;
			float verticalDelta = postMovePosition.y - preMovePosition.y;
			float horizontalDelta = new Vector2(frameDelta.x, frameDelta.z).magnitude;

			bool suspiciousVerticalPop =
				verticalDelta > MaxVerticalSnapPerFrame &&
				Grounded &&
				!_input.jump;

			bool suspiciousPositionWarp =
				!_input.jump &&
				(horizontalDelta > MaxHorizontalSnapPerFrame || frameDelta.magnitude > MaxTotalSnapPerFrame);

			if (suspiciousVerticalPop || suspiciousPositionWarp)
			{
				// Restore to the immediate pre-move position to avoid snapping back to an old checkpoint.
				Vector3 restorePos = preMovePosition;

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
				if (controllerVerticalSpeed < 1.5f)
				{
					_lastStableGroundedPosition = transform.position;
					_hasStableGroundedPosition = true;
				}
			}
		}

		private bool TryRecoverFromAbsurdPosition(string reason)
		{
			Vector3 pos = transform.position;
			bool absurd =
				float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z) ||
				Mathf.Abs(pos.x) > AbsurdCoordinateLimit ||
				Mathf.Abs(pos.z) > AbsurdCoordinateLimit ||
				pos.y < MinAllowedY;

			if (!absurd)
				return false;

			Vector3 restorePos = _hasStableGroundedPosition ? _lastStableGroundedPosition : Vector3.zero;

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
	}
}