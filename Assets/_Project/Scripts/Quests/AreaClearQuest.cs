// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// AreaClearQuest (M3-2, M3-6): misiones de "limpiar el área". Va en el mismo objeto que el
// NPCInteractable del Guardián Esqueleto (zombis) o del Niño Fantasma (espectros) e implementa
// IQuestDelivery:
//   - Al dar la misión activa los objetos de "Activar al empezar" (los CursedSpawner del sector, o un
//     grupo de enemigos apagado) y reclama los QuestEnemy que haya dentro del área.
//   - Cada enemigo que aparece dentro del área mientras la misión está en curso pasa a ser suyo;
//     cuando muere suma una baja (aunque se haya alejado del área persiguiendo al jugador).
//   - Está lista para entregar cuando hubo al menos "Bajas requeridas" y no queda ninguno suyo vivo.
// "Bajas requeridas" en 0 suma el "Enemies To Spawn" de los CursedSpawner de "Activar al empezar".
// Los spawners se dejan apagados en la escena: así no se disparan antes de que el NPC dé la misión.
// =================================================================================================

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class AreaClearQuest : MonoBehaviour, IQuestDelivery
{
    [Header("Misión")]
    public QuestId quest = QuestId.Esqueleto;
    [Tooltip("Bajas mínimas (0 = la suma de Enemies To Spawn de los CursedSpawner a activar)")]
    public int requiredKills = 0;
    [Tooltip("Texto del progreso, por ejemplo \"Zombis\" o \"Espectros\"")]
    public string enemyLabel = "Zombis";

    [Header("Área")]
    [Tooltip("Centro del área (vacío = este objeto)")]
    public Transform areaCenter;
    public float areaRadius = 15f;

    [Header("Al dar la misión")]
    [Tooltip("Spawners o grupos de enemigos del sector, apagados en la escena")]
    public GameObject[] activateOnQuestStart;
    [Tooltip("Disparar los CursedSpawner activados sin esperar a que el jugador se acerque")]
    public bool triggerSpawnersImmediately = false;

    [Header("HUD (opcional)")]
    public TextMeshProUGUI progressText;

    [Header("Eventos")]
    [Tooltip("Al eliminar al último enemigo (antes de volver a hablar con el NPC)")]
    public UnityEvent onAreaCleared;
    public UnityEvent onDelivered;

    public int Kills { get; private set; }
    public int RequiredKills { get; private set; }
    public int AliveCount => owned.Count;
    public bool IsReadyToDeliver => QuestManager.IsInProgress(quest) && claimedEver > 0 && owned.Count == 0 && Kills >= RequiredKills;

    private readonly HashSet<QuestEnemy> owned = new HashSet<QuestEnemy>();
    private int claimedEver;
    private bool clearedNotified;

    void Reset()
    {
        NPCInteractable npc = GetComponent<NPCInteractable>();
        if (npc != null) quest = npc.quest;
        enemyLabel = quest == QuestId.NinoFantasma ? "Espectros" : "Zombis";
    }

    void OnEnable()
    {
        QuestEnemy.OnEnemyAppeared += HandleAppeared;
        QuestEnemy.OnEnemyRemoved += HandleRemoved;
        QuestManager.OnQuestStateChanged += HandleQuestChanged;
    }

    void OnDisable()
    {
        QuestEnemy.OnEnemyAppeared -= HandleAppeared;
        QuestEnemy.OnEnemyRemoved -= HandleRemoved;
        QuestManager.OnQuestStateChanged -= HandleQuestChanged;
    }

    void Start()
    {
        RequiredKills = requiredKills;
        if (RequiredKills <= 0 && activateOnQuestStart != null)
            foreach (GameObject go in activateOnQuestStart)
                if (go != null)
                    foreach (CursedSpawner s in go.GetComponentsInChildren<CursedSpawner>(true)) RequiredKills += s.enemiesToSpawn;

        // Si la misión ya estaba en curso (menú de debug) arranca igual
        if (QuestManager.IsInProgress(quest)) BeginQuest();
        RefreshText();
    }

    // ---------------------------------------------------------------- Misión

    private void HandleQuestChanged(QuestId id, QuestState state)
    {
        if (id != quest) return;
        if (state == QuestState.EnProgreso) BeginQuest();
        RefreshText();
    }

    private void BeginQuest()
    {
        if (activateOnQuestStart != null)
        {
            foreach (GameObject go in activateOnQuestStart)
            {
                if (go == null) continue;
                go.SetActive(true);
                if (triggerSpawnersImmediately)
                    foreach (CursedSpawner s in go.GetComponentsInChildren<CursedSpawner>()) s.TriggerSpawner();
            }
        }
        foreach (QuestEnemy e in new List<QuestEnemy>(QuestEnemy.Alive)) TryClaim(e);
    }

    private void HandleAppeared(QuestEnemy enemy) => TryClaim(enemy);

    private void TryClaim(QuestEnemy enemy)
    {
        if (enemy == null || enemy.Owner != null || !QuestManager.IsInProgress(quest) || !InArea(enemy.transform.position)) return;
        enemy.Owner = this;
        owned.Add(enemy);
        claimedEver++;
        RefreshText();
    }

    private void HandleRemoved(QuestEnemy enemy)
    {
        if (enemy == null || enemy.Owner != this || !owned.Remove(enemy)) return;
        Kills++;
        RefreshText();
        if (IsReadyToDeliver && !clearedNotified)
        {
            clearedNotified = true;
            Debug.Log($"<color=yellow>[AreaClearQuest] {quest}: área limpia ({Kills} bajas).</color>");
            onAreaCleared?.Invoke();
        }
    }

    private bool InArea(Vector3 pos)
    {
        Vector3 c = areaCenter != null ? areaCenter.position : transform.position;
        Vector3 d = pos - c;
        d.y = 0f;
        return d.sqrMagnitude <= areaRadius * areaRadius;
    }

    // ---------------------------------------------------------------- IQuestDelivery

    public void Deliver()
    {
        onDelivered?.Invoke();
        RefreshText();
    }

    // ---------------------------------------------------------------- HUD

    private void RefreshText()
    {
        if (progressText == null) return;
        bool show = QuestManager.IsInProgress(quest);
        progressText.gameObject.SetActive(show);
        if (!show) return;
        progressText.text = IsReadyToDeliver
            ? "Área limpia: volvé a hablar"
            : RequiredKills > 0 ? $"{enemyLabel}: {Kills} / {RequiredKills}" : $"{enemyLabel} restantes: {owned.Count}";
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.9f, 0.2f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(areaCenter != null ? areaCenter.position : transform.position, areaRadius);
    }
}
