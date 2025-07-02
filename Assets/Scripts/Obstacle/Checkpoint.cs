using Fusion;
using UnityEngine;

public class Checkpoint : NetworkBehaviour
{
    [SerializeField]
    private Transform _teleportTarget;
    [field: SerializeField]
    public int CheckpointID { get; private set; }

    public Vector3 GetTeleportPosition()
    {
        return _teleportTarget != null ? _teleportTarget.position : transform.position;
    }

    public Quaternion GetTeleportRotation()
    {
        return _teleportTarget != null ? _teleportTarget.rotation : transform.rotation;
    }

    public void Activate()
    {
        Debug.Log($"Checkpoint {CheckpointID} Activated!");
    }

    private void OnDrawGizmos()
    {
        Vector3 gizmoPosition = _teleportTarget != null ? _teleportTarget.position : transform.position;
        Quaternion gizmoRotation = _teleportTarget != null ? _teleportTarget.rotation : transform.rotation;
        Gizmos.color = _teleportTarget != null ? Color.cyan : Color.yellow;
        Gizmos.DrawSphere(gizmoPosition, 0.5f);
        Gizmos.DrawLine(gizmoPosition, gizmoPosition + gizmoRotation * Vector3.forward * 1.5f);
        Gizmos.DrawLine(gizmoPosition, gizmoPosition + gizmoRotation * Vector3.forward * 1.5f + gizmoRotation * Vector3.left * 0.2f);
        Gizmos.DrawLine(gizmoPosition, gizmoPosition + gizmoRotation * Vector3.forward * 1.5f + gizmoRotation * Vector3.right * 0.2f);
        Collider checkpointCollider = GetComponent<Collider>();
        if (checkpointCollider != null && checkpointCollider is BoxCollider boxCollider)
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
            Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
            Gizmos.DrawCube(boxCollider.center, boxCollider.size);
            Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
            Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
        }
        else if (checkpointCollider != null && checkpointCollider is SphereCollider sphereCollider)
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
            Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
            Gizmos.DrawSphere(sphereCollider.center, sphereCollider.radius);
            Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
            Gizmos.DrawWireSphere(sphereCollider.center, sphereCollider.radius);
        }
        Gizmos.matrix = Matrix4x4.identity;
    }
}