using UnityEngine;

namespace Setus.HorrorFramework.Inventory.Items
{
    [CreateAssetMenu(
        fileName = "InventoryItem",
        menuName = "Setus/Horror Framework/Inventory/Item Definition")]
    public sealed class InventoryItemDefinition : ScriptableObject
    {
        [SerializeField] private string itemId;
        [SerializeField] private string displayName;
        [SerializeField] private bool keyItem = true;

        public string ItemId => itemId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? itemId : displayName;
        public bool KeyItem => keyItem;
    }
}
