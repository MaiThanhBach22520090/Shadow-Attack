using Fusion.Addons.KCC;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FloatingTrampoline : KCCProcessor
{
    public float ImpulseStrength;

    public override void OnEnter(KCC kcc, KCCData data)
    {
        Vector3 impulseDirection = transform.up;

        // Clear dynamic velocity proportionally to impulse direction
        kcc.SetDynamicVelocity(data.DynamicVelocity - Vector3.Scale(data.DynamicVelocity, impulseDirection.normalized));

        // Add impulse
        kcc.AddExternalImpulse(impulseDirection * ImpulseStrength);
    }
}
