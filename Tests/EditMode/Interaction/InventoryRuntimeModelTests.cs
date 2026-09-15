using System.Linq;
using NUnit.Framework;
using Setus.HorrorFramework.Inventory.Runtime;

namespace Setus.HorrorFramework.Tests.EditMode.Interaction
{
    public sealed class InventoryRuntimeModelTests
    {
        [Test]
        public void AddConsumeAndRestorePreservesHeldItem()
        {
            var inventory = new InventoryRuntimeModel();

            Assert.IsTrue(inventory.AddItem("key.front", true));
            Assert.IsTrue(inventory.HasItem("key.front"));
            Assert.AreEqual("key.front", inventory.Current.HeldItemId);

            var saved = inventory.CaptureState();
            Assert.IsTrue(inventory.ConsumeItem("key.front"));
            Assert.IsFalse(inventory.HasItem("key.front"));

            inventory.RestoreState(saved);

            Assert.IsTrue(inventory.HasItem("key.front"));
            Assert.AreEqual("key.front", inventory.Current.HeldItemId);
            Assert.AreEqual(1, inventory.Current.ItemIds.Count);
        }

        [Test]
        public void RestoreDeduplicatesItemIds()
        {
            var inventory = new InventoryRuntimeModel();

            inventory.RestoreState(new InventoryState(new[] { "key.front", "key.front", "note" }, "key.front"));

            Assert.AreEqual(2, inventory.Current.ItemIds.Count);
            Assert.IsTrue(inventory.Current.ItemIds.Contains("key.front"));
            Assert.IsTrue(inventory.Current.ItemIds.Contains("note"));
        }
    }
}
