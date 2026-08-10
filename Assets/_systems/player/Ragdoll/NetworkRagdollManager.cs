using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;

public sealed class NetworkRagdollManager : NetworkBehaviour
{
	[Header("References")]
	[SerializeField]
	private Animator animator;

	[Tooltip("Usually the humanoid Hips transform.")]
	[SerializeField]
	private Transform ragdollRoot;

	[SerializeField]
	private Rigidbody[] ragdollBodies = Array.Empty<Rigidbody>();

	[Tooltip("Only include the colliders used by the ragdoll bones.")]
	[SerializeField]
	private Collider[] ragdollColliders = Array.Empty<Collider>();

	[Tooltip(
		"Components disabled while ragdolled, such as PlayerMovement, " +
		"MouseLook or CharacterAnimator.")]
	[SerializeField]
	private Behaviour[] behavioursToDisable = Array.Empty<Behaviour>();

	[Tooltip(
		"Animated character colliders disabled while ragdolled, " +
		"such as the CharacterController or main CapsuleCollider.")]
	[SerializeField]
	private Collider[] animatedColliders = Array.Empty<Collider>();

	[Header("Client Ragdoll")]
	[Tooltip(
		"Enables kinematic ragdoll colliders on clients. Leave disabled " +
		"when all collision and hit detection is performed by the server.")]
	[SerializeField]
	private bool enableClientRagdollColliders;

	[Header("Networking")]
	[SerializeField, Range(1f, 30f)]
	private float snapshotsPerSecond = 15f;

	private readonly SyncVar<bool> ragdollEnabled = new();

	private Vector3[] initialLocalPositions;
	private Quaternion[] initialLocalRotations;

	private Vector3[] serverPositions;
	private Quaternion[] serverRotations;

	private Vector3[] interpolationStartPositions;
	private Quaternion[] interpolationStartRotations;

	private Vector3[] interpolationTargetPositions;
	private Quaternion[] interpolationTargetRotations;

	private bool[] previousBehaviourStates;
	private bool[] previousAnimatedColliderStates;
	private bool previousAnimatorState;

	private bool localRagdollEnabled;
	private bool hasReceivedSnapshot;
	private bool hasInterpolationTarget;

	private float snapshotTimer;
	private float interpolationTimer;
	private float interpolationDuration;

	private uint serverSequence;
	private uint lastReceivedSequence;

	public bool IsRagdolled => ragdollEnabled.Value;

	public event Action<bool> OnRagdollStateChanged;

	private void Awake()
	{
		AutoFindReferences();
		SortBodiesParentFirst();
		CreateBuffers();
		InitializeAnimatedState();
	}

	public override void OnStartNetwork()
	{
		base.OnStartNetwork();

		ragdollEnabled.OnChange += HandleRagdollStateChanged;
	}

	public override void OnStartServer()
	{
		base.OnStartServer();

		ragdollEnabled.Value = false;
		ApplyRagdollState(false);
	}

	public override void OnStartClient()
	{
		base.OnStartClient();

		ApplyRagdollState(ragdollEnabled.Value);
	}

	public override void OnStopNetwork()
	{
		ragdollEnabled.OnChange -= HandleRagdollStateChanged;

		base.OnStopNetwork();
	}

	private void FixedUpdate()
	{
		if (!IsServerStarted || !ragdollEnabled.Value)
			return;

		float interval = 1f / snapshotsPerSecond;

		snapshotTimer += Time.fixedDeltaTime;

		if (snapshotTimer < interval)
			return;

		snapshotTimer %= interval;

		SendRagdollSnapshot(Channel.Unreliable);
	}

	private void LateUpdate()
	{
		// The host uses its authoritative server physics directly.
		if (IsServerStarted)
			return;

		if (!localRagdollEnabled || !hasInterpolationTarget)
			return;

		interpolationTimer += Time.deltaTime;

		float t = interpolationDuration > 0f
			? Mathf.Clamp01(interpolationTimer / interpolationDuration)
			: 1f;

		for (int i = 0; i < ragdollBodies.Length; i++)
		{
			Rigidbody body = ragdollBodies[i];

			if (body == null)
				continue;

			body.position = Vector3.Lerp(
				interpolationStartPositions[i],
				interpolationTargetPositions[i],
				t);

			body.rotation = Quaternion.Slerp(
				interpolationStartRotations[i],
				interpolationTargetRotations[i],
				t);
		}
	}

