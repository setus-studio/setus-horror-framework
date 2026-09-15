using System;
using System.IO;
using System.Linq;
using Setus.HorrorFramework.Editor.Validators;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Setus.HorrorFramework.Editor.Release
{
    public static class HorrorBuildCommand
    {
        [MenuItem("Setus/Horror Framework/Release/Validate Production Readiness")]
        public static void ValidateProductionFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var result = ValidateCanonicalProject(ProductionHardeningBuilder.ProductionProfilePath);
            FrameworkSceneValidator.LogResult("M13 Production Readiness", result);
            if (!result.IsValid)
            {
                throw new InvalidOperationException(FormatFailure(result));
            }
        }

        [MenuItem("Setus/Horror Framework/Release/Build Production Player")]
        public static void BuildProductionFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Build(ProductionHardeningBuilder.ProductionProfilePath, null);
        }

        public static void BuildFromCommandLine()
        {
            var arguments = Environment.GetCommandLineArgs();
            var profilePath = ReadArgument(arguments, "-horrorBuildProfile") ??
                              ProductionHardeningBuilder.ProductionProfilePath;
            var output = ReadArgument(arguments, "-horrorBuildOutput");
            Build(profilePath, output);
        }

        public static void Build(string profilePath, string outputOverride)
        {
            var validation = ValidateCanonicalProject(profilePath);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(FormatFailure(validation));
            }

            var profile = AssetDatabase.LoadAssetAtPath<HorrorBuildProfile>(profilePath);
            if (profile == null)
            {
                throw new InvalidOperationException($"Build profile '{profilePath}' could not be loaded after validation.");
            }

            var output = string.IsNullOrWhiteSpace(outputOverride) ? profile.OutputPath : outputOverride.Trim();
            if (Path.IsPathRooted(output))
            {
                throw new ArgumentException("Build output must be project-relative.", nameof(outputOverride));
            }

            var outputDirectory = Path.GetDirectoryName(output);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var options = BuildOptions.None;
            if (profile.DevelopmentBuild)
            {
                options |= BuildOptions.Development;
            }
            if (profile.AllowDebugging)
            {
                options |= BuildOptions.AllowDebugging;
            }
            if (profile.ConnectProfiler)
            {
                options |= BuildOptions.ConnectWithProfiler;
            }

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                locationPathName = output,
                target = profile.Target,
                options = options
            });
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Standalone build failed: {report.summary.result}, {report.summary.totalErrors} error(s).");
            }

            Debug.Log(
                $"M13 standalone build succeeded: '{output}', " +
                $"{report.summary.totalSize} bytes in {report.summary.totalTime}.");
        }

        public static FrameworkSceneValidationResult ValidateCanonicalProject(string profilePath)
        {
            var profile = AssetDatabase.LoadAssetAtPath<HorrorBuildProfile>(profilePath);
            var budget = AssetDatabase.LoadAssetAtPath<HorrorPerformanceBudget>(
                ProductionHardeningBuilder.PerformanceBudgetPath);
            return ProductionReadinessValidator.Validate(profile, budget);
        }

        public static string ReadArgument(string[] arguments, string name)
        {
            if (arguments == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            for (var i = 0; i < arguments.Length - 1; i++)
            {
                if (string.Equals(arguments[i], name, StringComparison.Ordinal))
                {
                    return arguments[i + 1];
                }
            }
            return null;
        }

        private static string FormatFailure(FrameworkSceneValidationResult result)
        {
            return "M13 production readiness validation failed:\n" +
                   string.Join("\n", result.Issues.Where(issue => issue.IsError).Select(issue => "- " + issue));
        }
    }
}
