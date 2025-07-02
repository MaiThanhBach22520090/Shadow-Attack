using Fusion.Addons.KCC;
using Fusion;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
[RequireComponent(typeof(Rigidbody))]
public sealed class PlatformMovement : NetworkTRSPProcessor, IPlatform, IBeforeAllTicks
{
    public Transform Transform;
    public Rigidbody Rigidbody;

    public override void Spawned()
    {
        // Enable simulation also for proxy object.
        Runner.SetIsSimulated(Object, true);
    }

    public override void FixedUpdateNetwork()
    {
        CalculateNextState(out Vector3 nextPosition, out Quaternion nextRotation);

        // Set network state of NetworkTRSP.
        State.Position = nextPosition;
        State.Rotation = nextRotation;

        // Update engine components.
        Transform.position = nextPosition;
        Transform.rotation = nextRotation;
        Rigidbody.position = nextPosition;
    }

    public override void Render()
    {
        CalculateNextState(out Vector3 nextPosition, out Quaternion nextRotation);

        // Update only engine components, do not store in network state.
        Transform.position = nextPosition;
        Transform.rotation = nextRotation;
        Rigidbody.position = nextPosition;
    }

    void IBeforeAllTicks.BeforeAllTicks(bool resimulation, int tickCount)
    {
        // Restore state of the object before simulation.
        Transform.SetPositionAndRotation(State.Position, State.Rotation);
        Rigidbody.position = State.Position;
    }

    private void CalculateNextState(out Vector3 nextPosition, out Quaternion nextRotation)
    {
        nextPosition = State.Position;
        nextRotation = State.Rotation;

        // Movement based on waypoints, AnimationClip, ...
    }
}