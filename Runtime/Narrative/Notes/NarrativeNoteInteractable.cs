using Setus.HorrorFramework.Interaction.Interactors;
using Setus.HorrorFramework.Narrative.Scenario;
using UnityEngine;
using Setus.HorrorFramework.Localization;

namespace Setus.HorrorFramework.Narrative.Notes
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class NarrativeNoteInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private NarrativeNoteDefinition note;
        [SerializeField] private Transform promptAnchor;

        public Transform PromptAnchor => promptAnchor != null ? promptAnchor : transform;

        public InteractionPrompt GetPrompt(InteractionContext context)
        {
            if (note == null || string.IsNullOrWhiteSpace(note.NoteId))
            {
                return new InteractionPrompt(
                    new LocalizedTextReference(FrameworkTextKeys.NoteUnavailablePrompt, "Document unavailable"),
                    false,
                    InteractionType.Read,
                    InteractionRejectionReason.Disabled,
                    "Note is not configured.");
            }

            var alreadyRead = context.GameContext?.Narrative?.HasReadNote(note.NoteId) == true;
            return new InteractionPrompt(
                alreadyRead
                    ? new LocalizedTextReference(FrameworkTextKeys.NoteReadAgainPrompt, "Read again")
                    : new LocalizedTextReference(FrameworkTextKeys.NoteReadPrompt, "Read note"),
                true,
                InteractionType.Read);
        }

        public InteractionResult Interact(InteractionContext context)
        {
            if (note == null || string.IsNullOrWhiteSpace(note.NoteId) || context.GameContext?.Narrative == null)
            {
                return InteractionResult.RejectedLocalized(
                    InteractionRejectionReason.Disabled,
                    new LocalizedTextReference(
                        FrameworkTextKeys.NoteNotConfiguredResult,
                        "Note is not configured."));
            }

            context.GameContext.Narrative.TryReadNote(note.NoteId);
            context.GameContext.Events.Publish(new NarrativeSubtitleRequested(
                note.LocalizedTitle,
                note.LocalizedBody,
                note.SubtitleDuration));
            return InteractionResult.SucceededLocalized(new LocalizedTextReference(
                FrameworkTextKeys.NoteReadResult,
                "Note read"));
        }
    }
}
