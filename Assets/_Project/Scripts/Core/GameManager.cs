// =================================================================================================
// Proyecto: The Pumpkin Ritual - La Hora de las Brujas (TP Programación 2 - UNICABA)
// Autora: Tamara Maciel
// Correo: tmaciel96@gmail.com
// Repositorio: https://github.com/tmaciel96/PumpkinPolyTP
// 
// Descripción del script:
// Gestor principal del juego (Singleton). Controla el estado global de la partida (intro, juego activo,
// victoria y derrota), el temporizador ascendente de la "Hora de las Brujas" (00:00 a 03:33 = 213s),
// el conteo de purificación de los 3 altares rúnicos, penalizaciones de tiempo al morir (+20s),
// actualización del HUD de vida y munición, y el cableado automático de botones en pantallas finales.
// =================================================================================================

using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Cronómetro (Comienza en 00:00 y finaliza a las 3:33 = 213s)")]
    public float timeElapsed = 0f;
    public float maxTime = 213f; // 3 min 33 seg (La Hora de las Brujas)
    public bool timerIsActive = true;
    public TextMeshProUGUI timerText;

    [Header("Objetivos")]
    public int totalRunes = 3;
    public int runesActivated = 0;
    public TextMeshProUGUI runesText;
    public ExitPortal escapePortal;

    [Header("HUD de Salud y Munición")]
    public Slider healthSlider;
    public Image healthFillImage;
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI ammoText;
    public TextMeshProUGUI interactionPrompt;

    [Header("Efecto de Daño en Pantalla (Vignette / Flash Rojo)")]
    public Image damageVignette;
    public float vignetteFadeSpeed = 3f;
    private Coroutine vignetteCoroutine;

    [Header("Paneles de Fin de Juego")]
    public GameObject victoryPanel;
    public GameObject defeatPanel;
    public TextMeshProUGUI defeatReasonText;

    private PlayerHealth playerHealth;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        Time.timeScale = 1f;

        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (defeatPanel != null) defeatPanel.SetActive(false);
        if (interactionPrompt != null) interactionPrompt.gameObject.SetActive(false);

        // Auto-encontrar portal si no fue asignado en el Inspector
        if (escapePortal == null)
        {
            escapePortal = FindFirstObjectByType<ExitPortal>();
        }

        // Auto-encontrar imagen Fill del slider si no fue asignada
        if (healthFillImage == null && healthSlider != null)
        {
            if (healthSlider.fillRect != null)
            {
                healthFillImage = healthSlider.fillRect.GetComponent<Image>();
            }
        }

        // CRÍTICO: La viñeta de daño NUNCA debe bloquear clics del mouse (Raycast Target = false)
        if (damageVignette != null)
        {
            damageVignette.raycastTarget = false;
            Color c = damageVignette.color;
            c.a = 0f;
            damageVignette.color = c;
            damageVignette.gameObject.SetActive(true);
        }

        // Auto-vincular botones de los paneles para que funcionen 100% garantizados
        AutoWirePanelButtons(victoryPanel);
        AutoWirePanelButtons(defeatPanel);

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerHealth = playerObj.GetComponent<PlayerHealth>();
        }

        PlayerHealth.OnHealthChanged += UpdateHealthBar;
        PlayerHealth.OnPlayerDamaged += TriggerDamageVignette;
        StoneInventory.OnStonesChanged += UpdateStonesDisplay;
        PlayerInteractor.OnFocusChanged += ShowInteractionPrompt;

        UpdateRunesDisplay();
        UpdateTimerDisplay(timeElapsed);

        // El inventario pudo avisar antes de que el HUD se suscribiera: pedir el valor actual
        StoneInventory stones = playerObj != null ? playerObj.GetComponent<StoneInventory>() : null;
        if (stones != null) stones.Notify();
    }

    /// <summary>
    /// Vincula automáticamente los botones Reiniciar y Menú para que respondan al clic sin fallar.
    /// </summary>
    private void AutoWirePanelButtons(GameObject panel)
    {
        if (panel == null) return;
        Button[] buttons = panel.GetComponentsInChildren<Button>(true);
        foreach (var btn in buttons)
        {
            string bName = btn.gameObject.name.ToLower();
            TextMeshProUGUI tmpText = btn.GetComponentInChildren<TextMeshProUGUI>();
            string tText = tmpText != null ? tmpText.text.ToLower() : "";

            if (bName.Contains("restart") || bName.Contains("reiniciar") || bName.Contains("reintentar") ||
                tText.Contains("reiniciar") || tText.Contains("reintentar") || tText.Contains("restart"))
            {
                btn.onClick.RemoveListener(RestartLevel);
                btn.onClick.AddListener(RestartLevel);
            }
            else if (bName.Contains("menu") || bName.Contains("salir") || bName.Contains("main") ||
                     tText.Contains("menu") || tText.Contains("menú") || tText.Contains("salir"))
            {
                btn.onClick.RemoveListener(LoadMainMenu);
                btn.onClick.AddListener(LoadMainMenu);
            }
        }
    }

    void OnDestroy()
    {
        PlayerHealth.OnHealthChanged -= UpdateHealthBar;
        PlayerHealth.OnPlayerDamaged -= TriggerDamageVignette;
        StoneInventory.OnStonesChanged -= UpdateStonesDisplay;
        PlayerInteractor.OnFocusChanged -= ShowInteractionPrompt;
    }

    void Update()
    {
        if (!timerIsActive) return;

        if (timeElapsed < maxTime)
        {
            timeElapsed += Time.deltaTime;
            UpdateTimerDisplay(timeElapsed);
        }
        else
        {
            timeElapsed = maxTime;
            timerIsActive = false;
            UpdateTimerDisplay(timeElapsed);
            OnTimeExpired();
        }
    }

    private void UpdateTimerDisplay(float time)
    {
        if (timerText == null) return;

        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);
        timerText.text = string.Format("{0:0}:{1:00}", minutes, seconds);

        if (time >= maxTime - 30f)
        {
            timerText.color = Color.red;
        }
        else
        {
            timerText.color = Color.white;
        }
    }

    private void UpdateRunesDisplay()
    {
        if (runesText != null)
        {
            runesText.text = $"Altares: {runesActivated} / {totalRunes}";
        }
    }

    private void UpdateHealthBar(int current, int max)
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = max;
            healthSlider.value = current;
        }

        float pct = max > 0 ? (float)current / max : 0f;

        if (healthFillImage != null)
        {
            if (pct > 0.5f)
            {
                healthFillImage.color = new Color(0.2f, 0.85f, 0.2f);
            }
            else if (pct > 0.25f)
            {
                healthFillImage.color = new Color(1f, 0.75f, 0.1f);
            }
            else
            {
                healthFillImage.color = new Color(0.9f, 0.15f, 0.15f);
            }
        }

        if (healthText != null)
        {
            healthText.text = $"Vida: {current} / {max} HP";
        }
    }

    /// <summary>M1-4: contador de piedras (usa el mismo texto que antes mostraba la munición).</summary>
    private void UpdateStonesDisplay(int current, int max)
    {
        if (ammoText == null) return;

        string colorTag = current <= 0 ? "<color=red>" : "<color=white>";
        ammoText.text = $"Piedras: {colorTag}{current}</color> / {max}";
    }

    private void TriggerDamageVignette()
    {
        if (damageVignette == null) return;
        if (vignetteCoroutine != null) StopCoroutine(vignetteCoroutine);
        vignetteCoroutine = StartCoroutine(DamageVignetteRoutine());
    }

    private IEnumerator DamageVignetteRoutine()
    {
        Color c = Color.red;
        c.a = 0.45f;
        damageVignette.color = c;

        while (damageVignette.color.a > 0.01f)
        {
            c.a = Mathf.MoveTowards(c.a, 0f, vignetteFadeSpeed * Time.deltaTime);
            damageVignette.color = c;
            yield return null;
        }

        c.a = 0f;
        damageVignette.color = c;
    }

    // El texto "Presiona [E] para ..." lo decide el PlayerInteractor (M1-7) con el interactuable más cercano
    private void ShowInteractionPrompt(IInteractable focused)
    {
        if (interactionPrompt == null) return;

        bool show = focused != null;
        interactionPrompt.gameObject.SetActive(show);
        if (show) interactionPrompt.text = $"Presiona [E] para {focused.InteractionPrompt}";
    }

    public void AddTime(float amount)
    {
        timeElapsed = Mathf.Max(0f, timeElapsed - amount);
        UpdateTimerDisplay(timeElapsed);
    }

    public void ActivateRune()
    {
        runesActivated++;
        UpdateRunesDisplay();

        if (runesActivated >= totalRunes)
        {
            if (escapePortal == null)
            {
                escapePortal = FindFirstObjectByType<ExitPortal>();
            }

            if (escapePortal != null)
            {
                escapePortal.UnlockPortal();
            }
        }
    }

    public void OnPlayerDied()
    {
        if (playerHealth != null)
        {
            playerHealth.Respawn();
        }

        timeElapsed = Mathf.Min(maxTime, timeElapsed + 20f);
        UpdateTimerDisplay(timeElapsed);
    }

    public void OnTimeExpired()
    {
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (defeatPanel != null)
        {
            defeatPanel.SetActive(true);
            defeatPanel.transform.SetAsLastSibling(); // Poner al frente de cualquier otra imagen
            if (defeatReasonText != null)
            {
                defeatReasonText.text = "¡El reloj marcó las 3:33, es la Hora de las Brujas y las sombras consumieron el cementerio!";
            }
        }
    }

    public void OnPlayerEscaped()
    {
        Debug.Log("<color=green>¡Victoria! Escapaste por el portal rúnico.</color>");
        timerIsActive = false;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
            victoryPanel.transform.SetAsLastSibling(); // Poner al frente de cualquier otra imagen
        }
    }

    public void RestartLevel()
    {
        Debug.Log("<color=cyan>Reiniciando nivel...</color>");
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void LoadMainMenu()
    {
        Debug.Log("<color=cyan>Cargando Menú Principal (00_MainMenu)...</color>");
        Time.timeScale = 1f;
        SceneManager.LoadScene("00_MainMenu");
    }
}
