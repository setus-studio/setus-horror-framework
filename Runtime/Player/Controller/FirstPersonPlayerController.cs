using System;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Debugging.Logging;
using Setus.HorrorFramework.Player.Camera;
using Setus.HorrorFramework.Player.Input;
using Setus.HorrorFramework.Player.State;
using Setus.HorrorFramework.UI.Settings;
using UnityEngine;

namespace Setus.HorrorFramework.Player.Controller
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonPlayerController : MonoBehaviour, IPlayerPoseProvider, IPlayerControlStateTarget, IPlayerInputLockTarget, IRestoreStateValidator, IRuntimeRollbackOwner
    {
        [Header("References")]
        [SerializeField] private CharacterController characterController;
        [SerializeField] private FirstPersonCameraRig cameraRig;
        [SerializeField] private MonoBehaviour inputSourceBehaviour;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float walkSpeed = 3.2f;
        [SerializeField, Min(0f)] private float sprintSpeed = 5.1f;
        [SerializeField, Min(0f)] private float crouchSpeed = 1.8f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float groundedVerticalVelocity = -2f;

        [Header("Look")]
        [SerializeField, Min(0f)] private float lookSensitivity = 0.12f;
        [SerializeField] private float minimumPitch = -80f;
        [SerializeField] private float maximumPitch = 80f;

        [Header("Crouch")]
        [SerializeField, Min(0.2f)] private float standingHeight = 1.8f;
        [SerializeField, Min(0.2f)] private float crouchingHeight = 1.2f;
        [SerializeField, Min(0f)] private float crouchTransitionSpeed = 12f;
        [SerializeField] private LayerMask crouchClearanceMask = Physics.DefaultRaycastLayers;
        [SerializeField] private QueryTriggerInteraction crouchClearanceTriggerInteraction = QueryTriggerInteraction.Ignore;

        [Header("Feedback")]
        [SerializeField] private bool headBobEnabled = true;
        [SerializeField, Min(0f)] private float headBobAmplitude = 0.035f;
        [SerializeField, Min(0f)] private float headBobFrequency = 8f;
        [SerializeField, Min(0.1f)] private float footstepDistance = 1.75f;

        [Header("State")]
        [SerializeField] private PlayerControlState initialState = PlayerControlState.Normal;

        private IPlayerInputSource inputSource;
        private PlayerControlStateMachine stateMachine;
        private float verticalVelocity;
        private float travelledSinceFootstep;
        private float headBobTime;
        private bool isCrouching;
        private bool gameplayInputLocked;
        private bool sprintToggled;
        private bool sprintWasHeld;
        private bool sprintModeInitialized;
        private SprintInputMode lastSprintInputMode = SprintInputMode.Hold;
        private readonly Collider[] crouchClearanceHits = new Collider[8];

        public event Action<PlayerFootstepEmitted> FootstepEmitted;

        public string StateKey => "player.pose";
        public Type StateType => typeof(PlayerPose);
        public PlayerControlStateMachine StateMachine => stateMachine;
        public PlayerControlState ControlState => stateMachine.CurrentState;
        public PlayerControlState CurrentControlState => ControlState;
        public PlayerPose CurrentPose => CaptureState();

        private void Awake()
        {
            ResolveReferences();
            stateMachine = new PlayerControlStateMachine(initialState);

            standingHeight = Mathf.Max(standingHeight, characterController.height);
            crouchingHeight = Mathf.Min(crouchingHeight, standingHeight);
        }

        private void OnEnable()
        {
            HorrorGameContext.Active?.PauseFlow.SetPlayerTarget(this);
            ApplyCursorState();
        }

        private void OnDisable()
        {
            HorrorGameContext.Active?.PauseFlow.ClearPlayerTarget(this);

            if (cameraRig != null)
            {
                cameraRig.ResetTransientOffsets();
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            sprintToggled = false;
            sprintWasHeld = false;
            sprintModeInitialized = false;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || gameplayInputLocked)
            {
                return;
            }

            var input = inputSource != null ? inputSource.ReadInput() : default;
            var policy = stateMachine.CurrentPolicy;

            ApplyLook(input.Look, policy);
            ApplyMovement(input, policy, deltaTime);
            ApplyCursorState();
        }

        public bool SetControlState(PlayerControlState state)
        {
            var changed = stateMachine.SetState(state);
            ApplyCursorState();
            return changed;
        }

        public void SetGameplayInputLocked(bool isLocked)
        {
            if (gameplayInputLocked == isLocked)
            {
                return;
            }

            gameplayInputLocked = isLocked;
            if (!gameplayInputLocked)
            {
                ApplyCursorState();
            }
        }

        public PlayerPose CaptureState()
        {
            return new PlayerPose(
                transform.position,
                transform.rotation,
                cameraRig != null ? cameraRig.Pitch : 0f,
                PlayerControlStatePolicy.NormalizeForPersistence(ControlState));
        }

        public void RestoreState(PlayerPose state)
        {
            RestorePose(state);
        }

        public void RestoreState(object state)
        {
            if (state is PlayerPose pose)
            {
                RestorePose(pose);
                return;
            }

            throw new ArgumentException($"Expected {nameof(PlayerPose)} for player restore.", nameof(state));
        }

        public RestoreStateValidationResult ValidateRestoreState(object state)
        {
            if (!(state is PlayerPose pose))
            {
                return RestoreStateValidationResult.Invalid($"Expected {nameof(PlayerPose)}.");
            }

            if (!IsFinite(pose.Position) ||
                !IsFinite(pose.Rotation) ||
                Quaternion.Dot(pose.Rotation, pose.Rotation) <= Mathf.Epsilon ||
                float.IsNaN(pose.CameraPitch) ||
                float.IsInfinity(pose.CameraPitch))
            {
                return RestoreStateValidationResult.Invalid(
                    "Player pose must contain finite position, rotation, and camera pitch values.");
            }

            if (!Enum.IsDefined(typeof(PlayerControlState), pose.ControlState) ||
                PlayerControlStatePolicy.NormalizeForPersistence(pose.ControlState) != pose.ControlState)
            {
                return RestoreStateValidationResult.Invalid(
                    $"Player control state '{pose.ControlState}' is not supported in persistent state.");
            }

            return RestoreStateValidationResult.Success;
        }

        object IRuntimeStateOwner.CaptureState()
        {
            return CaptureState();
        }

        public Action CaptureRollbackAction()
        {
            var rollbackPosition = transform.position;
            var rollbackRotation = transform.rotation;
            var rollbackControlState = ControlState;
            var rollbackCamera = cameraRig?.CaptureRollbackAction();
            var rollbackControllerEnabled = characterController != null && characterController.enabled;
            var rollbackControllerHeight = characterController != null ? characterController.height : 0f;
            var rollbackControllerCenter = characterController != null ? characterController.center : Vector3.zero;
            var rollbackVerticalVelocity = verticalVelocity;
            var rollbackTravelledSinceFootstep = travelledSinceFootstep;
            var rollbackHeadBobTime = headBobTime;
            var rollbackIsCrouching = isCrouching;
            var rollbackGameplayInputLocked = gameplayInputLocked;
            var rollbackSprintToggled = sprintToggled;
            var rollbackSprintWasHeld = sprintWasHeld;
            var rollbackSprintModeInitialized = sprintModeInitialized;
            var rollbackLastSprintInputMode = lastSprintInputMode;
            var rollbackCursorLockState = Cursor.lockState;
            var rollbackCursorVisible = Cursor.visible;

            return () =>
            {
                if (characterController != null)
                {
                    characterController.enabled = false;
                }

                transform.SetPositionAndRotation(rollbackPosition, rollbackRotation);
                rollbackCamera?.Invoke();
                stateMachine.RestoreStateWithoutNotification(rollbackControlState);
                verticalVelocity = rollbackVerticalVelocity;
                travelledSinceFootstep = rollbackTravelledSinceFootstep;
                headBobTime = rollbackHeadBobTime;
                isCrouching = rollbackIsCrouching;
                gameplayInputLocked = rollbackGameplayInputLocked;
                sprintToggled = rollbackSprintToggled;
                sprintWasHeld = rollbackSprintWasHeld;
                sprintModeInitialized = rollbackSprintModeInitialized;
                lastSprintInputMode = rollbackLastSprintInputMode;

                if (characterController != null)
                {
                    characterController.height = rollbackControllerHeight;
                    characterController.center = rollbackControllerCenter;
                    characterController.enabled = rollbackControllerEnabled;
                }

                Cursor.lockState = rollbackCursorLockState;
                Cursor.visible = rollbackCursorVisible;
                Physics.SyncTransforms();
            };
        }

        public void RestorePose(PlayerPose pose)
        {
            var wasEnabled = characterController != null && characterController.enabled;
            if (characterController != null)
            {
                characterController.enabled = false;
            }

            transform.SetPositionAndRotation(pose.Position, pose.Rotation);
            if (cameraRig != null)
            {
                cameraRig.SetPitch(Mathf.Clamp(pose.CameraPitch, minimumPitch, maximumPitch));
                cameraRig.ResetTransientOffsets();
            }

            verticalVelocity = 0f;
            travelledSinceFootstep = 0f;
            headBobTime = 0f;
            SetControlState(PlayerControlStatePolicy.NormalizeForPersistence(pose.ControlState));

            if (characterController != null)
            {
                characterController.enabled = wasEnabled;
            }

            Physics.SyncTransforms();
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                !float.IsNaN(value.z) && !float.IsInfinity(value.z) &&
                !float.IsNaN(value.w) && !float.IsInfinity(value.w);
        }

        private void ApplyLook(Vector2 look, PlayerControlStatePolicy policy)
        {
            if (!policy.CanLook)
            {
                return;
            }

            var sensitivity = HorrorGameContext.Active?.RuntimeSettings.Current.MouseSensitivity ?? 1f;
            var yaw = look.x * lookSensitivity * sensitivity;
            if (!Mathf.Approximately(yaw, 0f))
            {
                transform.Rotate(Vector3.up, yaw, Space.Self);
            }

            if (cameraRig == null)
            {
                return;
            }

            var pitch = Mathf.Clamp(
                cameraRig.Pitch - look.y * lookSensitivity * sensitivity,
                minimumPitch,
                maximumPitch);
            cameraRig.SetPitch(pitch);
        }

        private void ApplyMovement(PlayerInputSnapshot input, PlayerControlStatePolicy policy, float deltaTime)
        {
            if (characterController == null)
            {
                return;
            }

            var grounded = characterController.isGrounded;
            if (grounded && verticalVelocity < 0f)
            {
                verticalVelocity = groundedVerticalVelocity;
            }
            else
            {
                verticalVelocity += gravity * deltaTime;
            }

            var horizontalVelocity = Vector3.zero;
            if (policy.CanMove)
            {
                if (input.CrouchHeld)
                {
                    isCrouching = true;
                }
                else if (CanStandUp())
                {
                    isCrouching = false;
                }

                var speed = isCrouching ? crouchSpeed : ResolveSprint(input.SprintHeld) ? sprintSpeed : walkSpeed;
                var move = Vector2.ClampMagnitude(input.Move, 1f);
                horizontalVelocity = (transform.right * move.x + transform.forward * move.y) * speed;
            }

            UpdateCrouch(deltaTime);

            var motion = (horizontalVelocity + Vector3.up * verticalVelocity) * deltaTime;
            characterController.Move(motion);
            UpdateFeedback(horizontalVelocity, grounded, deltaTime);
        }

        private void UpdateCrouch(float deltaTime)
        {
            if (!isCrouching && !CanStandUp())
            {
                isCrouching = true;
            }

            var targetHeight = isCrouching ? crouchingHeight : standingHeight;
            characterController.height = Mathf.MoveTowards(
                characterController.height,
                targetHeight,
                crouchTransitionSpeed * deltaTime);
            characterController.center = Vector3.up * characterController.height * 0.5f;

            if (cameraRig != null)
            {
                cameraRig.SetPostureOffset(Vector3.up * (characterController.height - standingHeight));
            }
        }

        private bool CanStandUp()
        {
            if (characterController == null ||
                characterController.height >= standingHeight - 0.001f)
            {
                return true;
            }

            var radius = Mathf.Min(characterController.radius, standingHeight * 0.5f);
            var localCenter = new Vector3(
                characterController.center.x,
                0f,
                characterController.center.z);
            // Only query the volume added above the crouched capsule. Querying the full standing
            // capsule would treat the ground beneath an already-valid controller as an obstruction.
            var bottomHeight = Mathf.Max(radius, characterController.height - radius);
            var topHeight = standingHeight - radius;
            var top = transform.TransformPoint(localCenter + Vector3.up * topHeight);
            var bottom = transform.TransformPoint(localCenter + Vector3.up * bottomHeight);
            var hitCount = Physics.OverlapCapsuleNonAlloc(
                bottom,
                top,
                radius,
                crouchClearanceHits,
                crouchClearanceMask,
                crouchClearanceTriggerInteraction);

            if (hitCount >= crouchClearanceHits.Length)
            {
                return false;
            }

            for (var i = 0; i < hitCount; i++)
            {
                var hit = crouchClearanceHits[i];
                crouchClearanceHits[i] = null;
                if (hit != null && !hit.transform.IsChildOf(transform))
                {
                    return false;
                }
            }

            return true;
        }

        private void UpdateFeedback(Vector3 horizontalVelocity, bool grounded, float deltaTime)
        {
            var horizontalSpeed = horizontalVelocity.magnitude;
            var shouldAnimateSteps = grounded && horizontalSpeed > 0.05f;

            if (!shouldAnimateSteps)
            {
                if (cameraRig != null)
                {
                    cameraRig.SetHeadBobOffset(Vector3.zero);
                }
                return;
            }

            var travelled = horizontalSpeed * deltaTime;
            travelledSinceFootstep += travelled;
            if (travelledSinceFootstep >= footstepDistance)
            {
                var footstep = new PlayerFootstepEmitted(transform.position, travelledSinceFootstep);
                travelledSinceFootstep = 0f;
                FootstepEmitted?.Invoke(footstep);
                HorrorGameContext.Active?.Events.Publish(footstep);
            }

            var settings = HorrorGameContext.Active?.RuntimeSettings.Current;
            var runtimeHeadBobEnabled = settings?.HeadBobEnabled ?? true;
            var runtimeHeadBobIntensity = settings?.HeadBobIntensity ?? 1f;
            if (!headBobEnabled || !runtimeHeadBobEnabled || cameraRig == null || runtimeHeadBobIntensity <= 0f)
            {
                cameraRig?.SetHeadBobOffset(Vector3.zero);
                return;
            }

            headBobTime += deltaTime * headBobFrequency * Mathf.Max(1f, horizontalSpeed);
            cameraRig.SetHeadBobOffset(
                Vector3.up * (Mathf.Sin(headBobTime) * headBobAmplitude * runtimeHeadBobIntensity));
        }

        private bool ResolveSprint(bool sprintHeld)
        {
            var mode = HorrorGameContext.Active?.RuntimeSettings.Current.SprintInputMode ?? SprintInputMode.Hold;
            if (!sprintModeInitialized)
            {
                sprintModeInitialized = true;
                lastSprintInputMode = mode;
                sprintToggled = false;
                sprintWasHeld = false;
            }
            else if (mode != lastSprintInputMode)
            {
                lastSprintInputMode = mode;
                sprintToggled = false;
                sprintWasHeld = sprintHeld;
            }

            if (mode == SprintInputMode.Toggle)
            {
                if (sprintHeld && !sprintWasHeld)
                {
                    sprintToggled = !sprintToggled;
                }

                sprintWasHeld = sprintHeld;
                return sprintToggled;
            }

            sprintWasHeld = sprintHeld;
            sprintToggled = false;
            return sprintHeld;
        }

        private void ApplyCursorState()
        {
            if (gameplayInputLocked)
            {
                return;
            }

            var policy = stateMachine.CurrentPolicy;
            Cursor.lockState = policy.CursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !policy.CursorLocked;
        }

        private void ResolveReferences()
        {
            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }

            if (cameraRig == null)
            {
                cameraRig = GetComponentInChildren<FirstPersonCameraRig>(true);
            }

            inputSource = inputSourceBehaviour as IPlayerInputSource;
            if (inputSource != null)
            {
                return;
            }

            var behaviours = GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPlayerInputSource source)
                {
                    inputSource = source;
                    return;
                }
            }

            HorrorGameContext.Active?.Logger.Warning(
                HorrorLogCategory.Player,
                "FirstPersonPlayerController has no IPlayerInputSource; movement input will be zero.");
        }
    }
}