	/// <summary>
	/// Enables the ragdoll and applies an impulse.
	/// This must only be called by server-side damage or death logic.
	/// </summary>
	[Server]
	public void EnableRagdoll(
		Vector3 hitPosition,
		Vector3 impulse)
	{
		if (!ragdollEnabled.Value)
		{
			ragdollEnabled.Value = true;

			// Apply immediately on the server rather than waiting for
			// the SyncVar callback.
			ApplyRagdollState(true);

			snapshotTimer = 0f;
			serverSequence = 0;

			// Send the initial pose reliably.
			SendRagdollSnapshot(Channel.Reliable);
		}

		ApplyImpulse(hitPosition, impulse);
	}

	/// <summary>
	/// Enables the ragdoll without applying an impact force.
	/// </summary>
	[Server]
	public void EnableRagdoll()
	{
		EnableRagdoll(transform.position, Vector3.zero);
	}

	/// <summary>
	/// Applies another impulse to an already active ragdoll.
	/// </summary>
	[Server]
	public void AddImpulse(
		Vector3 hitPosition,
		Vector3 impulse)
	{
		if (!ragdollEnabled.Value)
			return;

		ApplyImpulse(hitPosition, impulse);
	}

	/// <summary>
	/// Disables the ragdoll at the character's current root position.
	/// </summary>
	[Server]
	public void DisableRagdoll()
	{
		DisableRagdoll(
			transform.position,
			transform.rotation);
	}

	/// <summary>
	/// Disables the ragdoll and places the animated character at the
	/// supplied position and rotation.
	/// </summary>
	[Server]
	public void DisableRagdoll(
		Vector3 rootPosition,
		Quaternion rootRotation)
	{
		if (!ragdollEnabled.Value)
			return;

		transform.SetPositionAndRotation(
			rootPosition,
			rootRotation);

		ragdollEnabled.Value = false;

		ApplyRagdollState(false);
	}

	[Server]
	private void ApplyImpulse(
		Vector3 hitPosition,
		Vector3 impulse)
	{
		if (impulse.sqrMagnitude <= 0f)
			return;

		Rigidbody closestBody = FindClosestBody(hitPosition);

		if (closestBody == null || closestBody.isKinematic)
			return;

		closestBody.AddForceAtPosition(
			impulse,
			hitPosition,
			ForceMode.Impulse);
	}

	private Rigidbody FindClosestBody(Vector3 position)
	{
		Rigidbody closestBody = null;
		float closestDistance = float.PositiveInfinity;

		foreach (Rigidbody body in ragdollBodies)
		{
			if (body == null)
				continue;

			float distance =
				(body.worldCenterOfMass - position).sqrMagnitude;

			if (distance >= closestDistance)
				continue;

			closestDistance = distance;
			closestBody = body;
		}

		return closestBody;
	}

	[Server]
	private void SendRagdollSnapshot(Channel channel)
	{
		if (ragdollBodies.Length == 0)
			return;

		for (int i = 0; i < ragdollBodies.Length; i++)
		{
			Rigidbody body = ragdollBodies[i];

			if (body == null)
				continue;

			serverPositions[i] = body.position;
			serverRotations[i] = body.rotation;
		}

		serverSequence++;

		ReceiveRagdollSnapshotRpc(
			serverPositions,
			serverRotations,
			serverSequence,
			channel);
	}

	[ObserversRpc]
	private void ReceiveRagdollSnapshotRpc(
		Vector3[] positions,
		Quaternion[] rotations,
		uint sequence,
		Channel channel = Channel.Unreliable)
	{
		// The host already has the real server-simulated rigidbodies.
		if (IsServerStarted)
			return;

		// Ignore delayed snapshots after the ragdoll has been disabled.
		if (!ragdollEnabled.Value || !localRagdollEnabled)
			return;

		if (positions == null ||
			rotations == null ||
			positions.Length != ragdollBodies.Length ||
			rotations.Length != ragdollBodies.Length)
		{
			return;
		}

		if (hasReceivedSnapshot &&
			unchecked((int)(sequence - lastReceivedSequence)) <= 0)
		{
			return;
		}

		lastReceivedSequence = sequence;

		if (!hasReceivedSnapshot)
		{
			hasReceivedSnapshot = true;

			ApplySnapshotImmediately(
				positions,
				rotations);

			return;
		}

		for (int i = 0; i < ragdollBodies.Length; i++)
		{
			Rigidbody body = ragdollBodies[i];

			if (body == null)
				continue;

			interpolationStartPositions[i] = body.position;
			interpolationStartRotations[i] = body.rotation;

			interpolationTargetPositions[i] = positions[i];
			interpolationTargetRotations[i] = rotations[i];
		}

		interpolationTimer = 0f;
		interpolationDuration = 1f / snapshotsPerSecond;
		hasInterpolationTarget = true;
	}

