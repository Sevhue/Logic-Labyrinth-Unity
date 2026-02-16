using UnityEngine;
using System.Collections;
using StarterAssets;

/// <summary>
/// Procedural first-person arm animation with individual bone control.
/// Auto-detects correct rotation axis at startup by testing all 6 directions.
/// </summary>
public class FirstPersonArmAnimator : MonoBehaviour
{
    [Header("References (auto-found if empty)")]
    public CharacterController characterController;
    public StarterAssetsInputs inputSource;

    [Header("Walk Settings")]
    public float walkSwingAngle = 2.5f;
    public float walkElbowBend = 1.2f;
    public float walkBobFrequency = 1.4f;
    public float walkBobAmplitudeY = 0.002f;

    [Header("Run Settings (Shift) — uses old walk values")]
    public float runSwingAngle = 4f;
    public float runElbowBend = 2f;
    public float runBobFrequency = 2.2f;
    public float runBobAmplitudeY = 0.003f;

    [Header("Idle Settings")]
    public float idleBreathSpeed = 0.8f;
    public float idleBreathAmplitude = 0.001f;
    public float idleFingerSpeed = 0.5f;
    public float idleFingerAmplitude = 2f;

    [Header("Collection Settings")]
    public float collectReachDuration = 0.35f;
    public float collectGrabDuration = 0.2f;
    public float collectReturnDuration = 0.5f;
    public float collectFingerCurlAngle = 60f;
    public float collectShoulderAngle = 12f;
    public float collectUpperArmAngle = 35f;
    public float collectForeArmAngle = 20f;

    [Header("Smoothing")]
    public float smoothSpeed = 6f;

    // Bones
    private Transform leftShoulder, leftArm, leftForeArm, leftHand;
    private Transform rightShoulder, rightArm, rightForeArm, rightHand;
    private Transform[] leftIndexBones = new Transform[3];
    private Transform[] leftMiddleBones = new Transform[3];
    private Transform[] leftRingBones = new Transform[3];
    private Transform[] leftPinkyBones = new Transform[3];
    private Transform[] leftThumbBones = new Transform[3];
    private Transform[] rightIndexBones = new Transform[3];
    private Transform[] rightMiddleBones = new Transform[3];
    private Transform[] rightRingBones = new Transform[3];
    private Transform[] rightPinkyBones = new Transform[3];
    private Transform[] rightThumbBones = new Transform[3];

    // Rest rotations
    private Quaternion leftShoulderRest, leftArmRest, leftForeArmRest, leftHandRest;
    private Quaternion rightShoulderRest, rightArmRest, rightForeArmRest, rightHandRest;
    private Quaternion[] leftFingerRest = new Quaternion[15];
    private Quaternion[] rightFingerRest = new Quaternion[15];

    private Vector3 baseLocalPosition;

    // State
    private float bobTimer;
    private float currentBobY;
    private bool isPlayingCollectAnim;
    private Coroutine collectCoroutine;

    // Detected swing axes per bone (parent-local space)
    private Vector3 lShoulderSwingLocal, lArmSwingLocal, lForeArmSwingLocal;
    private Vector3 rShoulderSwingLocal, rArmSwingLocal, rForeArmSwingLocal;

    public static FirstPersonArmAnimator Instance { get; private set; }

    void Awake() { Instance = this; }

