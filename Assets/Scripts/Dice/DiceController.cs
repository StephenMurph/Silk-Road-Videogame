using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class DiceController : MonoBehaviour
{
    [Header("Refs")]
    public PhysicsResetManager resetManager;
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

    private bool thrown;
    private float stillTimer;
    
    public System.Action<int> OnRolled;

    private List<(int value, Transform t)> faces = new();

    void Awake()
    {
        // Always prefer the camera that renders the game view.
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

        Debug.Log($"[DiceController] Using camera: {(cam ? cam.name : "NULL")} tag={(cam ? cam.tag : "NULL")}");
        CacheFaces();
        
        if (!resetManager)
            resetManager = FindFirstObjectByType<PhysicsResetManager>();
    }
    
    void Start()
    {
        rb.isKinematic = true;

        // snap immediately so it's not flying in from world origin
        if (cam)
            transform.position = GetHoverTarget();
    }

    void Update()
    {
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

    Vector3 GetHoverTarget()
    {
        Vector3 target =
            cam.transform.position +
            cam.transform.right * cameraOffset.x +
            cam.transform.up * cameraOffset.y +
            cam.transform.forward * cameraOffset.z;

        return target;
    }

    void HoverInFrontOfCamera()
    {
        if (!cam) return;

        Vector3 target = GetHoverTarget();

        // Smooth follow (move a fraction toward target each frame)
        float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, target, t);
    }

    void SpinDice()
    {
        transform.Rotate(spinAxis.normalized, spinSpeed * Time.deltaTime, Space.Self);
    }

    void CheckClick()
    {
        if (Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.transform == transform)
                {
                    ThrowDice();
                }
            }
        }
    }

    void ThrowDice()
    {
        resetManager.Capture();
        thrown = true;
        rb.isKinematic = false;

        Vector3 vel = cam.transform.forward * throwForward + Vector3.up * throwUp;
        rb.linearVelocity = vel;
        rb.angularVelocity = Random.onUnitSphere * torque;
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
                
                resetManager?.RestoreAnimated();
                
                Debug.Log("Dice Result: " + result);
                
                OnRolled?.Invoke(result); 
                
                Destroy(gameObject);
            }
        }
        else
        {
            stillTimer = 0f;
        }
    }

    void CacheFaces()
    {
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
}