	private void ApplySnapshotImmediately(
		Vector3[] positions,
		Quaternion[] rotations)
	{
		for (int i = 0; i < ragdollBodies.Length; i++)
		{
			Rigidbody body = ragdollBodies[i];

			if (body == null)
				continue;

			body.position = positions[i];
			body.rotation = rotations[i];

			interpolationStartPositions[i] = positions[i];
			interpolationStartRotations[i] = rotations[i];

			interpolationTargetPositions[i] = positions[i];
			interpolationTargetRotations[i] = rotations[i];
		}

		interpolationTimer = 0f;
		hasInterpolationTarget = false;
	}

	private void HandleRagdollStateChanged(
		bool previous,
		bool next,
		bool asServer)
	{
		// Server state is applied immediately by EnableRagdoll and
		// DisableRagdoll.
		if (asServer)
			return;

		ApplyRagdollState(next);
	}

	private void ApplyRagdollState(bool enabled)
	{
		if (localRagdollEnabled == enabled)
			return;

		localRagdollEnabled = enabled;

		if (enabled)
			EnterRagdollState();
		else
			ExitRagdollState();

		OnRagdollStateChanged?.Invoke(enabled);
	}

	private void EnterRagdollState()
	{
		CaptureAnimatedComponentStates();

		if (animator != null)
			animator.enabled = false;

		foreach (Behaviour behaviour in behavioursToDisable)
		{
			if (behaviour != null)
				behaviour.enabled = false;
		}

		foreach (Collider animatedCollider in animatedColliders)
		{
			if (animatedCollider != null)
				animatedCollider.enabled = false;
		}

		bool simulatePhysics = IsServerStarted;
		bool enableColliders =
			simulatePhysics || enableClientRagdollColliders;

		foreach (Collider ragdollCollider in ragdollColliders)
		{
			if (ragdollCollider != null)
				ragdollCollider.enabled = enableColliders;
		}

		foreach (Rigidbody body in ragdollBodies)
		{
			if (body == null)
				continue;

			if (!simulatePhysics && !body.isKinematic)
			{
				body.linearVelocity = Vector3.zero;
				body.angularVelocity = Vector3.zero;
			}

			body.isKinematic = !simulatePhysics;
			body.useGravity = simulatePhysics;
			body.detectCollisions = enableColliders;

			if (simulatePhysics)
				body.WakeUp();
		}

		hasReceivedSnapshot = false;
		hasInterpolationTarget = false;
		interpolationTimer = 0f;
	}

	private void ExitRagdollState()
	{
		hasReceivedSnapshot = false;
		hasInterpolationTarget = false;
		interpolationTimer = 0f;

		foreach (Rigidbody body in ragdollBodies)
		{
			if (body == null)
				continue;

			if (!body.isKinematic)
			{
				body.linearVelocity = Vector3.zero;
				body.angularVelocity = Vector3.zero;
			}

			body.isKinematic = true;
			body.useGravity = false;
			body.detectCollisions = false;
			body.Sleep();
		}

		foreach (Collider ragdollCollider in ragdollColliders)
		{
			if (ragdollCollider != null)
				ragdollCollider.enabled = false;
		}

		ResetBonePose();

		if (animator != null)
			animator.enabled = previousAnimatorState;

		for (int i = 0; i < behavioursToDisable.Length; i++)
		{
			Behaviour behaviour = behavioursToDisable[i];

			if (behaviour != null &&
				i < previousBehaviourStates.Length)
			{
				behaviour.enabled =
					previousBehaviourStates[i];
			}
		}

		for (int i = 0; i < animatedColliders.Length; i++)
		{
			Collider animatedCollider = animatedColliders[i];

			if (animatedCollider != null &&
				i < previousAnimatedColliderStates.Length)
			{
				animatedCollider.enabled =
					previousAnimatedColliderStates[i];
			}
		}
	}