    void Start()
    {
        baseLocalPosition = transform.localPosition;

        if (characterController == null)
            characterController = GetComponentInParent<CharacterController>();
        if (inputSource == null)
            inputSource = GetComponentInParent<StarterAssetsInputs>();

        FindBones();
        StoreRestPoses();
        AutoDetectSwingAxis();

        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>())
            smr.updateWhenOffscreen = true;
    }

    // ===================== SETUP =====================

    void FindBones()
    {
        leftShoulder  = FindBone("mixamorig:LeftShoulder");
        leftArm       = FindBone("mixamorig:LeftArm");
        leftForeArm   = FindBone("mixamorig:LeftForeArm");
        leftHand      = FindBone("mixamorig:LeftHand");
        rightShoulder = FindBone("mixamorig:RightShoulder");
        rightArm      = FindBone("mixamorig:RightArm");
        rightForeArm  = FindBone("mixamorig:RightForeArm");
        rightHand     = FindBone("mixamorig:RightHand");

        FindFingerBones("Left",  "Index",  leftIndexBones);
        FindFingerBones("Left",  "Middle", leftMiddleBones);
        FindFingerBones("Left",  "Ring",   leftRingBones);
        FindFingerBones("Left",  "Pinky",  leftPinkyBones);
        FindFingerBones("Left",  "Thumb",  leftThumbBones);
        FindFingerBones("Right", "Index",  rightIndexBones);
        FindFingerBones("Right", "Middle", rightMiddleBones);
        FindFingerBones("Right", "Ring",   rightRingBones);
        FindFingerBones("Right", "Pinky",  rightPinkyBones);
        FindFingerBones("Right", "Thumb",  rightThumbBones);

        if (leftArm == null || rightArm == null)
            Debug.LogError("FirstPersonArmAnimator: Could not find arm bones!");
        else
            Debug.Log("FirstPersonArmAnimator: All bones found.");
    }

    void FindFingerBones(string side, string finger, Transform[] bones)
    {
        for (int i = 0; i < 3; i++)
            bones[i] = FindBone($"mixamorig:{side}Hand{finger}{i + 1}");
    }

    Transform FindBone(string n) => FindDeepChild(transform, n);
    Transform FindDeepChild(Transform parent, string name)
    {
        var r = parent.Find(name);
        if (r != null) return r;
        foreach (Transform c in parent) { r = FindDeepChild(c, name); if (r != null) return r; }
        return null;
    }

    void StoreRestPoses()
    {
        if (leftShoulder)  leftShoulderRest  = leftShoulder.localRotation;
        if (leftArm)       leftArmRest       = leftArm.localRotation;
        if (leftForeArm)   leftForeArmRest   = leftForeArm.localRotation;
        if (leftHand)      leftHandRest      = leftHand.localRotation;
        if (rightShoulder) rightShoulderRest  = rightShoulder.localRotation;
        if (rightArm)      rightArmRest       = rightArm.localRotation;
        if (rightForeArm)  rightForeArmRest   = rightForeArm.localRotation;
        if (rightHand)     rightHandRest      = rightHand.localRotation;

        StoreFingerRest(leftIndexBones,  0,  false); StoreFingerRest(leftMiddleBones, 3,  false);
        StoreFingerRest(leftRingBones,   6,  false); StoreFingerRest(leftPinkyBones,  9,  false);
        StoreFingerRest(leftThumbBones,  12, false);
        StoreFingerRest(rightIndexBones,  0,  true);  StoreFingerRest(rightMiddleBones, 3,  true);
        StoreFingerRest(rightRingBones,   6,  true);  StoreFingerRest(rightPinkyBones,  9,  true);
        StoreFingerRest(rightThumbBones,  12, true);
    }

    void StoreFingerRest(Transform[] bones, int offset, bool isRight)
    {
        var arr = isRight ? rightFingerRest : leftFingerRest;
        for (int i = 0; i < 3; i++)
            if (bones[i] != null) arr[offset + i] = bones[i].localRotation;
    }

    void AutoDetectSwingAxis()
    {
        if (rightArm == null || rightHand == null || rightArm.parent == null)
        {
            Debug.LogWarning("FirstPersonArmAnimator: Cannot detect swing axis — using fallback.");
            rArmSwingLocal = Vector3.forward;
            rShoulderSwingLocal = lShoulderSwingLocal = lArmSwingLocal = lForeArmSwingLocal = rForeArmSwingLocal = Vector3.forward;
            return;
        }

        Vector3 charForward = characterController.transform.forward;
        Quaternion savedRot = rightArm.localRotation;
        Vector3 handBase = rightHand.position;

        Vector3[] candidates = {
            Vector3.right, -Vector3.right,
            Vector3.up,    -Vector3.up,
            Vector3.forward, -Vector3.forward
        };

        float bestDot = -999f;
        Vector3 bestParentLocalAxis = Vector3.forward;

        foreach (var ax in candidates)
        {
            rightArm.localRotation = Quaternion.AngleAxis(15f, ax) * rightArmRest;
            float dot = Vector3.Dot(rightHand.position - handBase, charForward);
            if (dot > bestDot) { bestDot = dot; bestParentLocalAxis = ax; }
        }

        rightArm.localRotation = savedRot;

        Vector3 worldSwingAxis = rightArm.parent.TransformDirection(bestParentLocalAxis);

        rArmSwingLocal = bestParentLocalAxis;
        if (rightShoulder && rightShoulder.parent) rShoulderSwingLocal = rightShoulder.parent.InverseTransformDirection(worldSwingAxis);
        if (rightForeArm  && rightForeArm.parent)  rForeArmSwingLocal  = rightForeArm.parent.InverseTransformDirection(worldSwingAxis);
        if (leftShoulder  && leftShoulder.parent)   lShoulderSwingLocal = leftShoulder.parent.InverseTransformDirection(worldSwingAxis);
        if (leftArm       && leftArm.parent)         lArmSwingLocal      = leftArm.parent.InverseTransformDirection(worldSwingAxis);
        if (leftForeArm   && leftForeArm.parent)     lForeArmSwingLocal  = leftForeArm.parent.InverseTransformDirection(worldSwingAxis);

        Debug.Log($"FirstPersonArmAnimator: Detected swing axis = {bestParentLocalAxis} (world: {worldSwingAxis}), dot={bestDot:F4}");
    }

    // ===================== BONE HELPERS =====================

    void SwingBone(Transform bone, Quaternion restRot, Vector3 parentLocalAxis, float angleDeg, float lerpSpd = -1f)
    {
        if (bone == null) return;
        float s = lerpSpd > 0 ? lerpSpd : smoothSpeed;
        Quaternion target = Quaternion.AngleAxis(angleDeg, parentLocalAxis) * restRot;
        bone.localRotation = Quaternion.Slerp(bone.localRotation, target, Time.deltaTime * s);
    }

    void SwingBoneImmediate(Transform bone, Quaternion restRot, Vector3 parentLocalAxis, float angleDeg)
    {
        if (bone == null) return;
        bone.localRotation = Quaternion.AngleAxis(angleDeg, parentLocalAxis) * restRot;
    }

    // ===================== LATE UPDATE =====================

    void LateUpdate()
    {
        if (characterController == null || inputSource == null) return;
        if (isPlayingCollectAnim) return;

        float hSpeed = new Vector3(characterController.velocity.x, 0f, characterController.velocity.z).magnitude;
        bool moving = hSpeed > 0.3f;
        bool sprinting = inputSource.sprint && moving;

        if (moving) AnimateMovement(sprinting);
        else AnimateIdle();
    }

    // ===================== MOVEMENT =====================

    void AnimateMovement(bool sprinting)
    {
        float swing  = sprinting ? runSwingAngle : walkSwingAngle;
        float elbow  = sprinting ? runElbowBend  : walkElbowBend;
        float freq   = sprinting ? runBobFrequency : walkBobFrequency;
        float bobAmp = sprinting ? runBobAmplitudeY : walkBobAmplitudeY;

        bobTimer += Time.deltaTime * freq;
        float phase = Mathf.Sin(bobTimer);

        float lSwing =  phase * swing;
        float rSwing = -phase * swing;

        float lElbow = Mathf.Max(0f,  phase) * elbow;
        float rElbow = Mathf.Max(0f, -phase) * elbow;

        SwingBone(leftShoulder,  leftShoulderRest,  lShoulderSwingLocal, lSwing * 0.2f);
        SwingBone(leftArm,       leftArmRest,       lArmSwingLocal,     lSwing);
        SwingBone(leftForeArm,   leftForeArmRest,   lForeArmSwingLocal, lElbow);

        SwingBone(rightShoulder, rightShoulderRest,  rShoulderSwingLocal, rSwing * 0.2f);
        SwingBone(rightArm,      rightArmRest,       rArmSwingLocal,     rSwing);
        SwingBone(rightForeArm,  rightForeArmRest,   rForeArmSwingLocal, rElbow);

        if (leftHand)  leftHand.localRotation  = Quaternion.Slerp(leftHand.localRotation,  leftHandRest,  Time.deltaTime * smoothSpeed);
        if (rightHand) rightHand.localRotation = Quaternion.Slerp(rightHand.localRotation, rightHandRest, Time.deltaTime * smoothSpeed);

        float bobY = Mathf.Sin(bobTimer * 2f) * bobAmp;
        currentBobY = Mathf.Lerp(currentBobY, bobY, Time.deltaTime * smoothSpeed);
        transform.localPosition = baseLocalPosition + new Vector3(0f, currentBobY, 0f);

        SetAllFingersCurl(8f, 5f);
    }

    // ===================== IDLE =====================

    void AnimateIdle()
    {
        bobTimer += Time.deltaTime * idleBreathSpeed;

        float breath = Mathf.Sin(bobTimer) * idleBreathAmplitude;
        currentBobY = Mathf.Lerp(currentBobY, breath, Time.deltaTime * smoothSpeed * 0.3f);
        transform.localPosition = baseLocalPosition + new Vector3(0f, currentBobY, 0f);

        float rs = smoothSpeed * 0.3f;
        SwingBone(leftShoulder,  leftShoulderRest,  lShoulderSwingLocal, 0f, rs);
        SwingBone(leftArm,       leftArmRest,       lArmSwingLocal,     0f, rs);
        SwingBone(leftForeArm,   leftForeArmRest,   lForeArmSwingLocal, 0f, rs);
        SwingBone(rightShoulder, rightShoulderRest,  rShoulderSwingLocal, 0f, rs);
        SwingBone(rightArm,      rightArmRest,       rArmSwingLocal,     0f, rs);
        SwingBone(rightForeArm,  rightForeArmRest,   rForeArmSwingLocal, 0f, rs);
        if (leftHand)  leftHand.localRotation  = Quaternion.Slerp(leftHand.localRotation,  leftHandRest,  Time.deltaTime * rs);
        if (rightHand) rightHand.localRotation = Quaternion.Slerp(rightHand.localRotation, rightHandRest, Time.deltaTime * rs);

        float fp = bobTimer * idleFingerSpeed;
        ApplyFingerCurl(leftIndexBones,  0,  false, 5f + Mathf.Sin(fp * 1.7f) * idleFingerAmplitude);
        ApplyFingerCurl(leftMiddleBones, 3,  false, 5f + Mathf.Sin(fp * 1.3f + 0.5f) * idleFingerAmplitude);
        ApplyFingerCurl(leftRingBones,   6,  false, 5f + Mathf.Sin(fp * 1.1f + 1.0f) * idleFingerAmplitude);
        ApplyFingerCurl(leftPinkyBones,  9,  false, 5f + Mathf.Sin(fp * 0.9f + 1.5f) * idleFingerAmplitude);
        ApplyFingerCurl(leftThumbBones,  12, false, 3f + Mathf.Sin(fp * 0.7f + 2.0f) * idleFingerAmplitude * 0.5f);
        ApplyFingerCurl(rightIndexBones,  0,  true, 5f + Mathf.Sin(fp * 1.7f) * idleFingerAmplitude);
        ApplyFingerCurl(rightMiddleBones, 3,  true, 5f + Mathf.Sin(fp * 1.3f + 0.5f) * idleFingerAmplitude);
        ApplyFingerCurl(rightRingBones,   6,  true, 5f + Mathf.Sin(fp * 1.1f + 1.0f) * idleFingerAmplitude);
        ApplyFingerCurl(rightPinkyBones,  9,  true, 5f + Mathf.Sin(fp * 0.9f + 1.5f) * idleFingerAmplitude);
        ApplyFingerCurl(rightThumbBones,  12, true, 3f + Mathf.Sin(fp * 0.7f + 2.0f) * idleFingerAmplitude * 0.5f);
    }

    // ===================== COLLECTION =====================

    public void PlayCollectAnimation()
    {
        if (collectCoroutine != null) StopCoroutine(collectCoroutine);
        collectCoroutine = StartCoroutine(CollectAnimationCoroutine());
    }

    private IEnumerator CollectAnimationCoroutine()
    {
        isPlayingCollectAnim = true;

        float sA = collectShoulderAngle;
        float aA = collectUpperArmAngle;
        float fA = collectForeArmAngle;

        // Phase 1: Reach forward
        float elapsed = 0f;
        while (elapsed < collectReachDuration)
        {
            float t = EaseOutQuad(Mathf.Clamp01(elapsed / collectReachDuration));

            SwingBoneImmediate(rightShoulder, rightShoulderRest, rShoulderSwingLocal, sA * t);
            SwingBoneImmediate(rightArm,      rightArmRest,      rArmSwingLocal,     aA * t);
            SwingBoneImmediate(rightForeArm,  rightForeArmRest,  rForeArmSwingLocal, fA * t);
            SetRightFingersCurlImmediate(-10f * t, -5f * t);

            SwingBoneImmediate(leftArm, leftArmRest, lArmSwingLocal, 2f * t);

            transform.localPosition = baseLocalPosition + new Vector3(0f, 0f, 0.015f * t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Phase 2: Grab
        elapsed = 0f;
        while (elapsed < collectGrabDuration)
        {
            float t = EaseOutQuad(Mathf.Clamp01(elapsed / collectGrabDuration));

            SetRightFingersCurlImmediate(
                Mathf.Lerp(-10f, collectFingerCurlAngle, t),
                Mathf.Lerp(-5f, collectFingerCurlAngle * 0.7f, t));

            SwingBoneImmediate(rightShoulder, rightShoulderRest, rShoulderSwingLocal, sA);
            SwingBoneImmediate(rightArm,      rightArmRest,      rArmSwingLocal,     aA);
            SwingBoneImmediate(rightForeArm,  rightForeArmRest,  rForeArmSwingLocal, fA);

            elapsed += Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(0.1f);

        // Phase 3: Return
        Vector3 retPos = transform.localPosition;
        elapsed = 0f;
        while (elapsed < collectReturnDuration)
        {
            float t = EaseInOutQuad(Mathf.Clamp01(elapsed / collectReturnDuration));

            SwingBoneImmediate(rightShoulder, rightShoulderRest, rShoulderSwingLocal, Mathf.Lerp(sA, 0f, t));
            SwingBoneImmediate(rightArm,      rightArmRest,      rArmSwingLocal,     Mathf.Lerp(aA, 0f, t));
            SwingBoneImmediate(rightForeArm,  rightForeArmRest,  rForeArmSwingLocal, Mathf.Lerp(fA, 0f, t));
            SwingBoneImmediate(leftArm,       leftArmRest,       lArmSwingLocal,     Mathf.Lerp(2f, 0f, t));

            SetRightFingersCurlImmediate(
                Mathf.Lerp(collectFingerCurlAngle, 5f, t),
                Mathf.Lerp(collectFingerCurlAngle * 0.7f, 3f, t));

            if (rightHand) rightHand.localRotation = Quaternion.Slerp(rightHand.localRotation, rightHandRest, t);
            transform.localPosition = Vector3.Lerp(retPos, baseLocalPosition, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = baseLocalPosition;
        if (rightHand) rightHand.localRotation = rightHandRest;
        isPlayingCollectAnim = false;
        collectCoroutine = null;
    }

    // ===================== FINGER HELPERS =====================

    void SetAllFingersCurl(float a, float t) { SetLeftFingersCurl(a, t); SetRightFingersCurl(a, t); }

    void SetLeftFingersCurl(float a, float t)
    {
        ApplyFingerCurl(leftIndexBones, 0, false, a); ApplyFingerCurl(leftMiddleBones, 3, false, a);
        ApplyFingerCurl(leftRingBones, 6, false, a);  ApplyFingerCurl(leftPinkyBones, 9, false, a);
        ApplyFingerCurl(leftThumbBones, 12, false, t);
    }

    void SetRightFingersCurl(float a, float t)
    {
        ApplyFingerCurl(rightIndexBones, 0, true, a); ApplyFingerCurl(rightMiddleBones, 3, true, a);
        ApplyFingerCurl(rightRingBones, 6, true, a);  ApplyFingerCurl(rightPinkyBones, 9, true, a);
        ApplyFingerCurl(rightThumbBones, 12, true, t);
    }

    void ApplyFingerCurl(Transform[] bones, int off, bool isR, float angle)
    {
        var rest = isR ? rightFingerRest : leftFingerRest;
        for (int i = 0; i < 3; i++)
        {
            if (bones[i] == null) continue;
            Quaternion tgt = rest[off + i] * Quaternion.Euler(angle * (1f + i * 0.15f), 0f, 0f);
            bones[i].localRotation = Quaternion.Slerp(bones[i].localRotation, tgt, Time.deltaTime * smoothSpeed);
        }
    }

    void SetRightFingersCurlImmediate(float fa, float ta)
    {
        CurlImmediate(rightIndexBones,  0,  true, fa); CurlImmediate(rightMiddleBones, 3,  true, fa);
        CurlImmediate(rightRingBones,   6,  true, fa); CurlImmediate(rightPinkyBones,  9,  true, fa);
        CurlImmediate(rightThumbBones,  12, true, ta);
    }

    void CurlImmediate(Transform[] bones, int off, bool isR, float angle)
    {
        var rest = isR ? rightFingerRest : leftFingerRest;
        for (int i = 0; i < 3; i++)
        {
            if (bones[i] == null) continue;
            bones[i].localRotation = rest[off + i] * Quaternion.Euler(angle * (1f + i * 0.15f), 0f, 0f);
        }
    }

    float EaseOutQuad(float t)   => 1f - (1f - t) * (1f - t);
    float EaseInOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

    void OnDestroy() { if (Instance == this) Instance = null; }
}
