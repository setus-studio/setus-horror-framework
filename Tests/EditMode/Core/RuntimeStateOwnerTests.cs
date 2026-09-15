using NUnit.Framework;
using Setus.HorrorFramework.Core.Services;

namespace Setus.HorrorFramework.Tests.EditMode.Core
{
    public sealed class RuntimeStateOwnerTests
    {
        [Test]
        public void RuntimeStateOwnerCapturesAndRestoresTypedState()
        {
            var owner = new TestStateOwner();
            owner.RestoreState(new TestState(5));

            var captured = owner.CaptureState();

            Assert.AreEqual(5, captured.Value);
            Assert.AreEqual(typeof(TestState), ((IRuntimeStateOwner)owner).StateType);
            Assert.AreEqual("test.state", owner.StateKey);
        }

        private readonly struct TestState
        {
            public TestState(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }

        private sealed class TestStateOwner : RuntimeStateOwnerBase<TestState>
        {
            private TestState state;

            public TestStateOwner()
                : base("test.state")
            {
            }

            public override TestState CaptureState()
            {
                return state;
            }

            public override void RestoreState(TestState state)
            {
                this.state = state;
            }
        }
    }
}
