using UnityEngine;
using Setus.HorrorFramework.Localization;

namespace Setus.HorrorFramework.Narrative.Notes
{
    [CreateAssetMenu(
        fileName = "NarrativeNoteDefinition",
        menuName = "Setus/Horror Framework/Narrative/Note Definition")]
    public sealed class NarrativeNoteDefinition : ScriptableObject
    {
        [SerializeField] private string noteId;
        [SerializeField] private string title;
        [SerializeField] private string titleLocalizationKey;
        [SerializeField, TextArea] private string body;
        [SerializeField] private string bodyLocalizationKey;
        [SerializeField, Min(0.5f)] private float subtitleDuration = 5f;

        public string NoteId => noteId;
        public string Title => title;
        public string Body => body;
        public float SubtitleDuration => subtitleDuration;
        public LocalizedTextReference LocalizedTitle => new LocalizedTextReference(titleLocalizationKey, title);
        public LocalizedTextReference LocalizedBody => new LocalizedTextReference(bodyLocalizationKey, body);
    }
}
