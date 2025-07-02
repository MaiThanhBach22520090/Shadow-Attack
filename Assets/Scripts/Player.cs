using Fusion;
using Fusion.Addons.KCC;
using System;
using UnityEngine;

public enum AbilityMode : byte
{
    BreakBlock,
    Cage,
    Shove,
}

public class Player : NetworkBehaviour
{
    [SerializeField] private MeshRenderer[] modelParts;

    [SerializeField] private LayerMask lagCompensationLayer;

    [SerializeField] private KCC kcc;
    [SerializeField] private KCCProcessor glideProcessor;
    [SerializeField] private Transform camTarget;
    [SerializeField] private AudioSource source;
    [SerializeField] private AudioClip shoveSound;
    [SerializeField] private float maxPitch = 85f;
    [SerializeField] private float lookSensitivity = 0.15f;
    [SerializeField] private Vector3 jumpImpulse = new(0f, 10f, 0f);
    [SerializeField] private float doubleJumpMultiplier = 0.75f;

    [SerializeField] private float grappleCD = 2f;
    [SerializeField] private float glideCD = 20f;
    [SerializeField] private float doubleJumpCD = 5f;
    [SerializeField] private float breakBlockCD = 1.25f;
    [SerializeField] private float cageCD = 10f;
    [SerializeField] private float shoveCD = 2f;

    [SerializeField] private float shoveStrength = 20f;
    [SerializeField] private float grappleStrength = 12f;
    [SerializeField] private float maxGlideTime = 2f;
    [field: SerializeField] public float AbilityRange { get; private set; } = 25f;

    [SerializeField] private Cage cagePrefab;

    [SerializeField] private Checkpoint _initialCheckpoint;
    [SerializeField] private Example.Teleport.Teleport _teleportProcessor;

    public float GrappleCDFactor => (GrappleCD.RemainingTime(Runner) ?? 0f) / grappleCD;
    public float GlideCDFactor => (GlideCD.RemainingTime(Runner) ?? 0f) / glideCD;
    public float DoubleJumpCDFactor => (DoubleJumpCD.RemainingTime(Runner) ?? 0f) / doubleJumpCD;
    public float BreakBlockCDFactor => (BreakBlockCD.RemainingTime(Runner) ?? 0f) / breakBlockCD;
    public float CageCDFactor => (CageCD.RemainingTime(Runner) ?? 0f) / cageCD;
    public float ShoveCDFactor => (ShoveCD.RemainingTime(Runner) ?? 0f) / shoveCD;

    public Vector3 basePosition;

    // Calculated Distance from the base position to the current position then rounded to 2 decimal places for display purposes.
    public double Score => Math.Round(Vector3.Distance(basePosition, transform.position), 2);
    public bool IsReady; // Server is the only one who cares about this
    private bool CanGlide => !kcc.Data.IsGrounded && GlideCharge > 0f || !IsCaged;
    public AbilityMode SelectedAbility { get; private set; }

    [Networked] public string Name { get; private set; }
    [Networked] public float GlideCharge { get; private set; }
    [Networked] public bool IsGliding { get; private set; }
    [Networked] public bool IsCaged { get; set; }
    [Networked] private TickTimer GrappleCD { get; set; }
    [Networked] private TickTimer GlideCD { get; set; }
    [Networked] private TickTimer DoubleJumpCD { get; set; }
    [Networked] private TickTimer BreakBlockCD { get; set; }
    [Networked] private TickTimer CageCD { get; set; }
    [Networked] private TickTimer ShoveCD { get; set; }
    [Networked] private NetworkButtons PreviousButtons { get; set; }

    [Networked, OnChangedRender(nameof(Jumped))] private int JumpSync { get; set; }
    [Networked, OnChangedRender(nameof(Shoved))] private int ShoveSync { get; set; }

    [Networked] public NetworkId _activeCheckpointId { get; private set; }

    private InputManager inputManager;
    private Vector2 baseLookRotation;
    private float glideDrain;

