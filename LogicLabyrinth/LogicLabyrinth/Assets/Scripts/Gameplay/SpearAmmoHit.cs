using UnityEngine;

public class SpearAmmoHit : MonoBehaviour
{
    [SerializeField] private float damageAmount = 10f;
    [SerializeField] private float hitCooldown = 0.2f;
    private readonly System.Collections.Generic.Dictionary<int, float> nextDamageTimeByPlayer = new System.Collections.Generic.Dictionary<int, float>();

    private void Awake()
    {
        // Keep spear damage fixed to the requested value.
        damageAmount = 10f;

        Collider c = GetComponent<Collider>();
        if (c != null)
        {
            c.enabled = true;
            if (c is MeshCollider meshCollider)
                meshCollider.convex = true;
            c.isTrigger = true;
        }
    }

    private void TryDamage(Collider other)
    {
        if (other == null) return;
        if (!other.CompareTag("Player")) return;
        Debug.Log($"[SpearAmmoHit] Triggered by {other.name}");
        var fpc = other.GetComponentInParent<StarterAssets.FirstPersonController>();
        if (fpc == null)
        {
            Debug.LogWarning("[SpearAmmoHit] Player hit but no FirstPersonController found in parent!");
            return;
        }
        int id = fpc.GetInstanceID();
        float now = Time.time;
        if (nextDamageTimeByPlayer.TryGetValue(id, out float nextAllowed) && now < nextAllowed)
        {
            Debug.Log($"[SpearAmmoHit] Damage cooldown active for {other.name}");
            return;
        }
        nextDamageTimeByPlayer[id] = now + Mathf.Max(0.05f, hitCooldown);
        Debug.Log($"[SpearAmmoHit] Applying {damageAmount} damage to {other.name}");
        fpc.ApplyDamage(Mathf.Max(0f, damageAmount));
        gameObject.SetActive(false); // Disappear instantly
    }

    private void OnTriggerEnter(Collider other) => TryDamage(other);
    private void OnTriggerStay(Collider other) => TryDamage(other);
}
