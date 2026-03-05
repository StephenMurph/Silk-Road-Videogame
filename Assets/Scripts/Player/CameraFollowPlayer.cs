using UnityEngine;
using UnityEngine.InputSystem;

public class CameraFollowPlayer : MonoBehaviour
{
    public Transform target;

    [Header("Distance")]
    public float distance = 16f;
    public float height = 12f;

    [Header("Rotation")]
    public float orbitSpeed = 120f;
    public float currentYaw = 0f;

    [Header("Follow")]
    public float followSmooth = 6f;

    private Vector3 currentVelocity;

    public void SetTarget(Transform newTarget, bool snap = false)
    {
        target = newTarget;
        if (snap && target)
        {
            currentVelocity = Vector3.zero;
            SnapNow();
        }
    }

    public void SnapNow()
    {
        if (!target) return;

        Quaternion rot = Quaternion.Euler(0f, currentYaw, 0f);
        Vector3 offset = rot * new Vector3(0, 0, -distance);
        offset.y = height;

        transform.position = target.position + offset;
        transform.LookAt(target.position + Vector3.up * 1.5f);
    }

    void LateUpdate()
    {
        if (!target) return;

        HandleOrbitInput();

        Quaternion rot = Quaternion.Euler(0f, currentYaw, 0f);
        Vector3 offset = rot * new Vector3(0, 0, -distance);
        offset.y = height;

        Vector3 desiredPosition = target.position + offset;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref currentVelocity,
            1f / Mathf.Max(0.0001f, followSmooth)
        );

        transform.LookAt(target.position + Vector3.up * 1.5f);
    }

    void HandleOrbitInput()
    {
        if (Keyboard.current == null) return;

        float input = 0f;

        if (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed)
            input -= 1f;

        if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed)
            input += 1f;

        currentYaw += input * orbitSpeed * Time.deltaTime;
    }
    
    public void SnapToTarget()
    {
        if (!target) return;

        Quaternion rot = Quaternion.Euler(0f, currentYaw, 0f);
        Vector3 offset = rot * new Vector3(0, 0, -distance);
        offset.y = height;

        transform.position = target.position + offset;
        transform.LookAt(target.position + Vector3.up * 1.5f);
    }
}