	private void CaptureAnimatedComponentStates()
	{
		previousAnimatorState =
			animator != null && animator.enabled;

		for (int i = 0; i < behavioursToDisable.Length; i++)
		{
			Behaviour behaviour = behavioursToDisable[i];

			previousBehaviourStates[i] =
				behaviour != null && behaviour.enabled;
		}

		for (int i = 0; i < animatedColliders.Length; i++)
		{
			Collider animatedCollider = animatedColliders[i];

			previousAnimatedColliderStates[i] =
				animatedCollider != null &&
				animatedCollider.enabled;
		}
	}

	private void ResetBonePose()
	{
		for (int i = 0; i < ragdollBodies.Length; i++)
		{
			Rigidbody body = ragdollBodies[i];

			if (body == null)
				continue;

			body.transform.localPosition =
				initialLocalPositions[i];

			body.transform.localRotation =
				initialLocalRotations[i];
		}
	}

	private void InitializeAnimatedState()
	{
		foreach (Rigidbody body in ragdollBodies)
		{
			if (body == null)
				continue;

			if (!body.isKinematic)
			{
				body.linearVelocity = Vector3.zero;
				body.angularVelocity = Vector3.zero;
			}

			body.isKinematic = true;
			body.useGravity = false;
			body.detectCollisions = false;
		}

		foreach (Collider ragdollCollider in ragdollColliders)
		{
			if (ragdollCollider != null)
				ragdollCollider.enabled = false;
		}
	}

	private void CreateBuffers()
	{
		int count = ragdollBodies.Length;

		initialLocalPositions = new Vector3[count];
		initialLocalRotations = new Quaternion[count];

		serverPositions = new Vector3[count];
		serverRotations = new Quaternion[count];

		interpolationStartPositions = new Vector3[count];
		interpolationStartRotations = new Quaternion[count];

		interpolationTargetPositions = new Vector3[count];
		interpolationTargetRotations = new Quaternion[count];

		previousBehaviourStates =
			new bool[behavioursToDisable.Length];

		previousAnimatedColliderStates =
			new bool[animatedColliders.Length];

		for (int i = 0; i < count; i++)
		{
			Rigidbody body = ragdollBodies[i];

			if (body == null)
				continue;

			initialLocalPositions[i] =
				body.transform.localPosition;

			initialLocalRotations[i] =
				body.transform.localRotation;
		}
	}

	private void AutoFindReferences()
	{
		if (animator == null)
			animator = GetComponentInChildren<Animator>(true);

		if (ragdollRoot == null &&
			animator != null &&
			animator.isHuman)
		{
			ragdollRoot =
				animator.GetBoneTransform(
					HumanBodyBones.Hips);
		}

		if (ragdollRoot == null)
			ragdollRoot = transform;

		if (ragdollBodies == null ||
			ragdollBodies.Length == 0)
		{
			ragdollBodies =
				ragdollRoot.GetComponentsInChildren<Rigidbody>(
					true);
		}

		if (ragdollColliders == null ||
			ragdollColliders.Length == 0)
		{
			ragdollColliders =
				ragdollRoot.GetComponentsInChildren<Collider>(
					true);
		}
	}

	private void SortBodiesParentFirst()
	{
		Array.Sort(
			ragdollBodies,
			CompareBodyHierarchyDepth);
	}

	private static int CompareBodyHierarchyDepth(
		Rigidbody first,
		Rigidbody second)
	{
		if (first == second)
			return 0;

		if (first == null)
			return 1;

		if (second == null)
			return -1;

		return GetHierarchyDepth(first.transform)
			.CompareTo(
				GetHierarchyDepth(second.transform));
	}

	private static int GetHierarchyDepth(Transform target)
	{
		int depth = 0;

		while (target != null)
		{
			depth++;
			target = target.parent;
		}

		return depth;
	}

	[ContextMenu("Auto Assign Ragdoll Parts")]
	private void AutoAssignRagdollParts()
	{
		if (animator == null)
			animator = GetComponentInChildren<Animator>(true);

		if (animator != null && animator.isHuman)
		{
			ragdollRoot =
				animator.GetBoneTransform(
					HumanBodyBones.Hips);
		}

		if (ragdollRoot == null)
			ragdollRoot = transform;

		ragdollBodies =
			ragdollRoot.GetComponentsInChildren<Rigidbody>(
				true);

		ragdollColliders =
			ragdollRoot.GetComponentsInChildren<Collider>(
				true);
	}
}