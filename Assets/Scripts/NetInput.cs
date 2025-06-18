using Fusion;
using UnityEngine;

public enum InputButton
{
    Jump,
    Grapple,
    Glide,
}

public struct NetInput : INetworkInput
{
    public NetworkButtons Buttons;
    public Vector2 Direction;
    public Vector2 LookDelta;
}