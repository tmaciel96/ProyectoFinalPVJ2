using UnityEngine;
using UnityEngine.UI;

public class HealthHeartsUI : MonoBehaviour
{
    [SerializeField] private Image[] hearts;

    private void OnEnable()
    {
        PlayerHealth.OnHealthChanged += Refresh;
        var player = FindFirstObjectByType<PlayerHealth>();
        if (player != null) Refresh(player.CurrentHealth, player.MaxHealth);
    }

    private void OnDisable()
    {
        PlayerHealth.OnHealthChanged -= Refresh;
    }

    private void Refresh(int current, int maximum)
    {
        float remaining = maximum > 0 ? Mathf.Clamp01((float)current / maximum) * hearts.Length : 0f;
        for (int i = 0; i < hearts.Length; i++)
            hearts[i].fillAmount = Mathf.Clamp01(remaining - i);
    }
}
