using System.Collections;
using UnityEngine;

/// <summary>
/// Attached to a gate that was just dropped/spawned at the player's feet.
/// Disables all colliders for a short window so the CharacterController
/// does not get violently ejected by the new static collider overlapping it.
/// After the delay the colliders are restored and this component removes itself.
/// </summary>
public class GateSpawnDelay : MonoBehaviour
{
    [Tooltip("Seconds before the gate's colliders are re-enabled.")]
    public float delay = 0.8f;

    private Collider[] _colliders;

    void Awake()
    {
        _colliders = GetComponentsInChildren<Collider>(true);
        foreach (var col in _colliders)
            col.enabled = false;

        StartCoroutine(ReEnableAfterDelay());
    }

    private IEnumerator ReEnableAfterDelay()
    {
        yield return new WaitForSeconds(delay);

        if (this == null) yield break; // gate was destroyed before timer finished

        foreach (var col in _colliders)
        {
            if (col != null)
                col.enabled = true;
        }

        Destroy(this);
    }
}
