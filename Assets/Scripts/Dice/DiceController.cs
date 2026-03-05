using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System;
using Random = UnityEngine.Random;

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

    public Action<int> OnRolled;

    private readonly List<(int value, Transform t)> faces = new();

    void Awake()
    {
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

        CacheFaces();

        if (!resetManager)
            resetManager = FindFirstObjectByType<PhysicsResetManager>();
    }

    void Start()
    {
        PrepareForHover();
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

    void PrepareForHover()
    {
        thrown = false;
        stillTimer = 0f;

        if (rb)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

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

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                // IMPORTANT: works even if collider is on a child
                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                    ThrowDice();
            }
        }
    }

    void ThrowDice()
    {
        resetManager?.Capture();

        thrown = true;
        stillTimer = 0f;

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

                // ready for the next click/roll
                PrepareForHover();
            }
        }
        else
        {
            stillTimer = 0f;
        }
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
}