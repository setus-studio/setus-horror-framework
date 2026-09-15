using UnityEngine;
using Setus.HorrorFramework.Localization;

namespace Setus.HorrorFramework.Narrative.Phone
{
    [CreateAssetMenu(
        fileName = "PhoneMessageDefinition",
        menuName = "Setus/Horror Framework/Narrative/Phone Message Definition")]
    public sealed class PhoneMessageDefinition : ScriptableObject
    {
        [SerializeField] private string messageId;
        [SerializeField] private string sender;
        [SerializeField] private string senderLocalizationKey;
        [SerializeField, TextArea] private string messageText;
        [SerializeField] private string messageLocalizationKey;
        [SerializeField] private bool requiresReply;
        [SerializeField, TextArea] private string replyText;
        [SerializeField] private string replyLocalizationKey;

        public string MessageId => messageId;
        public string Sender => sender;
        public string MessageText => messageText;
        public bool RequiresReply => requiresReply;
        public string ReplyText => replyText;
        public LocalizedTextReference LocalizedSender => new LocalizedTextReference(senderLocalizationKey, sender);
        public LocalizedTextReference LocalizedMessage => new LocalizedTextReference(messageLocalizationKey, messageText);
        public LocalizedTextReference LocalizedReply => new LocalizedTextReference(replyLocalizationKey, replyText);
    }
}
