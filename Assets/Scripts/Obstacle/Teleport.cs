namespace Example.Teleport
{
    using UnityEngine;
    using Fusion;
    using Fusion.Addons.KCC;
    using System.Linq; // For .FirstOrDefault()

    /// <summary>
    /// Interface to notify other processors about teleport event.
    /// </summary>
    public interface ITeleportListener
    {
        void OnTeleport(KCC kcc, KCCData data);
    }

    /// <summary>
    /// Example processor - finds the player's active checkpoint from its targets
    /// and teleports the KCC to that position, optionally setting look rotation and resetting velocities.
    /// </summary>
    public sealed class Teleport : KCCProcessor
    {
        // PRIVATE MEMBERS

        // IMPORTANT: Drag ALL Checkpoint GameObjects from your scene into this array in the Inspector.
        [SerializeField]
        private Checkpoint[] _targets;
        [SerializeField]
        private bool _setLookRotation;
        [SerializeField]
        private bool _resetDynamicVelocity;
        [SerializeField]
        private bool _resetKinematicVelocity;

        // KCCProcessor INTERFACE

        public override void OnEnter(KCC kcc, KCCData data)
        {
            // Teleport only in fixed update to not introduce glitches caused by incorrect render prediction.
            if (kcc.IsInFixedUpdate == false)
                return;

            if (_targets.Length == 0)
            {
                Debug.LogError($"[{nameof(Teleport)}] Missing target checkpoints on {name}. Please assign all Checkpoint GameObjects to the '_targets' array in the Inspector.", gameObject);
                return;
            }

            // This processor is activated by a specific player's KCC.
            // We need to find that player's active checkpoint.
            Player player = kcc.GetComponent<Player>();
            if (player == null)
            {
                Debug.LogError($"[{nameof(Teleport)}] KCC does not have a Player component attached. Cannot determine active checkpoint.", kcc.gameObject);
                return;
            }

            // Find the active checkpoint using the player's _activeCheckpointId
            Checkpoint targetCheckpoint = null;
            // First, try to find it efficiently using Runner.TryFindObject, as this is the most direct way
            if (player._activeCheckpointId.IsValid) // Check if the NetworkId is valid
            {
                if (kcc.Runner.TryFindObject(player._activeCheckpointId, out NetworkObject foundNO))
                {
                    targetCheckpoint = foundNO.GetComponent<Checkpoint>();
                }
            }

            // If not found via NetworkId (e.g., first spawn before a networked ID is assigned, or ID became invalid)
            // or if player's _activeCheckpointId is not valid, use the initial checkpoint from the _targets array.
            // This fallback assumes _targets[0] is your designated initial/default spawn.
            if (targetCheckpoint == null)
            {
                Debug.LogWarning($"[{nameof(Teleport)}] Player {player.Object.InputAuthority.PlayerId} active checkpoint (ID: {player._activeCheckpointId}) not found via NetworkRunner. Falling back to the first target in array (_targets[0]).");
                targetCheckpoint = _targets[0]; // Fallback to the first assigned target
                if (targetCheckpoint == null) // Double check if _targets[0] is also null
                {
                    Debug.LogError($"[{nameof(Teleport)}] Fallback target _targets[0] is null. Cannot teleport.", gameObject);
                    return;
                }
            }


            // Use the position and rotation from the resolved targetCheckpoint
            Vector3 teleportPosition = targetCheckpoint.GetTeleportPosition();
            Quaternion teleportRotation = targetCheckpoint.GetTeleportRotation();

            kcc.SetPosition(teleportPosition);

            if (_setLookRotation == true)
            {
                kcc.SetLookRotation(teleportRotation, true); // The 'true' here ensures it's instantly set
            }

            if (_resetDynamicVelocity == true)
            {
                kcc.SetDynamicVelocity(Vector3.zero);
            }

            if (_resetKinematicVelocity == true)
            {
                kcc.SetKinematicVelocity(Vector3.zero);
            }

            // Notify all listeners.
            foreach (ITeleportListener listener in kcc.GetProcessors<ITeleportListener>(true))
            {
                try
                {
                    listener.OnTeleport(kcc, data);
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }
    }
}