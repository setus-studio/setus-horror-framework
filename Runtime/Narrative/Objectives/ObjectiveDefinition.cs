using UnityEngine;
using Setus.HorrorFramework.Localization;

namespace Setus.HorrorFramework.Narrative.Objectives
{
    [CreateAssetMenu(
        fileName = "ObjectiveDefinition",
        menuName = "Setus/Horror Framework/Narrative/Objective Definition")]
    public sealed class ObjectiveDefinition : ScriptableObject
    {
        [SerializeField] private string objectiveId;
        [SerializeField] private string title;
        [SerializeField] private string titleLocalizationKey;
        [SerializeField, TextArea] private string description;
        [SerializeField] private string descriptionLocalizationKey;
        [SerializeField] private ObjectiveGateKind completionGate;
        [SerializeField] private string completionGateId;
        [SerializeField] private string nextObjectiveId;

        public string ObjectiveId => objectiveId;
        public string Title => title;
        public string Description => description;
        public LocalizedTextReference LocalizedTitle => new LocalizedTextReference(titleLocalizationKey, title);
        public LocalizedTextReference LocalizedDescription => new LocalizedTextReference(descriptionLocalizationKey, description);
        public ObjectiveGateKind CompletionGate => completionGate;
        public string CompletionGateId => completionGateId;
        public string NextObjectiveId => nextObjectiveId;
    }
}
