using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class LinuxServerBuild
{
    const string DefaultOutputDirectory = "Builds/LinuxServer";

    [MenuItem("Tools/Build/Build Linux Server")]
    public static void BuildFromMenu()
    {
        Build(DefaultOutputDirectory);
    }

    public static void BuildFromCommandLine()
    {
        string outputDirectory = GetArgument("-buildOutput");
        Build(string.IsNullOrWhiteSpace(outputDirectory) ? DefaultOutputDirectory : outputDirectory);
    }

    static void Build(string outputDirectory)
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
            throw new InvalidOperationException("Nenhuma cena habilitada em ProjectSettings/EditorBuildSettings.asset.");

        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Nao foi possivel localizar a raiz do projeto.");

        string resolvedOutputDirectory = Path.IsPathRooted(outputDirectory)
            ? outputDirectory
            : Path.Combine(projectRoot, outputDirectory);

        Directory.CreateDirectory(resolvedOutputDirectory);

        string executableName = SanitizeFileName(string.IsNullOrWhiteSpace(PlayerSettings.productName)
            ? "LinuxServer"
            : PlayerSettings.productName);

        string locationPathName = Path.Combine(resolvedOutputDirectory, $"{executableName}.x86_64");

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = locationPathName,
            target = BuildTarget.StandaloneLinux64,
            subtarget = (int)StandaloneBuildSubtarget.Server,
            options = BuildOptions.StrictMode
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result != BuildResult.Succeeded)
            throw new Exception($"Build Linux Server falhou: {summary.result}");

        Debug.Log($"Linux Server gerado em: {locationPathName}");
    }

    static string GetArgument(string argumentName)
    {
        string[] args = Environment.GetCommandLineArgs();

        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], argumentName, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }

    static string SanitizeFileName(string fileName)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        return new string(fileName.Select(character => invalidChars.Contains(character) ? '_' : character).ToArray());
    }
}
