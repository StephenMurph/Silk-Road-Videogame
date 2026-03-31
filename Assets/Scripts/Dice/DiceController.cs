using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

public class DiceController : MonoBehaviour
{
    [Header("Refs")]
    public Camera cam;
    public Rigidbody rb;

    [Header("Camera Hover")]
    public Vector3 cameraOffset = new Vector3(-0.4f, -0.25f, 10f);
    public float followSpeed = 12f;

    [Header("Spin")]
    public Vector3 spinAxis = new Vector3(0.35f, 1f, 0.2f);
    public float spinSpeed = 540f;

    [Header("Throw")]
    public float throwForward = 7f;
    public float throwUp = 3f;
    public float torque = 20f;

    [Header("Settle")]
    public float settleVelocity = 0.1f;
    public float settleAngular = 0.2f;
    public float settleTime = 0.6f;

    [Header("Result Reveal")]
    public float revealMoveDuration = 0.35f;
    public float revealHoldDuration = 0.45f;
    public float revealFollowSharpness = 14f;
    
    [Header("Impact Particles")]
    [SerializeField] private ParticleSystem sandImpactPrefab;
    [SerializeField] private float minSandImpactSpeed = 2.5f;
    [SerializeField] private float sandSpawnYOffset = 0.05f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource throwSource;
    [SerializeField] private AudioSource rollingSource;

    [SerializeField] private AudioClip throwClip;
    [SerializeField] private AudioClip rollingLoopClip;

    [SerializeField] private float throwVolume = 1.5f;
    [SerializeField] private float rollingVolume = 1.5f;

    [SerializeField] private float rollingStartSpeed = 0.8f;
    
    [SerializeField] private AudioSource impactSource;

    [SerializeField] private AudioClip grasslandGroundHitClip;
    [SerializeField] private AudioClip playerHitClip;
    [SerializeField] private AudioClip treeHitClip;

    [SerializeField] private float grasslandGroundHitVolume = 1.2f;
    [SerializeField] private float playerHitVolume = 1.2f;
    [SerializeField] private float treeHitVolume = 1.2f;

    [SerializeField] private float minImpactSoundSpeed = 1.5f;
    [SerializeField] private float impactSoundCooldown = 0.06f;
    
    [SerializeField] private AudioClip sandGroundHitClip;
    [SerializeField] private float sandGroundHitVolume = 1.2f;
    
    [SerializeField] private AudioClip cactusHitClip;
    [SerializeField] private float cactusHitVolume = 1.2f;
    
    public bool HasBeenThrown => thrown;
    
    private float lastImpactSoundTime = -999f;

    private Terrain terrain;
    private TerrainManager terrainManager;

    public Action<int> OnRolled;

    private readonly List<(int value, Transform t)> faces = new();

    public BoxCollider diceColider;
    private bool thrown;
    private bool settled;
    private bool revealing;
    private bool showingResult;
    private int shownResult = -1;
    private float stillTimer;

    void Awake()
    {
        terrain = FindFirstObjectByType<Terrain>();
        terrainManager = FindFirstObjectByType<TerrainManager>();
        
        if (!cam)
        {
            cam = Camera.main;
            if (!cam)
            {
                var all = FindObjectsByType<Camera>(FindObjectsSortMode.None);
                if (all.Length > 0) cam = all[0];
            }
        }

        if (!rb) rb = GetComponent<Rigidbody>();
        if (!diceColider) diceColider = GetComponent<BoxCollider>();
        
        if (!throwSource)
            throwSource = gameObject.AddComponent<AudioSource>();

        if (!rollingSource)
            rollingSource = gameObject.AddComponent<AudioSource>();

        throwSource.playOnAwake = false;
        throwSource.loop = false;
        throwSource.spatialBlend = 0f;
        throwSource.volume = 1f;

        rollingSource.playOnAwake = false;
        rollingSource.loop = false;
        rollingSource.spatialBlend = 0f;
        rollingSource.volume = 1f;
        
        if (!impactSource)
            impactSource = gameObject.AddComponent<AudioSource>();

        impactSource.playOnAwake = false;
        impactSource.loop = false;
        impactSource.spatialBlend = 0f;
        impactSource.volume = 1f;

        CacheFaces();
    }

    void Start()
    {
        PrepareForHover();
    }

    void Update()
    {
        if (showingResult)
        {
            StopRollingLoop();
            FollowCameraWhileShowingResult();
            return;
        }

        if (revealing || settled)
        {
            StopRollingLoop();
            return;
        }

        UpdateRollingAudio();

        if (!thrown)
        {
            HoverInFrontOfCamera();
            SpinDice();
            CheckClick();
        }
        else
        {
            CheckSettled();
        }
    }

