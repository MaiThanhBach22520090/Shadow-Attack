using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Highlightable : MonoBehaviour
{
    [SerializeField] private Material highlight;
    [SerializeField] private MeshRenderer meshRenderer;

    private Material regular;

    private void OnValidate()
    {
        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }
    }

    private void Start()
    {
        regular = meshRenderer.material;
    }

    public void Highlight(bool doHighlight)
    {
        meshRenderer.material = doHighlight ? highlight : regular;
    }
}
