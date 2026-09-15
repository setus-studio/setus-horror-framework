using Setus.HorrorFramework.Player.Controller;
using Setus.HorrorFramework.SceneFlow.Transitions;
using UnityEngine;

namespace Setus.HorrorFramework.SceneFlow.Spawn
{
    public enum PlayerSpawnResolutionKind
    {
        NotRequested,
        AppliedRequestedSpawn,
        AppliedDefaultFallback,
        NoPlayerTarget,
        MultiplePlayerTargets,
        SpawnNotFound,
        DuplicateSpawnId,
    }

    public readonly struct PlayerSpawnResolution
    {
        public PlayerSpawnResolution(PlayerSpawnResolutionKind kind, string requestedSpawnId, string resolvedSpawnId)
        {
            Kind = kind;
            RequestedSpawnId = requestedSpawnId ?? string.Empty;
            ResolvedSpawnId = resolvedSpawnId ?? string.Empty;
        }

        public PlayerSpawnResolutionKind Kind { get; }
        public string RequestedSpawnId { get; }
        public string ResolvedSpawnId { get; }
        public bool Applied =>
            Kind == PlayerSpawnResolutionKind.AppliedRequestedSpawn ||
            Kind == PlayerSpawnResolutionKind.AppliedDefaultFallback;
    }

    public static class PlayerSpawnResolver
    {
        public const string DefaultSpawnId = "default";

        public static PlayerSpawnResolution ResolveAndApply(SceneTransitionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SpawnId))
            {
                return new PlayerSpawnResolution(PlayerSpawnResolutionKind.NotRequested, string.Empty, string.Empty);
            }

            var target = FindSinglePlayerTarget(out var targetCount);
            if (targetCount == 0)
            {
                return new PlayerSpawnResolution(PlayerSpawnResolutionKind.NoPlayerTarget, request.SpawnId, string.Empty);
            }

            if (targetCount > 1)
            {
                return new PlayerSpawnResolution(PlayerSpawnResolutionKind.MultiplePlayerTargets, request.SpawnId, string.Empty);
            }

            var points = Object.FindObjectsByType<PlayerSpawnPoint>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            var requested = FindSingleSpawnPoint(points, request.SpawnId, out var requestedCount);
            if (requestedCount == 1)
            {
                requested.ApplyTo(target);
                return new PlayerSpawnResolution(
                    PlayerSpawnResolutionKind.AppliedRequestedSpawn,
                    request.SpawnId,
                    requested.SpawnId);
            }

            if (requestedCount > 1)
            {
                return new PlayerSpawnResolution(PlayerSpawnResolutionKind.DuplicateSpawnId, request.SpawnId, string.Empty);
            }

            var fallback = FindSingleSpawnPoint(points, DefaultSpawnId, out var fallbackCount);
            if (fallbackCount == 1)
            {
                fallback.ApplyTo(target);
                return new PlayerSpawnResolution(
                    PlayerSpawnResolutionKind.AppliedDefaultFallback,
                    request.SpawnId,
                    fallback.SpawnId);
            }

            return new PlayerSpawnResolution(
                fallbackCount > 1
                    ? PlayerSpawnResolutionKind.DuplicateSpawnId
                    : PlayerSpawnResolutionKind.SpawnNotFound,
                request.SpawnId,
                string.Empty);
        }

        private static IPlayerPoseProvider FindSinglePlayerTarget(out int count)
        {
            var behaviours = Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            IPlayerPoseProvider result = null;
            count = 0;

            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPlayerPoseProvider provider)
                {
                    result = provider;
                    count++;
                }
            }

            return result;
        }

        private static PlayerSpawnPoint FindSingleSpawnPoint(
            PlayerSpawnPoint[] points,
            string spawnId,
            out int count)
        {
            PlayerSpawnPoint result = null;
            count = 0;

            for (var i = 0; i < points.Length; i++)
            {
                if (points[i] != null && string.Equals(points[i].SpawnId, spawnId, System.StringComparison.Ordinal))
                {
                    result = points[i];
                    count++;
                }
            }

            return result;
        }
    }
}