    void PrepareForHover()
    {
        thrown = false;
        settled = false;
        revealing = false;
        showingResult = false;
        shownResult = -1;
        stillTimer = 0f;
        StopRollingLoop();

        if (rb)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    
        if (diceColider) diceColider.isTrigger = true;

        if (diceColider) diceColider.enabled = true;

        if (cam)
            transform.position = GetHoverTarget();
    }

    Vector3 GetHoverTarget()
    {
        if (!cam) return transform.position;

        return cam.transform.position +
               cam.transform.right * cameraOffset.x +
               cam.transform.up * cameraOffset.y +
               cam.transform.forward * cameraOffset.z;
    }

    void HoverInFrontOfCamera()
    {
        if (!cam) return;

        Vector3 target = GetHoverTarget();
        float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, target, t);
    }

    void SpinDice()
    {
        transform.Rotate(spinAxis.normalized, spinSpeed * Time.deltaTime, Space.Self);
    }

    void CheckClick()
    {
        if (Mouse.current == null || cam == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, ~0, QueryTriggerInteraction.Collide))
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                    ThrowDice();
            }
        }
    }

    void ThrowDice()
    {
        if (thrown || settled || revealing || showingResult) return;

        thrown = true;
        stillTimer = 0f;
        
        StopRollingLoop();

        if (diceColider)
        {
            diceColider.enabled = true;
            diceColider.isTrigger = false;
        }

        rb.isKinematic = false;

        Vector3 vel = cam.transform.forward * throwForward + Vector3.up * throwUp;
        rb.linearVelocity = vel;
        rb.angularVelocity = Random.onUnitSphere * torque;

        PlayThrowSound();
    }

    void CheckSettled()
    {
        if (rb.linearVelocity.magnitude < settleVelocity &&
            rb.angularVelocity.magnitude < settleAngular)
        {
            stillTimer += Time.deltaTime;

            if (stillTimer > settleTime)
            {
                int result = GetTopFace();
                StartCoroutine(RevealResultRoutine(result));
            }
        }
        else
        {
            stillTimer = 0f;
        }
    }

    private IEnumerator RevealResultRoutine(int result)
    {
        if (revealing) yield break;

        revealing = true;
        thrown = false;
        settled = true;
        stillTimer = 0f;
        
        StopRollingLoop();

        if (rb)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        if (diceColider) diceColider.enabled = false;

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        Vector3 targetPos = GetHoverTarget();
        Quaternion targetRot = GetRevealRotationForResult(result);

        float t = 0f;
        float dur = Mathf.Max(0.01f, revealMoveDuration);

        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            float s = u * u * (3f - 2f * u);

            transform.position = Vector3.Lerp(startPos, targetPos, s);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, s);

            yield return null;
        }

        transform.position = targetPos;
        transform.rotation = targetRot;

        shownResult = result;
        revealing = false;
        showingResult = true;

        yield return new WaitForSeconds(revealHoldDuration);

        Debug.Log("Dice Result: " + result);
        OnRolled?.Invoke(result);
    }

    private void FollowCameraWhileShowingResult()
    {
        if (!cam || shownResult < 0) return;

        Vector3 targetPos = GetHoverTarget();
        Quaternion targetRot = GetRevealRotationForResult(shownResult);

        float t = 1f - Mathf.Exp(-revealFollowSharpness * Time.deltaTime);

        transform.position = Vector3.Lerp(transform.position, targetPos, t);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
    }

    private Quaternion GetRevealRotationForResult(int result)
    {
        if (!cam) return transform.rotation;

        Transform face = GetFaceTransform(result);
        if (!face) return transform.rotation;

        Vector3 desiredFaceNormal = -cam.transform.forward;

        Quaternion alignFaceToCamera =
            Quaternion.FromToRotation(face.up, desiredFaceNormal) * transform.rotation;

        Quaternion saved = transform.rotation;
        transform.rotation = alignFaceToCamera;

        Vector3 faceRight = face.right;
        Vector3 desiredRight = cam.transform.right;

        Vector3 projectedFaceRight = Vector3.ProjectOnPlane(faceRight, desiredFaceNormal).normalized;
        Vector3 projectedDesiredRight = Vector3.ProjectOnPlane(desiredRight, desiredFaceNormal).normalized;

        Quaternion twistFix = Quaternion.identity;
        if (projectedFaceRight.sqrMagnitude > 0.0001f && projectedDesiredRight.sqrMagnitude > 0.0001f)
        {
            float signed = Vector3.SignedAngle(projectedFaceRight, projectedDesiredRight, desiredFaceNormal);
            twistFix = Quaternion.AngleAxis(signed, desiredFaceNormal);
        }

        Quaternion finalRot = twistFix * transform.rotation;
        transform.rotation = saved;

        return finalRot;
    }

    private Transform GetFaceTransform(int value)
    {
        foreach (var f in faces)
        {
            if (f.value == value)
                return f.t;
        }
        return null;
    }

    void CacheFaces()
    {
        faces.Clear();

        Transform root = transform.Find("Faces");
        if (!root) return;

        for (int i = 1; i <= 6; i++)
        {
            Transform t = root.Find("Face" + i);
            if (t) faces.Add((i, t));
        }
    }

    int GetTopFace()
    {
        float best = -999f;
        int result = -1;

        foreach (var f in faces)
        {
            float dot = Vector3.Dot(f.t.up, Vector3.up);
            if (dot > best)
            {
                best = dot;
                result = f.value;
            }
        }

        return result;
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        TrySpawnSandImpact(collision);
        TryPlayImpactSound(collision);
    }

    private void TrySpawnSandImpact(Collision collision)
    {
        if (!sandImpactPrefab || !terrain || !terrainManager || collision.contactCount == 0)
            return;

        float impactSpeed = collision.relativeVelocity.magnitude;
        if (impactSpeed < minSandImpactSpeed)
            return;

        ContactPoint contact = collision.GetContact(0);
        Vector3 p = contact.point;

        float nx = (p.x - terrain.transform.position.x) / terrain.terrainData.size.x;
        float nz = (p.z - terrain.transform.position.z) / terrain.terrainData.size.z;

        if (nx < 0f || nx > 1f || nz < 0f || nz > 1f)
            return;

        float desert = terrainManager.SendMessageDesertMask(nx, nz);
        if (desert < 0.35f)
            return;

        Vector3 spawnPos = p + Vector3.up * sandSpawnYOffset;
        ParticleSystem ps = Instantiate(sandImpactPrefab, spawnPos, Quaternion.identity);
        ps.Play();
        Destroy(ps.gameObject, 3f);
    }
    
    private void PlayThrowSound()
    {
        if (!throwSource || !throwClip)
            return;

        throwSource.PlayOneShot(throwClip, throwVolume);
    }

    private void StartRollingLoop()
    {
        if (!rollingSource || !rollingLoopClip)
            return;

        if (rollingSource.isPlaying && rollingSource.clip == rollingLoopClip)
            return;

        rollingSource.clip = rollingLoopClip;
        rollingSource.volume = rollingVolume;
        rollingSource.loop = true;
        rollingSource.Play();
    }
    
    private void UpdateRollingAudio()
    {
        if (!rollingSource || !rollingLoopClip)
            return;
        
        if (!thrown && !revealing && !settled && !showingResult)
        {
            StartRollingLoop();
            return;
        }
        
        StopRollingLoop();
    }
    
    private void StopRollingLoop()
    {
        if (!rollingSource)
            return;

        rollingSource.Stop();
        rollingSource.clip = null;
        rollingSource.loop = false;
        rollingSource.volume = 0f;
    }

    private void TryPlayImpactSound(Collision collision)
    {
        if (!impactSource)
            return;

        if (Time.time - lastImpactSoundTime < impactSoundCooldown)
            return;

        float impactSpeed = collision.relativeVelocity.magnitude;
        if (impactSpeed < minImpactSoundSpeed)
            return;

        GameObject other = collision.gameObject;
        if (!other)
            return;
    
        if (other.CompareTag("Player"))
        {
            PlayImpactClip(playerHitClip, playerHitVolume);
            return;
        }
    
        if (other.CompareTag("Tree"))
        {
            PlayImpactClip(treeHitClip, treeHitVolume);
            return;
        }

        if (other.CompareTag("Cactus"))
        {
            PlayImpactClip(cactusHitClip, cactusHitVolume);
            return;
        }
    
        if (terrain != null && terrainManager != null && collision.contactCount > 0)
        {
            ContactPoint contact = collision.GetContact(0);
            Vector3 p = contact.point;

            float nx = (p.x - terrain.transform.position.x) / terrain.terrainData.size.x;
            float nz = (p.z - terrain.transform.position.z) / terrain.terrainData.size.z;

            if (nx >= 0f && nx <= 1f && nz >= 0f && nz <= 1f)
            {
                float desert = terrainManager.SendMessageDesertMask(nx, nz);

                if (desert >= 0.35f)
                {
                    PlayImpactClip(sandGroundHitClip, sandGroundHitVolume);
                    return;
                }

                PlayImpactClip(grasslandGroundHitClip, grasslandGroundHitVolume);
                return;
            }
        }
    }
    
    private void PlayImpactClip(AudioClip clip, float volume)
    {
        if (!impactSource || !clip)
            return;

        lastImpactSoundTime = Time.time;
        impactSource.PlayOneShot(clip, volume);
    }
    
    public void ForceResult(int value)
    {
        if (revealing || showingResult)
            return;

        int clampedValue = Mathf.Clamp(value, 1, 6);
        StartCoroutine(RevealResultRoutine(clampedValue));
    }
}