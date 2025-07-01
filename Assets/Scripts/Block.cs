using UnityEngine;
using Fusion;

public class Block : NetworkBehaviour
{
    [SerializeField] private BoxCollider boxCollider;
    [SerializeField] private MeshRenderer visual;
    [SerializeField] private AudioSource source;
    [SerializeField] private AudioClip reappearSound;

    public int ReappearTick;
    private BlockManager blockManager;

    private void Start()
    {
        blockManager = GetComponentInParent<BlockManager>();
    }

    public void Disable()
    {
        blockManager.AddDisable(this);
    }

    public void Hide(bool value)
    {
        boxCollider.enabled = !value;
        visual.enabled = !value;
        if (value)
        {
            source.Play();
        }
        else
        {
            source.PlayOneShot(reappearSound);
        }
    }
}
