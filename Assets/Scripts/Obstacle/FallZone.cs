using Fusion;
using UnityEngine;

// This script does not need to be a NetworkBehaviour itself,
// as its primary role is just to detect collisions and trigger a method on the Player.
public class FallZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Try to get the NetworkObject component from the root GameObject
        // that the collider belongs to (which should be the Player prefab).
        NetworkObject playerNetworkObject = other.GetComponent<NetworkObject>();

        // Check if it's a networked object and if this instance has State Authority.
        // We only want the server/host to initiate the teleport, not clients.
        if (playerNetworkObject != null && playerNetworkObject.HasStateAuthority)
        {
            // Get the Player component from the detected NetworkObject.
            Player player = playerNetworkObject.GetComponent<Player>();
            if (player != null)
            {
                Debug.Log($"Player {player.Object.InputAuthority.PlayerId} entered FallZone trigger.");
                // Call the teleport method on the Player instance.
                player.TeleportToActiveCheckpoint();
            }
        }
    }

    // Editor-only visualization to easily see the Fall Zone in the Scene view.
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f); // Semi-transparent red
        // Draw a solid cube representing the trigger volume.
        // Use transform.lossyScale to correctly show the scaled size.
        Gizmos.DrawCube(transform.position, transform.lossyScale);

        Gizmos.color = new Color(1f, 0f, 0f, 0.7f); // More opaque red
        // Draw a wire cube for the outline.
        Gizmos.DrawWireCube(transform.position, transform.lossyScale);
    }
}