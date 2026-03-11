using UnityEngine;
using UnityEngine.InputSystem;

public class CameraFollowPlayer : MonoBehaviour
{
    public Transform target;

    [Header("Orbit")]
    public float yaw = 0f;
    public float pitch = 58f;
    public float minPitch = 35f;
    public float maxPitch = 75f;

    [Header("Zoom")]
    public float distance = 18f;
    public float minDistance = 8f;
    public float maxDistance = 30f;
    public float zoomStepKeyboard = 18f;
    public float zoomStepScroll = 12f;
    public float zoomSmooth = 10f;

    [Header("Rotation Input")]
    public float keyboardYawSpeed = 120f;
    public float mouseYawSpeed = 0.22f;
    public float mousePitchSpeed = 0.16f;

    [Header("Follow")]
    public float normalFollowSmooth = 4.5f;
    public float looseFollowSmooth = 1.8f;
    public float currentFollowSmooth = 4.5f;
    public Vector3 targetLookOffset = new Vector3(0f, 1.5f, 0f);

    private Vector3 pivotPosition;
    private Vector3 pivotVelocity;

    private float targetDistance;
    private bool initialized;

    public void SetTarget(Transform newTarget, bool snap = false)
    {
        target = newTarget;

        if (!target) return;

        if (!initialized || snap)
        {
            pivotPosition = target.position;
            pivotVelocity = Vector3.zero;
            targetDistance = distance;
            initialized = true;
            SnapNow();
        }
    }

    public void SetLooseFollow(bool loose)
    {
        currentFollowSmooth = loose ? looseFollowSmooth : normalFollowSmooth;
    }

    public void SnapNow()
    {
        if (!target) return;

        Vector3 focus = target.position + targetLookOffset;
        Vector3 camPos = ComputeOrbitPosition(focus, distance);

        transform.position = camPos;
        transform.rotation = Quaternion.LookRotation((focus - camPos).normalized, Vector3.up);
    }

    void Awake()
    {
        targetDistance = distance;
        currentFollowSmooth = normalFollowSmooth;
    }

    void LateUpdate()
    {
        if (!target) return;

        HandleInput();

        float followSmoothTime = 1f / Mathf.Max(0.0001f, currentFollowSmooth);
        pivotPosition = Vector3.SmoothDamp(
            pivotPosition,
            target.position,
            ref pivotVelocity,
            followSmoothTime
        );

        distance = Mathf.Lerp(distance, targetDistance, 1f - Mathf.Exp(-zoomSmooth * Time.deltaTime));

        Vector3 focus = pivotPosition + targetLookOffset;
        Vector3 camPos = ComputeOrbitPosition(focus, distance);

        transform.position = camPos;
        transform.rotation = Quaternion.LookRotation((focus - camPos).normalized, Vector3.up);
    }

    void HandleInput()
    {
        if (Keyboard.current != null)
        {
            float yawInput = 0f;
            if (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed)
                yawInput -= 1f;
            if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed)
                yawInput += 1f;

            yaw += yawInput * keyboardYawSpeed * Time.deltaTime;

            float zoomInput = 0f;
            if (Keyboard.current.upArrowKey.isPressed || Keyboard.current.wKey.isPressed)
                zoomInput -= 1f;
            if (Keyboard.current.downArrowKey.isPressed || Keyboard.current.sKey.isPressed)
                zoomInput += 1f;

            targetDistance += zoomInput * zoomStepKeyboard * Time.deltaTime;
        }

        if (Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
                targetDistance -= scroll * zoomStepScroll * 0.01f;

            if (Mouse.current.rightButton.isPressed)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();
                yaw += delta.x * mouseYawSpeed;
                pitch -= delta.y * mousePitchSpeed;
            }
        }

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
    }

    Vector3 ComputeOrbitPosition(Vector3 focus, float usedDistance)
    {
        Quaternion orbitRot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 offset = orbitRot * new Vector3(0f, 0f, -usedDistance);
        return focus + offset;
    }
}