    public override void Spawned()
    {
        glideDrain = 1f / (maxGlideTime * Runner.TickRate);
        GlideCharge = 1f;

        basePosition = transform.position;

        if (HasInputAuthority)
        {
            foreach (MeshRenderer renderer in modelParts)
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;

            inputManager = Runner.GetComponent<InputManager>();
            inputManager.LocalPlayer = this;
            Name = PlayerPrefs.GetString("Photon.Menu.Username");
            RPC_PlayerName(Name);
            CameraFollow.Singleton.SetTarget(camTarget, this);
            UIManager.Singleton.LocalPlayer = this;
            _teleportProcessor = Runner.GetComponent<Example.Teleport.Teleport>();
        }

        if (Object.HasStateAuthority)
        {
            // When the player object spawns, set its initial active checkpoint.
            if (_initialCheckpoint != null)
            {
                _activeCheckpointId = _initialCheckpoint.Object.Id; // Set the networked ID
                // Immediately teleport the player to the initial checkpoint's location
                // The Teleport method now just adds the processor, which finds the target.
                Debug.Log($"Player {Object.InputAuthority.PlayerId} spawned and set initial checkpoint: {_initialCheckpoint.CheckpointID}");
            }
            else
            {
                Debug.LogWarning($"Player {Object.InputAuthority.PlayerId} spawned without an initial checkpoint assigned! Assign one in the Inspector.");
            }
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (HasInputAuthority)
        {
            CameraFollow.Singleton.SetTarget(null, this);
            UIManager.Singleton.LocalPlayer = null;
        }
    }


    public override void FixedUpdateNetwork()
    {
        if (GetInput(out NetInput input))
        {
            SelectedAbility = input.AbilityMode;
            CheckGlide(input);
            CheckJump(input);
            kcc.AddLookRotation(input.LookDelta * lookSensitivity, -maxPitch, maxPitch);
            UpdateCamTarget();
            Vector3 lookDirection = camTarget.forward;  

            if (input.Buttons.WasPressed(PreviousButtons, InputButton.Grapple))
                TryGrapple(lookDirection    );

            if (IsGliding && !CanGlide)
                ToggleGlide(false);

            SetInputDirection(input);
            CheckAbility(input, lookDirection);
            PreviousButtons = input.Buttons;
            baseLookRotation = kcc.GetLookRotation();
        }
    }

    public override void Render()
    {
        if (kcc.IsPredictingLookRotation)
        {
            Vector2 predictedLookRotation = baseLookRotation + inputManager.AccumulatedMouseDelta * lookSensitivity;
            kcc.SetLookRotation(predictedLookRotation);
        }

        UpdateCamTarget();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!Object.HasStateAuthority)
            return; // Only server/host processes checkpoint activations

        // Try to get the Checkpoint component from the triggered collider's root NetworkObject.
        Checkpoint checkpoint = other.GetComponent<Checkpoint>();
        if (checkpoint != null)
        {
            // Optional: Logic to ensure checkpoints are activated in order or only higher IDs
            // If you want any checkpoint hit to be the new active one, remove the 'if' condition below.
            Runner.TryFindObject(_activeCheckpointId, out NetworkObject activeCheckpointNO);
            Checkpoint currentActiveCheckpoint = activeCheckpointNO?.GetComponent<Checkpoint>();

            // If no checkpoint is active, or the new checkpoint has a higher ID than the current one, activate it.
            // This prevents players from going backwards and activating an earlier checkpoint.
            if (currentActiveCheckpoint == null || checkpoint.CheckpointID > currentActiveCheckpoint.CheckpointID)
            {
                _activeCheckpointId = checkpoint.Object.Id; // Update the networked active checkpoint ID
                checkpoint.Activate(); // Call the Activate method on the checkpoint (visual/audio feedback)
                Debug.Log($"Player {Object.InputAuthority.PlayerId} activated Checkpoint {checkpoint.CheckpointID}");
            }
        }
    }


