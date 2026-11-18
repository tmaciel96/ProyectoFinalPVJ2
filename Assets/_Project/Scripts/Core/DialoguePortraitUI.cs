using System;
using UnityEngine;
using UnityEngine.UI;

public class DialoguePortraitUI : MonoBehaviour
{
    [Serializable]
    private struct SpeakerPortrait
    {
        public string speaker;
        public Sprite portrait;
    }

    [SerializeField] private Image[] portraitImages;
    [SerializeField] private SpeakerPortrait[] portraits;

    public void ShowSpeaker(string speaker)
    {
        Sprite portrait = null;
        foreach (var entry in portraits)
        {
            if (!string.Equals(entry.speaker, speaker, StringComparison.OrdinalIgnoreCase)) continue;
            portrait = entry.portrait;
            break;
        }
        foreach (var image in portraitImages)
        {
            image.sprite = portrait;
            image.enabled = portrait != null;
        }
    }
}
