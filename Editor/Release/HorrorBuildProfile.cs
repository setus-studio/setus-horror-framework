using UnityEditor;
using UnityEngine;

namespace Setus.HorrorFramework.Editor.Release
{
    public sealed class HorrorBuildProfile : ScriptableObject
    {
        [SerializeField] private string profileId = "production";
        [SerializeField] private BuildTarget target = BuildTarget.StandaloneOSX;
        [SerializeField] private string outputPath = "Builds/Production/SetusHorrorGame.app";
        [SerializeField] private bool developmentBuild;
        [SerializeField] private bool allowDebugging;
        [SerializeField] private bool connectProfiler;
        [SerializeField] private bool strictValidation = true;

        public string ProfileId => profileId;
        public BuildTarget Target => target;
        public string OutputPath => outputPath;
        public bool DevelopmentBuild => developmentBuild;
        public bool AllowDebugging => allowDebugging;
        public bool ConnectProfiler => connectProfiler;
        public bool StrictValidation => strictValidation;
        public bool IsProduction => !developmentBuild && !allowDebugging && !connectProfiler;

        public void Configure(
            string id,
            BuildTarget buildTarget,
            string path,
            bool development,
            bool debugging,
            bool profiler,
            bool strict)
        {
            profileId = id?.Trim() ?? string.Empty;
            target = buildTarget;
            outputPath = path?.Trim() ?? string.Empty;
            developmentBuild = development;
            allowDebugging = debugging;
            connectProfiler = profiler;
            strictValidation = strict;
        }
    }
}