    public void TeleportToActiveCheckpoint()
    {
        // Attempt to find the NetworkObject associated with the stored _activeCheckpointId.
        if (Runner.TryFindObject(_activeCheckpointId, out NetworkObject checkpointNO))
        {
            Checkpoint activeCheckpoint = checkpointNO.GetComponent<Checkpoint>();
            if (activeCheckpoint != null)
            {
                // Use the Teleport method (which now uses the KCC processor) to move the player.
                Teleport(activeCheckpoint.GetTeleportPosition(), activeCheckpoint.GetTeleportRotation());
                Debug.Log($"Player {Object.InputAuthority.PlayerId} teleported to checkpoint {activeCheckpoint.CheckpointID}");
            }
            else
            {
                Debug.LogError($"Failed to get Checkpoint component from active checkpoint NetworkObject with ID: {_activeCheckpointId}.");
                // Fallback: If the object exists but isn't a Checkpoint (shouldn't happen with correct setup)
                TeleportToInitialCheckpointAsFallback();
            }
        }
        else
        {
            Debug.LogError($"Active checkpoint NetworkObject not found with ID: {_activeCheckpointId}. Was it despawned?");
            // Fallback: If the active checkpoint object itself is not found (e.g., despawned, not loaded additively)
            TeleportToInitialCheckpointAsFallback();
        }
    }

    private void TeleportToInitialCheckpointAsFallback()
    {
        if (_initialCheckpoint != null)
        {
            Teleport(_initialCheckpoint.GetTeleportPosition(), _initialCheckpoint.GetTeleportRotation());
            _activeCheckpointId = _initialCheckpoint.Object.Id; // Reset to initial checkpoint
            Debug.LogWarning("Teleported to initial checkpoint as fallback.");
        }
        else
        {
            // Absolute last resort: teleport to origin (0,0,0) or a default safe spawn point
            Teleport(Vector3.zero, Quaternion.identity);
            Debug.LogError("No valid initial or active checkpoint found. Teleporting to origin.");
        }
    }

    private void CheckGlide(NetInput input)
    {
        if (input.Buttons.WasPressed(PreviousButtons, InputButton.Glide) && GlideCD.ExpiredOrNotRunning(Runner) && CanGlide)
            ToggleGlide(true);
        else if (input.Buttons.WasReleased(PreviousButtons, InputButton.Glide) && IsGliding)
            ToggleGlide(false);
    }

    private void CheckJump(NetInput input)
    {
        if (input.Buttons.WasPressed(PreviousButtons, InputButton.Jump))
        {
            if (kcc.FixedData.IsGrounded)
            {
                kcc.Jump(jumpImpulse);
                JumpSync++;
            }
            else if (DoubleJumpCD.ExpiredOrNotRunning(Runner))
            {
                kcc.Jump(jumpImpulse * doubleJumpMultiplier);
                DoubleJumpCD = TickTimer.CreateFromSeconds(Runner, doubleJumpCD);
                ToggleGlide(false);
                JumpSync++;
            }
        }
    }

    private void SetInputDirection(NetInput input)
    {
        Vector3 worldDirection;
        if (IsGliding)
        {
            GlideCharge = Mathf.Max(0f, GlideCharge - glideDrain);
            worldDirection = kcc.Data.TransformDirection;
        }
        else
            worldDirection = kcc.FixedData.TransformRotation * input.Direction.X0Y();

        kcc.SetInputDirection(worldDirection);
    }

    private void UpdateCamTarget()
    {
        camTarget.localRotation = Quaternion.Euler(kcc.GetLookRotation().x, 0f, 0f);
    }

    private void CheckAbility(NetInput input, Vector3 lookDirection)
    {
        if (!HasStateAuthority || !input.Buttons.WasPressed(PreviousButtons, InputButton.UseAbility))
            return;

        switch (input.AbilityMode)
        {
            case AbilityMode.BreakBlock:
                TryBreakBlock(lookDirection);
                break;
            case AbilityMode.Cage:
                TryCage(lookDirection);
                break;
            case AbilityMode.Shove:
                TryShove(lookDirection);
                break;
            default:
                break;
        }
    }

