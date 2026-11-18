// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// AudioController: música del juego (Singleton persistente).
//  - Va UNA sola vez en 00_MainMenu; sobrevive a los cambios de escena (DontDestroyOnLoad).
//  - Menú principal: menuMusic. Nivel: explorationMusic mientras no haya misión en progreso.
//  - Cada misión (desafío) tiene su propia música: suena cuando pasa a En progreso y, al completarse,
//    se vuelve a la de exploración. Con la victoria suena victoryMusic.
//  - No conoce a las misiones: escucha QuestManager.OnQuestStateChanged y GameManager.OnStateChanged.
//  - Cambia de tema con fundido cruzado entre dos AudioSource, en tiempo real (unscaled) para que
//    funcione aunque la victoria ponga Time.timeScale = 0.
//  - PlaySfx(clip) queda disponible para efectos sueltos (botones, recoger objetos, etc.).
// =================================================================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioController : MonoBehaviour
{
    public static AudioController Instance { get; private set; }

    [Serializable]
    public struct QuestMusic
    {
        public QuestId quest;
        public AudioClip clip;
    }

    [Header("Escenas")]
    [Tooltip("Nombre exacto de la escena del menú principal")]
    public string menuSceneName = "00_MainMenu";

    [Header("Música general")]
    public AudioClip menuMusic;
    [Tooltip("Suena en el nivel cuando no hay ninguna misión en progreso")]
    public AudioClip explorationMusic;
    public AudioClip victoryMusic;

    [Header("Música de cada desafío")]
    [Tooltip("Una entrada por misión. Si una misión no tiene clip, sigue la música de exploración")]
    public QuestMusic[] questMusic =
    {
        new QuestMusic { quest = QuestId.Bruja },
        new QuestMusic { quest = QuestId.Esqueleto },
        new QuestMusic { quest = QuestId.Vampiro },
        new QuestMusic { quest = QuestId.NinoFantasma },
        new QuestMusic { quest = QuestId.GatoNegro },
    };

    [Header("Volumen y fundido")]
    [Range(0f, 1f)] public float musicVolume = 0.6f;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Tooltip("Segundos que tarda en pasar de un tema a otro")]
    public float crossfadeTime = 1.5f;

    private AudioSource[] musicSources;
    private int activeSource;
    private AudioSource sfxSource;
    private Coroutine fadeRoutine;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        musicSources = new AudioSource[2];
        for (int i = 0; i < musicSources.Length; i++)
        {
            AudioSource src = gameObject.AddComponent<AudioSource>();
            src.loop = true;
            src.playOnAwake = false;
            src.volume = 0f;
            musicSources[i] = src;
        }

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        QuestManager.OnQuestStateChanged += HandleQuestChanged;
        GameManager.OnStateChanged += HandleGameStateChanged;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        QuestManager.OnQuestStateChanged -= HandleQuestChanged;
        GameManager.OnStateChanged -= HandleGameStateChanged;
    }

    void Start()
    {
        // La escena en la que arranca no dispara sceneLoaded para este objeto: elegir la música a mano
        PlayForScene(SceneManager.GetActiveScene());
    }

    // ---------------------------------------------------------------- Eventos

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) PlayForScene(scene);
    }

    private void HandleQuestChanged(QuestId id, QuestState state)
    {
        if (state == QuestState.EnProgreso)
        {
            AudioClip clip = GetQuestClip(id);
            PlayMusic(clip != null ? clip : explorationMusic);
        }
        else if (state == QuestState.Completada)
        {
            PlayMusic(explorationMusic);
        }
    }

    private void HandleGameStateChanged(GameManager.GameState state)
    {
        if (state == GameManager.GameState.Victoria && victoryMusic != null)
            PlayMusic(victoryMusic);
    }

    private void PlayForScene(Scene scene)
    {
        // Al cargar el nivel (o reiniciarlo) el QuestManager vuelve todo a cero: arranca con exploración
        PlayMusic(scene.name == menuSceneName ? menuMusic : explorationMusic);
    }

    private AudioClip GetQuestClip(QuestId id)
    {
        foreach (QuestMusic qm in questMusic)
            if (qm.quest == id) return qm.clip;
        return null;
    }

    // ---------------------------------------------------------------- API pública

    /// <summary>Cambia la música con fundido cruzado. Si ya está sonando ese clip, no hace nada.</summary>
    public void PlayMusic(AudioClip clip)
    {
        AudioSource current = musicSources[activeSource];
        if (clip != null && current.clip == clip && current.isPlaying) return;

        AudioSource next = musicSources[1 - activeSource];
        activeSource = 1 - activeSource;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(Crossfade(current, next, clip));
    }

    /// <summary>Corta la música con fundido.</summary>
    public void StopMusic() => PlayMusic(null);

    /// <summary>Efecto de sonido suelto (no corta la música).</summary>
    public void PlaySfx(AudioClip clip, float volumeScale = 1f)
    {
        if (clip != null) sfxSource.PlayOneShot(clip, sfxVolume * volumeScale);
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        if (fadeRoutine == null) musicSources[activeSource].volume = musicVolume;
    }

    // ---------------------------------------------------------------- Fundido

    private IEnumerator Crossfade(AudioSource from, AudioSource to, AudioClip clip)
    {
        if (clip != null)
        {
            to.clip = clip;
            to.volume = 0f;
            to.Play();
        }

        float fromStart = from.volume;
        float t = 0f;
        while (t < crossfadeTime)
        {
            t += Time.unscaledDeltaTime;
            float k = crossfadeTime > 0f ? t / crossfadeTime : 1f;
            from.volume = Mathf.Lerp(fromStart, 0f, k);
            if (clip != null) to.volume = Mathf.Lerp(0f, musicVolume, k);
            yield return null;
        }

        from.volume = 0f;
        from.Stop();
        from.clip = null;
        if (clip != null) to.volume = musicVolume;
        fadeRoutine = null;
    }

    // Con "Enter Play Mode" sin recargar dominio los estáticos sobreviven
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }
}