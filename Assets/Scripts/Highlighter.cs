using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Highlighter : MonoBehaviour
{
    private Highlightable current;

    public void UpdateHightlightable(Vector3 origin, Vector3 direction, Player player)
    {
        Highlightable highlightable = null;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, player.AbilityRange))
        {
            if (player.GrappleCDFactor == 0f)
                highlightable = hit.collider.GetComponent<Highlightable>();

            switch (player.SelectedAbility)
            {
                case AbilityMode.BreakBlock:
                    if (player.BreakBlockCDFactor == 0f)
                    {
                        highlightable = hit.collider.GetComponent<Highlightable>();
                    }
                    break;
                case AbilityMode.Cage:
                    if (player.CageCDFactor == 0f)
                    {
                        if (hit.rigidbody != null)
                        {
                            highlightable = hit.rigidbody.GetComponent<Highlightable>();
                        }
                    }
                    break;
                case AbilityMode.Shove:
                    if (player.ShoveCDFactor == 0f)
                    {
                        if (hit.rigidbody != null)
                        {
                            highlightable = hit.rigidbody.GetComponent<Highlightable>();
                        }
                    }
                    break;
            }
        }

        if (highlightable != current)
        {
            if (current != null)
            {
                current.Highlight(false);
            }
            current = highlightable;
            if (current != null)
            {
                current.Highlight(true);
            }
        }
    }
}