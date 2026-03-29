using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyFightController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerTravelController travelController;
    [SerializeField] private EnemyHealthBarUI enemyHealthBar;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip swordHitClip;
    [SerializeField] private RunState runState;

    [Header("Enemy Health Range")]
    [SerializeField] private int minEnemyHp = 20;
    [SerializeField] private int maxEnemyHp = 30;

    [Header("Player Damage Range")]
    [SerializeField] private int minPlayerDamage = 1;
    [SerializeField] private int maxPlayerDamage = 12;

    [Header("Enemy Damage Range")]
    [SerializeField] private int minEnemyDamage = 10;
    [SerializeField] private int maxEnemyDamage = 40;

    [Header("Attack Motion")]
    [SerializeField] private float strikeDistanceFromTarget = 0.35f;
    [SerializeField] private float strikeTime = 0.25f;
    [SerializeField] private float strikeHopHeight = 0.9f;
    [SerializeField] private float impactPause = 0.10f;
    [SerializeField] private float returnTime = 0.22f;
    [SerializeField] private float returnHopHeight = 0.7f;
    
    [SerializeField] private EventPopupUI eventPopupUI;
    [SerializeField] private Sprite skullSprite;
    
    private readonly Dictionary<Transform, Vector3> combatHomePos = new();
    private readonly Dictionary<Transform, Quaternion> combatHomeRot = new();

    private GameObject activeEnemy;
    private int currentEnemyHp;
    private bool fightActive;

    private readonly Dictionary<Transform, int> allyPartyIndex = new();

    public bool FightActive => fightActive;
    public GameObject ActiveEnemy => activeEnemy;
    public int CurrentEnemyHp => currentEnemyHp;

    private void Awake()
    {
        if (!travelController)
            travelController = FindFirstObjectByType<PlayerTravelController>();

        if (!enemyHealthBar)
            enemyHealthBar = FindFirstObjectByType<EnemyHealthBarUI>(FindObjectsInactive.Include);

        if (!sfxSource)
            sfxSource = GetComponent<AudioSource>();

        if (!sfxSource)
            sfxSource = gameObject.AddComponent<AudioSource>();
        
        if (!runState)
            runState = FindFirstObjectByType<RunState>();
        
        if (!eventPopupUI)
            eventPopupUI = FindFirstObjectByType<EventPopupUI>(FindObjectsInactive.Include);

        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;
    }

    public void StartFight(GameObject enemy, string enemyName)
    {
        if (enemy == null)
        {
            Debug.LogError("EnemyFightController: StartFight called with null enemy.");
            return;
        }

        activeEnemy = enemy;
        currentEnemyHp = Random.Range(minEnemyHp, maxEnemyHp + 1);
        fightActive = true;

        BuildPartyHpTable();
        CacheCombatHomes();
        RefreshPartyUI();
        
        if (enemyHealthBar)
            enemyHealthBar.ShowEnemy(enemyName, currentEnemyHp);

        StartCoroutine(CombatLoop());
    }

    private void BuildPartyHpTable()
    {
        allyPartyIndex.Clear();

        if (travelController == null)
            return;

        List<Transform> targets = travelController.GetCombatTargets();
        if (targets == null)
            return;

        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] != null)
                allyPartyIndex[targets[i]] = i;
        }
    }

    private IEnumerator CombatLoop()
    {
        while (fightActive && activeEnemy != null)
        {
            int playerDamage = 0;
            yield return WaitForPlayerDiceRoll(v => playerDamage = v);

            Transform playerTarget = travelController != null && travelController.Player != null
                ? travelController.Player.transform
                : null;

            if (playerTarget == null)
            {
                LoseFight();
                yield break;
            }

            yield return AttackHop(playerTarget, activeEnemy.transform);

            currentEnemyHp = Mathf.Max(0, currentEnemyHp - playerDamage);

            if (enemyHealthBar)
                enemyHealthBar.SetHealth(currentEnemyHp);
            
            var partyMembers = GetPartyMembersOnly();

            for (int i = 0; i < partyMembers.Count; i++)
            {
                Transform member = partyMembers[i];
                if (member == null) continue;

                int dmg = Random.Range(1, 7);

                yield return AttackHop(member, activeEnemy.transform);

                currentEnemyHp = Mathf.Max(0, currentEnemyHp - dmg);

                if (enemyHealthBar)
                    enemyHealthBar.SetHealth(currentEnemyHp);

                Debug.Log($"Party member {i} dealt {dmg}. Enemy HP: {currentEnemyHp}");

                if (currentEnemyHp <= 0)
                {
                    WinFight();
                    yield break;
                }

                yield return new WaitForSeconds(0.2f); 
            }
            RefreshPartyUI();

            if (enemyHealthBar)
                enemyHealthBar.SetHealth(currentEnemyHp);

            Debug.Log($"Player dealt {playerDamage} damage. Enemy HP now {currentEnemyHp}.");

            if (currentEnemyHp <= 0)
            {
                WinFight();
                yield break;
            }

            yield return new WaitForSeconds(0.12f);

            Transform chosenTarget = ChooseRandomLivingAlly();
            if (chosenTarget == null)
            {
                LoseFight();
                yield break;
            }

            int enemyDamage = Random.Range(minEnemyDamage, maxEnemyDamage + 1);
            yield return AttackHop(activeEnemy.transform, chosenTarget);

            if (allyPartyIndex.TryGetValue(chosenTarget, out int partyIndex) &&
                runState != null &&
                runState.party != null &&
                partyIndex >= 0 &&
                partyIndex < runState.party.members.Count)
            {
                var member = runState.party.members[partyIndex];
                if (member != null)
                    member.ApplyDamage(enemyDamage);
            }

            RefreshPartyUI();

            yield return ProcessCombatDeathsAfterHit("Your caravan leader was slain by bandits.");

            if (!fightActive)
                yield break;

            if (AllAlliesDead())
            {
                LoseFight();
                yield break;
            }

            yield return new WaitForSeconds(0.15f);
        }
    }

    private IEnumerator WaitForPlayerDiceRoll(System.Action<int> onRolled)
    {
        if (travelController == null || travelController.DicePrefab == null)
            yield break;
        
        SetPhysicsEnabled(true);

        DiceController diceA = Instantiate(travelController.DicePrefab).GetComponent<DiceController>();
        DiceController diceB = Instantiate(travelController.DicePrefab).GetComponent<DiceController>();

        if (diceA == null || diceB == null)
        {
            Debug.LogError("EnemyFightController: Dice prefab missing DiceController.");
            yield break;
        }

        diceA.cameraOffset = new Vector3(-4f, -0.25f, 10f);
        diceB.cameraOffset = new Vector3( 4f, -0.25f, 10f);

        diceA.spinAxis = new Vector3(0.35f, 1f, 0.2f);
        diceB.spinAxis = new Vector3(-0.28f, 1f, -0.18f);

        diceA.transform.rotation = Random.rotation;
        diceB.transform.rotation = Random.rotation;

        bool aReady = false;
        bool bReady = false;
        int aValue = 0;
        int bValue = 0;

        diceA.OnRolled += v =>
        {
            aValue = v;
            aReady = true;
        };

        diceB.OnRolled += v =>
        {
            bValue = v;
            bReady = true;
        };

        while (!aReady || !bReady)
            yield return null;

        int total = Mathf.Clamp(aValue + bValue, minPlayerDamage, maxPlayerDamage);
        onRolled?.Invoke(total);

        StartCoroutine(ShrinkAndDestroy(diceA.gameObject, 0.12f));
        StartCoroutine(ShrinkAndDestroy(diceB.gameObject, 0.12f));
        
        SetPhysicsEnabled(false);
        yield return ResetAfterDice();
    }

    private IEnumerator AttackHop(Transform attacker, Transform target)
    {
        if (attacker == null || target == null || travelController == null)
            yield break;

        Vector3 startPos = travelController.GetGroundedCombatPosition(attacker.position);
        Quaternion startRot = attacker.rotation;
        Vector3 targetScale = target.localScale;

        Vector3 toTarget = target.position - attacker.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude < 0.001f)
            toTarget = Vector3.forward;

        toTarget.Normalize();

        Quaternion faceRot = Quaternion.LookRotation(toTarget, Vector3.up);

        Vector3 hitPos = target.position - toTarget * strikeDistanceFromTarget;
        hitPos = travelController.GetGroundedCombatPosition(hitPos);

        yield return ArcHop(attacker, startPos, hitPos, faceRot, strikeTime, strikeHopHeight);

        PlaySwordHitSound();

        target.localScale = new Vector3(
            targetScale.x * 1.25f,
            targetScale.y * 0.72f,
            targetScale.z * 1.25f
        );

        yield return new WaitForSeconds(0.06f);
        target.localScale = targetScale;

        yield return new WaitForSeconds(impactPause);

        yield return ArcHop(attacker, hitPos, startPos, startRot, returnTime, strikeHopHeight);

        attacker.position = startPos;
        attacker.rotation = startRot;
        target.localScale = targetScale;
    }
    
    private IEnumerator ArcHop(
        Transform mover,
        Vector3 start,
        Vector3 end,
        Quaternion targetRot,
        float duration,
        float height)
    {
        if (mover == null || travelController == null)
            yield break;

        float t = 0f;
        float dur = Mathf.Max(0.05f, duration);
        
        Vector3 groundedStart = travelController.GetGroundedCombatPosition(start);
        Vector3 groundedEnd   = travelController.GetGroundedCombatPosition(end);

        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            float s = u * u * (3f - 2f * u);
            
            Vector3 basePos = Vector3.Lerp(groundedStart, groundedEnd, s);

            float arc = Mathf.Sin(u * Mathf.PI) * height;

            mover.position = basePos + Vector3.up * arc;

            mover.rotation = Quaternion.Slerp(
                mover.rotation,
                targetRot,
                1f - Mathf.Exp(-14f * Time.deltaTime)
            );

            yield return null;
        }
        
        mover.position = groundedEnd;
        mover.rotation = targetRot;
    }

    private Transform ChooseRandomLivingAlly()
    {
        if (runState == null || runState.party == null || runState.party.members == null)
            return null;

        List<Transform> living = new List<Transform>();

        foreach (var kv in allyPartyIndex)
        {
            Transform tr = kv.Key;
            int partyIndex = kv.Value;

            if (tr == null) continue;
            if (partyIndex < 0 || partyIndex >= runState.party.members.Count) continue;

            var member = runState.party.members[partyIndex];
            if (member == null) continue;
            if (member.currentHealth > 0)
                living.Add(tr);
        }

        if (living.Count == 0)
            return null;

        return living[Random.Range(0, living.Count)];
    }

    private bool AllAlliesDead()
    {
        if (runState == null || runState.party == null || runState.party.members == null)
            return true;

        for (int i = 0; i < runState.party.members.Count; i++)
        {
            var member = runState.party.members[i];
            if (member != null && member.currentHealth > 0)
                return false;
        }

        return true;
    }

    private void PlaySwordHitSound()
    {
        if (sfxSource != null && swordHitClip != null)
            sfxSource.PlayOneShot(swordHitClip, 1f);
    }

    private IEnumerator ShrinkAndDestroy(GameObject go, float duration)
    {
        if (go == null)
            yield break;

        Vector3 startScale = go.transform.localScale;
        float t = 0f;

        while (t < duration && go != null)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            go.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, u);
            yield return null;
        }

        if (go != null)
            Destroy(go);
    }

    private void WinFight()
    {
        Debug.Log("Enemy defeated.");

        int goldReward = Random.Range(30, 81);
        
        RunState runState = FindFirstObjectByType<RunState>();
        if (runState != null)
            runState.resources.gold += goldReward;

        TravelEventManager mgr = FindFirstObjectByType<TravelEventManager>();
        if (mgr != null)
        {
            mgr.ShowBanditVictoryPopup(goldReward, () =>
            {
                EndFight(true);
            });
        }
        else
        {
            EndFight(true);
        }
    }

    private void LoseFight()
    {
        Debug.Log("Party defeated.");
        EndFight(true);

        TriggerLeaderDeathGameOver("Your caravan leader was slain.");
    }

    private void EndFight(bool destroyEnemy)
    {
        fightActive = false;

        if (enemyHealthBar)
            enemyHealthBar.Hide();
        
        var partyHUD = FindFirstObjectByType<PartyHUDController>();
        if (partyHUD)
        {
            partyHUD.ClearCombatOverride();
        }
        
        if (travelController != null)
        {
            StartCoroutine(travelController.RestoreCompanionsAfterCombat());
        }

        TravelEventManager mgr = FindFirstObjectByType<TravelEventManager>();
        if (mgr != null)
            mgr.FinishActiveEnemyEvent(destroyEnemy);

        activeEnemy = null;
    }
    
    private void CacheCombatHomes()
    {
        combatHomePos.Clear();
        combatHomeRot.Clear();

        if (travelController == null)
            return;
        
        var targets = travelController.GetCombatTargets();

        foreach (var t in targets)
        {
            if (t == null) continue;

            combatHomePos[t] = t.position;
            combatHomeRot[t] = t.rotation;
        }
        
        if (activeEnemy != null)
        {
            Transform e = activeEnemy.transform;
            combatHomePos[e] = e.position;
            combatHomeRot[e] = e.rotation;
        }
    }
    
    private void SetPhysicsEnabled(bool enabled)
    {
        foreach (var kv in combatHomePos)
        {
            if (kv.Key == null) continue;

            Rigidbody rb = kv.Key.GetComponent<Rigidbody>();
            if (rb)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = !enabled;
                rb.useGravity = enabled;
            }
        }
    }
    
    private IEnumerator ResetAfterDice(float duration = 0.25f)
    {
        float t = 0f;

        var startPos = new Dictionary<Transform, Vector3>();
        var startRot = new Dictionary<Transform, Quaternion>();

        foreach (var kv in combatHomePos)
        {
            if (kv.Key == null) continue;

            startPos[kv.Key] = kv.Key.position;
            startRot[kv.Key] = kv.Key.rotation;
        }

        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            float s = u * u * (3f - 2f * u);

            foreach (var kv in combatHomePos)
            {
                Transform tr = kv.Key;
                if (tr == null) continue;

                tr.position = Vector3.Lerp(startPos[tr], kv.Value, s);
                tr.rotation = Quaternion.Slerp(startRot[tr], combatHomeRot[tr], s);
            }

            yield return null;
        }

        foreach (var kv in combatHomePos)
        {
            if (kv.Key == null) continue;

            kv.Key.position = kv.Value;
            kv.Key.rotation = combatHomeRot[kv.Key];
        }
        
        CacheCombatHomes();
    }
    
    private void RefreshPartyUI()
    {
        if (travelController == null || runState == null || runState.party == null)
            return;

        var partyHUD = FindFirstObjectByType<PartyHUDController>();
        if (partyHUD == null)
            return;

        var targets = travelController.GetCombatTargets();

        for (int i = 0; i < targets.Count; i++)
        {
            if (i < 0 || i >= runState.party.members.Count)
                continue;

            var member = runState.party.members[i];
            if (member == null)
                continue;

            partyHUD.SetMemberHealth(i, member.currentHealth, member.maxHealth);
        }

        partyHUD.Refresh();
    }
    
    private List<Transform> GetPartyMembersOnly()
    {
        var all = travelController.GetCombatTargets();
        var list = new List<Transform>();

        if (all == null || all.Count == 0)
            return list;
        
        for (int i = 1; i < all.Count; i++)
        {
            if (all[i] != null)
                list.Add(all[i]);
        }

        return list;
    }
    
    private bool TriggerLeaderDeathGameOver(string reason)
    {
        if (runState == null || !runState.IsLeaderDead())
            return false;

        var gameOver = FindFirstObjectByType<GameOverManager>();
        if (gameOver != null && !gameOver.IsGameOverTriggered)
            gameOver.TriggerGameOver(reason);

        return true;
    }
    
    private IEnumerator ProcessCombatDeathsAfterHit(string leaderDeathText)
    {
        if (runState == null || runState.party == null || runState.party.members == null)
            yield break;

        if (runState.IsLeaderDead())
        {
            EndFight(true);

            var gameOver = FindFirstObjectByType<GameOverManager>();
            if (gameOver != null && !gameOver.IsGameOverTriggered)
                gameOver.TriggerGameOver(leaderDeathText);

            yield break;
        }

        for (int i = runState.party.members.Count - 1; i >= 1; i--)
        {
            var member = runState.party.members[i];
            if (member == null || !member.IsDead())
                continue;

            string deadName = string.IsNullOrWhiteSpace(member.memberName) ? "A companion" : member.memberName;

            bool acknowledged = false;

            if (eventPopupUI != null)
            {
                eventPopupUI.ShowSimpleEvent(
                    "Companion Lost",
                    $"{deadName} was killed in battle. \n You will have to leave behind what they were carrying.",
                    skullSprite,
                    "OK",
                    () => { acknowledged = true; }
                );

                yield return new WaitUntil(() => acknowledged);
            }

            if (travelController != null)
                travelController.RemoveCompanionAtPartyIndex(i);

            runState.party.TryRemoveMemberAt(i);

            BuildPartyHpTable();
            CacheCombatHomes();
            RefreshPartyUI();
        }
    }
    
}