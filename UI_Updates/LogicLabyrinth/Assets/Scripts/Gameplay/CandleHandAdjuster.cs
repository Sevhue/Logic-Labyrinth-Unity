using UnityEngine;

/// <summary>
/// LIVE ADJUSTER — Added to the EquippedCandle at runtime.
/// 
/// HOW TO USE:
/// 1. Play the game, collect the candle, equip it from hotbar
/// 2. In Hierarchy, find: R_hand > EquippedCandle
/// 3. You can FREELY move/rotate/scale EquippedCandle and its children
///    (CandleMesh, FlameLight, FlameGlow) using the normal Unity gizmos
/// 4. Press P to print all current values to the Console
/// 5. Send those values to hardcode them permanently
/// </summary>
public class CandleHandAdjuster : MonoBehaviour
{
    [Header("References (auto-assigned)")]
    public Transform meshChild;
    public Transform flameLightTransform;
    public Transform flameGlowTransform;

    void Update()
    {
        // Press P to print current transform values to Console
        if (Input.GetKeyDown(KeyCode.P))
        {
            string rootPos = $"({transform.localPosition.x:F3}, {transform.localPosition.y:F3}, {transform.localPosition.z:F3})";
            string rootRot = $"({transform.localEulerAngles.x:F1}, {transform.localEulerAngles.y:F1}, {transform.localEulerAngles.z:F1})";
            string rootScl = $"({transform.localScale.x:F2}, {transform.localScale.y:F2}, {transform.localScale.z:F2})";

            string meshPos = "N/A", meshRot = "N/A", meshScl = "N/A";
            if (meshChild != null)
            {
                meshPos = $"({meshChild.localPosition.x:F3}, {meshChild.localPosition.y:F3}, {meshChild.localPosition.z:F3})";
                meshRot = $"({meshChild.localEulerAngles.x:F1}, {meshChild.localEulerAngles.y:F1}, {meshChild.localEulerAngles.z:F1})";
                meshScl = $"({meshChild.localScale.x:F1}, {meshChild.localScale.y:F1}, {meshChild.localScale.z:F1})";
            }

            string flamePos = "N/A";
            if (flameLightTransform != null)
                flamePos = $"({flameLightTransform.localPosition.x:F3}, {flameLightTransform.localPosition.y:F3}, {flameLightTransform.localPosition.z:F3})";

            string glowPos = "N/A", glowScl = "N/A";
            if (flameGlowTransform != null)
            {
                glowPos = $"({flameGlowTransform.localPosition.x:F3}, {flameGlowTransform.localPosition.y:F3}, {flameGlowTransform.localPosition.z:F3})";
                glowScl = $"({flameGlowTransform.localScale.x:F3}, {flameGlowTransform.localScale.y:F3}, {flameGlowTransform.localScale.z:F3})";
            }

            Debug.Log(
                "╔══════════════════════════════════════════════════╗\n" +
                "║     CANDLE HAND VALUES — COPY THESE!             ║\n" +
                "╠══════════════════════════════════════════════════╣\n" +
               $"║  ROOT Position:    {rootPos}\n" +
               $"║  ROOT Rotation:    {rootRot}\n" +
               $"║  ROOT Scale:       {rootScl}\n" +
                "║  ─────────────────────────────────────────────── ║\n" +
               $"║  MESH Position:    {meshPos}\n" +
               $"║  MESH Rotation:    {meshRot}\n" +
               $"║  MESH Scale:       {meshScl}\n" +
                "║  ─────────────────────────────────────────────── ║\n" +
               $"║  FLAME Position:   {flamePos}\n" +
               $"║  GLOW Position:    {glowPos}\n" +
               $"║  GLOW Scale:       {glowScl}\n" +
                "╚══════════════════════════════════════════════════╝"
            );
        }
    }
}