    public void Cage()
    {
        Runner.Spawn(cagePrefab, transform.position, Quaternion.identity, Object.InputAuthority).Init(this);
        IsCaged = true;
        ToggleGlide(false);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.InputAuthority | RpcTargets.StateAuthority)]
    public void RPC_SetReady()
    {
        IsReady = true;
        if (HasInputAuthority)
            UIManager.Singleton.DidSetReady();
    }

    public void Teleport(Vector3 position, Quaternion rotation)
    {
        kcc.SetPosition(position);
        kcc.SetLookRotation(rotation);
    }

    public void ResetCooldowns()
    {
        GrappleCD = TickTimer.None;
        GlideCD = TickTimer.None;
        DoubleJumpCD = TickTimer.None;
        BreakBlockCD = TickTimer.None;
        CageCD = TickTimer.None;
        ShoveCD = TickTimer.None;
    }

    public void TryCage(Vector3 lookDirection)
    {
        if (CageCD.ExpiredOrNotRunning(Runner) && Runner.LagCompensation.Raycast(camTarget.position, lookDirection, AbilityRange, Object.InputAuthority, out LagCompensatedHit hit, lagCompensationLayer, HitOptions.IncludePhysX | HitOptions.IgnoreInputAuthority))
        {
            if (hit.Hitbox != null && hit.Hitbox.TryGetComponent(out Player player) && player != this && !player.IsCaged)
            {
                CageCD = TickTimer.CreateFromSeconds(Runner, cageCD);
                player.Cage();
            }
        }
    }

    public void TryShove(Vector3 lookDirection)
    {
        if (ShoveCD.ExpiredOrNotRunning(Runner) && Runner.LagCompensation.Raycast(camTarget.position, lookDirection, AbilityRange, Object.InputAuthority, out LagCompensatedHit hit, lagCompensationLayer, HitOptions.IncludePhysX | HitOptions.IgnoreInputAuthority))
        {
            if (hit.Hitbox != null && hit.Hitbox.TryGetComponent(out Player player))
            {
                ShoveCD = TickTimer.CreateFromSeconds(Runner, shoveCD);
                player.Shove(lookDirection, shoveStrength);
            }
        }
    }

    public void TryBreakBlock(Vector3 lookDirection)
    {
        if (BreakBlockCD.ExpiredOrNotRunning(Runner) && Physics.Raycast(camTarget.position, lookDirection, out RaycastHit hitInfo, AbilityRange))
        {
            if (hitInfo.collider.TryGetComponent(out Block block))
            {
                BreakBlockCD = TickTimer.CreateFromSeconds(Runner, breakBlockCD);
                block.Disable();
            }
        }
    }

    private void TryGrapple(Vector3 lookDirection)
    {
        if (GrappleCD.ExpiredOrNotRunning(Runner) && Physics.Raycast(camTarget.position, lookDirection, out RaycastHit hitInfo, AbilityRange))
        {
            if (hitInfo.collider.TryGetComponent(out Block _))
            {
                GrappleCD = TickTimer.CreateFromSeconds(Runner, grappleCD);
                Vector3 grappleVector = Vector3.Normalize(hitInfo.point - transform.position);
                if (grappleVector.y > 0f)
                    grappleVector = Vector3.Normalize(grappleVector + Vector3.up);

                kcc.Jump(grappleVector * grappleStrength);
                ToggleGlide(false);
            }
        }
    }

    private void Shove(Vector3 lookDirection, float shoveStrenght)
    {
        kcc.AddExternalImpulse(lookDirection * shoveStrenght);
        ShoveSync++;
    }

    private void ToggleGlide(bool isGliding)
    {
        if (IsGliding == isGliding)
            return;

        if (isGliding)
        {
            kcc.AddModifier(glideProcessor);
            Vector3 velocity = kcc.Data.DynamicVelocity;
            velocity.y *= 0.25f;
            kcc.SetDynamicVelocity(velocity);
        }
        else
        {
            kcc.RemoveModifier(glideProcessor);
            GlideCharge = 1f;
            GlideCD = TickTimer.CreateFromSeconds(Runner, glideCD);
        }

        IsGliding = isGliding;
    }

    private void Jumped()
    {
        source.Play();
    }

    private void Shoved()
    {
        source.PlayOneShot(shoveSound);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_PlayerName(string name)
    {
        Name = name;
    }
}
