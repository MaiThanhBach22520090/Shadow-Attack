namespace Example.MovingPlatform
{
    using System;
    using UnityEngine;
    using Fusion;
    using Fusion.Addons.KCC;

    /// <summary>
    /// Basic platform which moves the object between waypoints. It must be executed first, before any player executes its movement.
    /// This script needs to be a KCC processor (deriving from NetworkTRSPProcessor) to be correctly tracked by PlatformProcessor.
    /// It also implements IMapStatusProvider - providing status text about waiting/travel time shown in UI.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [RequireComponent(typeof(Rigidbody))]
    public sealed unsafe class MovingPlatform : NetworkTRSPProcessor, IPlatform, IAfterClientPredictionReset, IBeforeAllTicks, IAfterTick
    {
        // PRIVATE MEMBERS

        [SerializeField] private EPlatformMode _mode;
        [SerializeField] private float _speed = 1.0f;
        [SerializeField] private float _rotationSpeed = 90.0f;
        [SerializeField] private RotationAxis _rotationAxis = RotationAxis.Y;
        [SerializeField] private PlatformWaypoint[] _waypoints;

        [Networked]
        private int _waypoint { get; set; }
        [Networked]
        private int _direction { get; set; }
        [Networked]
        private float _waitTime { get; set; }
        [Networked]
        private Vector3 _position { get; set; }

        private Transform _transform;
        private Rigidbody _rigidbody;
        private int _lastRenderFrame;

        // NetworkBehaviour INTERFACE

        public override void Spawned()
        {
            _lastRenderFrame = default;

            if (HasStateAuthority == true)
            {
                // Write initial values.
                _waypoint = default;
                _direction = default;

                StoreTransform();
            }
            else
            {
                // Read initial values.
                RestoreTransform();
            }

            // Enable prediction on this object.
            if (Runner.GameMode != GameMode.Shared)
            {
                Runner.SetIsSimulated(Object, true);
            }
        }

        public override void FixedUpdateNetwork()
        {

            if (_mode == EPlatformMode.Rotating)
            {
                // Rotate the platform around its up axis.
                //_transform.Rotate(Vector3.up, _rotationSpeed * Runner.DeltaTime, Space.World);

                Vector3 rotationAxis = _rotationAxis switch
                {
                    RotationAxis.X => Vector3.right,
                    RotationAxis.Y => Vector3.up,
                    RotationAxis.Z => Vector3.forward,
                    _ => throw new NotImplementedException(_rotationAxis.ToString())
                };

                _transform.Rotate(rotationAxis, _rotationSpeed * Runner.DeltaTime, Space.World);
            }
            else return;

            if (_waitTime > 0.0f)
            {
                _waitTime = Mathf.Max(_waitTime - Runner.DeltaTime, 0.0f);
            }
            else
            {
                // Calculate next position of the platform.
                CalculateNextPosition(_waypoint, _direction, _position, Runner.DeltaTime, out int nextWaypoint, out int nextDirection, out Vector3 nextPosition, out float nextWaitTime);

                _transform.position = nextPosition;
                _rigidbody.position = nextPosition;

                _waypoint = nextWaypoint;
                _direction = nextDirection;
                _waitTime = nextWaitTime;

                StoreTransform();
            }
            

        }

        public override void Render()
        {
            _lastRenderFrame = Time.frameCount;

            if (Runner.GameMode == GameMode.Shared && IsProxy == true)
            {
                InterpolateSharedPosition();
                return;
            }

            if (_waitTime > 0.0f)
                return;

            // Calculate next render position of the platform.
            CalculateNextPosition(_waypoint, _direction, _position, Runner.DeltaTime * Runner.LocalAlpha, out int nextWaypoint, out int nextDirection, out Vector3 nextPosition, out float nextWaitTime);

            _transform.position = nextPosition;
            _rigidbody.position = nextPosition;
        }

        // IAfterClientPredictionReset INTERFACE

        void IAfterClientPredictionReset.AfterClientPredictionReset()
        {
            RestoreTransform();
        }

        // IBeforeAllTicks INTERFACE

        void IBeforeAllTicks.BeforeAllTicks(bool resimulation, int tickCount)
        {
            // Skip resimulation, the state is already restored from AfterClientPredictionReset().
            if (resimulation == true)
                return;

            // Restore state only if a render update was executed previous frame.
            // Otherwise we continue with state from previous fixed tick or the state is already restored from AfterClientPredictionReset().
            int previousFrame = Time.frameCount - 1;
            if (previousFrame != _lastRenderFrame)
                return;

            RestoreTransform();
        }

        // IAfterTick INTERFACE

        void IAfterTick.AfterTick()
        {
            if (Runner.GameMode == GameMode.Shared && HasStateAuthority == false)
                return;

            StoreTransform();
        }

        // MonoBehaviour INTERFACE

        private void Awake()
        {
            _transform = transform;
            _rigidbody = GetComponent<Rigidbody>();

            if (_rigidbody == null)
                throw new NullReferenceException($"GameObject {name} has missing Rigidbody component!");

            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;
            _rigidbody.interpolation = RigidbodyInterpolation.None;
            _rigidbody.constraints = RigidbodyConstraints.FreezeAll;
        }

        // IMapStatusProvider INTERFACE


        // PRIVATE METHODS

        private void StoreTransform()
        {
            _transform.GetPositionAndRotation(out Vector3 position, out Quaternion rotation);

            // Set full-precision position.
            _position = position;

            // Set position of NetworkTRSP (compressed).
            State.Position = position;
            State.Rotation = rotation;
        }

        private void RestoreTransform()
        {
            _transform.SetPositionAndRotation(_position, State.Rotation);
            _rigidbody.position = _position;
        }

        private void InterpolateSharedPosition()
        {
            if (TryGetSnapshotsBuffers(out NetworkBehaviourBuffer from, out NetworkBehaviourBuffer to, out float alpha) == false)
                return;

            (Vector3, Vector3) positions = GetPropertyReader<Vector3>(nameof(_position)).Read(from, to);

            Vector3 position = Vector3.Lerp(positions.Item1, positions.Item2, alpha);

            _transform.position = position;
            _rigidbody.position = position;
        }

        private void CalculateNextPosition(int baseWaypoint, int baseDirection, Vector3 basePosition, float deltaTime, out int nextWaypoint, out int nextDirection, out Vector3 nextPosition, out float nextWaitTime)
        {
            nextWaypoint = baseWaypoint;
            nextDirection = baseDirection;
            nextPosition = basePosition;
            nextWaitTime = default;

            if (baseWaypoint >= _waypoints.Length)
                return;

            float remainingDistance = _speed * deltaTime;
            while (remainingDistance > 0.0f)
            {
                PlatformWaypoint targetWaypoint = _waypoints[nextWaypoint];
                Vector3 targetDelta = targetWaypoint.Transform.position - basePosition;

                if (targetDelta.sqrMagnitude >= (remainingDistance * remainingDistance))
                {
                    nextPosition += targetDelta.normalized * remainingDistance;
                    break;
                }
                else
                {
                    basePosition += targetDelta;
                    nextPosition += targetDelta;

                    remainingDistance -= targetDelta.magnitude;

                    nextWaitTime = targetWaypoint.WaitTime;

                    if (_mode == EPlatformMode.None)
                    {
                        ++nextWaypoint;
                        if (nextWaypoint >= _waypoints.Length)
                            break;
                    }
                    else if (_mode == EPlatformMode.Looping)
                    {
                        ++nextWaypoint;
                        nextWaypoint %= _waypoints.Length;
                    }
                    else if (_mode == EPlatformMode.PingPong)
                    {
                        if (nextDirection == 0)
                        {
                            ++nextWaypoint;
                            if (nextWaypoint >= _waypoints.Length)
                            {
                                nextWaypoint = _waypoints.Length - 2;
                                nextDirection = -1;
                            }
                        }
                        else
                        {
                            --nextWaypoint;
                            if (nextWaypoint < 0)
                            {
                                nextWaypoint = 1;
                                nextDirection = 0;
                            }
                        }
                    }

                    if (nextWaitTime != default)
                        break;
                }
            }
        }

        private float CalculateRelativeWaypointDistance(int nextWaypoint, int direction, Vector3 currentPosition)
        {
            if (_waypoints.Length <= 1)
                return 0.0f;

            int previousWaypoint = nextWaypoint;

            if (_mode == EPlatformMode.None)
            {
                --previousWaypoint;
                if (previousWaypoint < 0)
                    return 0.0f;
            }
            else if (_mode == EPlatformMode.Looping)
            {
                previousWaypoint = (previousWaypoint - 1 + _waypoints.Length) % _waypoints.Length;
            }
            else if (_mode == EPlatformMode.PingPong)
            {
                if (direction == 0)
                {
                    previousWaypoint = (previousWaypoint - 1 + _waypoints.Length) % _waypoints.Length;
                    if (previousWaypoint < 0)
                    {
                        previousWaypoint = 1;
                    }
                }
                else
                {
                    ++previousWaypoint;
                    if (previousWaypoint >= _waypoints.Length)
                    {
                        previousWaypoint = _waypoints.Length - 2;
                    }
                }
            }
            else
            {
                throw new NotImplementedException(_mode.ToString());
            }

            if (previousWaypoint == nextWaypoint)
                return 1.0f;
            if (nextWaypoint < 0 || nextWaypoint >= _waypoints.Length)
                return 1.0f;

            Vector3 previousPosition = _waypoints[previousWaypoint].Transform.position;
            Vector3 nextPosition = _waypoints[nextWaypoint].Transform.position;

            float length = Vector3.Distance(previousPosition, nextPosition);
            float distance = Vector3.Distance(previousPosition, currentPosition);

            return length > 0.001f ? Mathf.Clamp01(distance / length) : 1.0f;
        }

        // DATA STRUCTURES

        [Serializable]
        private sealed class PlatformWaypoint
        {
            public Transform Transform;
            public float WaitTime;
        }

        private enum EPlatformMode
        {
            None = 0,
            Looping = 1,
            PingPong = 2,
            Rotating = 3,
        }
    }
}
