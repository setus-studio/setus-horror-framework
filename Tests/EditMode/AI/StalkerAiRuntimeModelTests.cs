using NUnit.Framework;
using System.Collections.Generic;
using Setus.HorrorFramework.AI.Memory;
using Setus.HorrorFramework.AI.Perception;
using Setus.HorrorFramework.AI.States;
using Setus.HorrorFramework.AI.Tuning;
using Setus.HorrorFramework.Core.Events;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Setus.HorrorFramework.Tests.EditMode.AI
{
    public sealed class StalkerAiRuntimeModelTests
    {
        [Test]
        public void PatrolSuspicionSearchChaseAndLostTargetTransitionsHaveReasons()
        {
            var profile = CreateProfile();
            try
            {
                var events = new GameplayEventBus();
                var model = new StalkerAiRuntimeModel("m8.test.stalker", events);
                var transitionCount = 0;
                var lastReason = StalkerAiTransitionReason.Initialized;
                events.Subscribe<StalkerAiStateChanged>(changed =>
                {
                    transitionCount++;
                    lastReason = changed.Reason;
                });
                model.Configure(profile, 2);

                model.Hear(new Vector3(3f, 0f, 0f), 1f);
                model.Tick(0.2f, NoSight());
                Assert.That(model.State, Is.EqualTo(StalkerAiState.Searching));
                Assert.That(lastReason, Is.EqualTo(StalkerAiTransitionReason.SuspicionThresholdReached));

                var confirmed = ConfirmedSight(new Vector3(4f, 0f, 0f));
                model.Tick(0.1f, confirmed);
                Assert.That(model.State, Is.Not.EqualTo(StalkerAiState.Chasing));
                model.Tick(profile.ChaseAnticipationDuration, confirmed);
                Assert.That(model.State, Is.EqualTo(StalkerAiState.Chasing));
                Assert.That(lastReason, Is.EqualTo(StalkerAiTransitionReason.ChaseAnticipationElapsed));

                model.Tick(1.1f, NoSight());
                Assert.That(model.State, Is.EqualTo(StalkerAiState.Searching));
                Assert.That(lastReason, Is.EqualTo(StalkerAiTransitionReason.LostSightDelayElapsed));
                Assert.That(transitionCount, Is.GreaterThanOrEqualTo(4));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ChaseCueIsPublishedBeforeDelayedChaseTransition()
        {
            var profile = CreateProfile();
            try
            {
                var events = new GameplayEventBus();
                var model = new StalkerAiRuntimeModel("m8.test.stalker", events);
                var timeline = new List<string>();
                events.Subscribe<StalkerAiHighStakesFeedbackRequested>(_ => timeline.Add("cue"));
                events.Subscribe<StalkerAiStateChanged>(changed =>
                {
                    if (changed.State == StalkerAiState.Chasing)
                    {
                        timeline.Add("chase");
                    }
                });
                model.Configure(profile, 1);
                var confirmed = ConfirmedSight(new Vector3(4f, 0f, 0f));

                model.Tick(0.1f, confirmed);

                Assert.That(timeline, Is.EqualTo(new[] { "cue" }));
                Assert.That(model.State, Is.Not.EqualTo(StalkerAiState.Chasing));
                Assert.That(model.IsChaseAnticipating, Is.True);

                model.Tick(profile.ChaseAnticipationDuration, confirmed);

                Assert.That(model.State, Is.EqualTo(StalkerAiState.Chasing));
                Assert.That(model.IsChaseAnticipating, Is.False);
                Assert.That(timeline, Is.EqualTo(new[] { "cue", "chase" }));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void RepeatedConfirmedSightDuringAnticipationDoesNotRepeatCue()
        {
            var profile = CreateProfile();
            try
            {
                var events = new GameplayEventBus();
                var model = new StalkerAiRuntimeModel("m8.test.stalker", events);
                var cueCount = 0;
                events.Subscribe<StalkerAiHighStakesFeedbackRequested>(_ => cueCount++);
                model.Configure(profile, 1);
                var confirmed = ConfirmedSight(new Vector3(4f, 0f, 0f));

                model.Tick(0.1f, confirmed);
                model.Tick(0.1f, confirmed);
                model.Tick(0.1f, confirmed);

                Assert.That(cueCount, Is.EqualTo(1));
                Assert.That(model.State, Is.Not.EqualTo(StalkerAiState.Chasing));
                Assert.That(model.IsChaseAnticipating, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void LosingConfirmedSightCancelsAnticipationWithoutEnteringChase()
        {
            var profile = CreateProfile();
            try
            {
                var events = new GameplayEventBus();
                var model = new StalkerAiRuntimeModel("m8.test.stalker", events);
                var cueCount = 0;
                events.Subscribe<StalkerAiHighStakesFeedbackRequested>(_ => cueCount++);
                model.Configure(profile, 1);

                model.Tick(0.1f, ConfirmedSight(new Vector3(4f, 0f, 0f)));
                model.Tick(profile.ChaseAnticipationDuration, NoSight());

                Assert.That(cueCount, Is.EqualTo(1));
                Assert.That(model.State, Is.Not.EqualTo(StalkerAiState.Chasing));
                Assert.That(model.IsChaseAnticipating, Is.False);
                Assert.That(model.ChaseAnticipationRemaining, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void BlockedSightCannotConfirmChase()
        {
            var profile = CreateProfile();
            try
            {
                var model = new StalkerAiRuntimeModel("m8.test.stalker", new GameplayEventBus());
                model.Configure(profile, 1);

                var blocked = new StalkerSightResult(StalkerSightReason.TargetBlocked, Vector3.forward, null);
                model.Tick(0.1f, new StalkerPerceptionInput(blocked, false));

                Assert.That(model.State, Is.Not.EqualTo(StalkerAiState.Chasing));
                Assert.That(model.Suspicion, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ConfirmedSightUpdatesLastKnownPosition()
        {
            var profile = CreateProfile();
            try
            {
                var model = new StalkerAiRuntimeModel("m8.test.stalker", new GameplayEventBus());
                model.Configure(profile, 1);
                var confirmedPosition = new Vector3(4f, 0f, 2f);

                model.Tick(0.1f, ConfirmedSight(confirmedPosition));

                Assert.That(model.LastKnownPosition, Is.EqualTo(confirmedPosition));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void BlockedSightPreservesLastConfirmedPositionAndDecaysSuspicion()
        {
            var profile = CreateProfile();
            try
            {
                var model = new StalkerAiRuntimeModel("m8.test.stalker", new GameplayEventBus());
                model.Configure(profile, 1);
                var lastVisiblePosition = new Vector3(3f, 0f, 1f);
                model.Tick(0.1f, ConfirmedSight(lastVisiblePosition));
                var suspicionBeforeBlockedSight = model.Suspicion;

                var hiddenPosition = new Vector3(9f, 0f, -4f);
                var blocked = new StalkerSightResult(StalkerSightReason.TargetBlocked, hiddenPosition, null);
                model.Tick(0.25f, new StalkerPerceptionInput(blocked, false));

                Assert.That(model.LastKnownPosition, Is.EqualTo(lastVisiblePosition));
                Assert.That(model.Suspicion, Is.LessThan(suspicionBeforeBlockedSight));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void MovingTargetBehindWallAcrossTicksDoesNotReplaceLastKnownPosition()
        {
            var profile = CreateProfile();
            try
            {
                var model = new StalkerAiRuntimeModel("m8.test.stalker", new GameplayEventBus());
                model.Configure(profile, 1);
                var lastVisiblePosition = new Vector3(2f, 0f, 5f);
                model.Tick(0.1f, ConfirmedSight(lastVisiblePosition));

                for (var index = 0; index < 8; index++)
                {
                    var hiddenPosition = new Vector3(10f + index, 0f, -index);
                    var blocked = new StalkerSightResult(StalkerSightReason.TargetBlocked, hiddenPosition, null);
                    model.Tick(0.25f, new StalkerPerceptionInput(blocked, false));
                }

                Assert.That(model.LastKnownPosition, Is.EqualTo(lastVisiblePosition));
                Assert.That(model.State, Is.EqualTo(StalkerAiState.Searching));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void OutsideFieldOfViewDoesNotReplaceLastKnownPosition()
        {
            var profile = CreateProfile();
            try
            {
                var model = new StalkerAiRuntimeModel("m8.test.stalker", new GameplayEventBus());
                model.Configure(profile, 1);
                var lastVisiblePosition = new Vector3(1f, 0f, 4f);
                model.Tick(0.1f, ConfirmedSight(lastVisiblePosition));

                var outsideFieldOfView = new StalkerSightResult(
                    StalkerSightReason.OutsideFieldOfView,
                    new Vector3(-6f, 0f, -3f),
                    null);
                model.Tick(0.25f, new StalkerPerceptionInput(outsideFieldOfView, false));

                Assert.That(model.LastKnownPosition, Is.EqualTo(lastVisiblePosition));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void HearingAndProximityStillUpdateLastKnownPosition()
        {
            var profile = CreateProfile();
            try
            {
                var hearingModel = new StalkerAiRuntimeModel("m8.test.hearing", new GameplayEventBus());
                hearingModel.Configure(profile, 1);
                var heardPosition = new Vector3(7f, 0f, 3f);
                hearingModel.Hear(heardPosition, 0.5f);
                Assert.That(hearingModel.LastKnownPosition, Is.EqualTo(heardPosition));

                var proximityModel = new StalkerAiRuntimeModel("m8.test.proximity", new GameplayEventBus());
                proximityModel.Configure(profile, 1);
                var proximityPosition = new Vector3(-2f, 0f, 1f);
                var noSight = new StalkerSightResult(StalkerSightReason.TargetBlocked, Vector3.zero, null);
                proximityModel.Tick(
                    0.1f,
                    new StalkerPerceptionInput(noSight, true, proximityPosition));

                Assert.That(proximityModel.LastKnownPosition, Is.EqualTo(proximityPosition));
                Assert.That(proximityModel.LastKnownPosition, Is.Not.EqualTo(noSight.TargetPosition));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ChasingStateRestoresAsSearchAtLastKnownPosition()
        {
            var profile = CreateProfile();
            try
            {
                var source = new StalkerAiRuntimeModel("m8.test.stalker", new GameplayEventBus());
                source.Configure(profile, 2);
                var confirmed = ConfirmedSight(new Vector3(8f, 0f, 2f));
                source.Tick(0.1f, confirmed);
                source.Tick(profile.ChaseAnticipationDuration, confirmed);

                var restored = new StalkerAiRuntimeModel("m8.test.stalker", new GameplayEventBus());
                restored.Configure(profile, 2);
                restored.RestoreState(source.CaptureState());

                Assert.That(restored.State, Is.EqualTo(StalkerAiState.Searching));
                Assert.That(restored.LastKnownPosition, Is.EqualTo(new Vector3(8f, 0f, 2f)));
                Assert.That(restored.HasActiveState, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void InvalidPersistedStateFailsSemanticValidation()
        {
            var profile = CreateProfile();
            try
            {
                var model = new StalkerAiRuntimeModel("m8.test.stalker", new GameplayEventBus());
                model.Configure(profile, 1);
                var invalid = new StalkerAiRuntimeState(
                    StalkerAiState.Chasing,
                    1.2f,
                    Vector3.zero,
                    0,
                    0f,
                    0f);

                Assert.That(model.ValidateRestoreState(invalid).IsValid, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        private static StalkerPerceptionInput ConfirmedSight(Vector3 targetPosition)
        {
            return new StalkerPerceptionInput(
                new StalkerSightResult(StalkerSightReason.TargetConfirmed, targetPosition, null),
                false);
        }

        private static StalkerPerceptionInput NoSight()
        {
            return new StalkerPerceptionInput(
                new StalkerSightResult(StalkerSightReason.NoTarget, Vector3.zero, null),
                false);
        }

        private static StalkerAiTuningProfile CreateProfile()
        {
            var profile = ScriptableObject.CreateInstance<StalkerAiTuningProfile>();
            var serialized = new SerializedObject(profile);
            serialized.FindProperty("suspicionGainPerSecond").floatValue = 2f;
            serialized.FindProperty("suspicionDecayPerSecond").floatValue = 0.5f;
            serialized.FindProperty("searchThreshold").floatValue = 0.2f;
            serialized.FindProperty("chaseAnticipationDuration").floatValue = 0.5f;
            serialized.FindProperty("lostSightDelay").floatValue = 1f;
            serialized.FindProperty("searchDuration").floatValue = 3f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return profile;
        }
    }
}
