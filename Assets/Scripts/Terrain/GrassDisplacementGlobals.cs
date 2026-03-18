using UnityEngine;

public class GrassDisplacementGlobals : MonoBehaviour
{
    [Header("Dice Sources")]
    public Transform diceA;
    public Transform diceB;

    [Header("Shader Settings")]
    [SerializeField] private float radius = 3.5f;
    [SerializeField] private float strength = 0.6f;
    [SerializeField] private float diceCenterYOffset = -0.35f;

    private static readonly int DiceAId = Shader.PropertyToID("_DiceA");
    private static readonly int DiceBId = Shader.PropertyToID("_DiceB");
    private static readonly int InteractionDistanceId = Shader.PropertyToID("_InteractionDistance");
    private static readonly int DisplacementStrengthId = Shader.PropertyToID("_DisplacementStrength");

    private static readonly Vector4 DisabledPos = new Vector4(999999f, 999999f, 999999f, 0f);

    private void OnEnable()
    {
        Debug.Log("GrassDisplacementGlobals ENABLED");
    }

    private void Start()
    {
        Debug.Log("GrassDisplacementGlobals START");
    }

    private void LateUpdate()
    {
        Shader.SetGlobalFloat(InteractionDistanceId, radius);
        Shader.SetGlobalFloat(DisplacementStrengthId, strength);

        Push(DiceAId, diceA);
        Push(DiceBId, diceB);
    }

    private void Push(int id, Transform t)
    {
        if (t == null)
        {
            Shader.SetGlobalVector(id, DisabledPos);
            return;
        }

        Vector3 p = t.position + Vector3.up * diceCenterYOffset;
        Shader.SetGlobalVector(id, new Vector4(p.x, p.y, p.z, 1f));
    }

    public void SetDice(Transform a, Transform b)
    {
        diceA = a;
        diceB = b;
        Debug.Log($"SetDice called. A={(a ? a.name : "null")} B={(b ? b.name : "null")}");
    }

    public void ClearDice()
    {
        diceA = null;
        diceB = null;
        Debug.Log("ClearDice called");
    }
}