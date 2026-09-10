using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

public static class WindowsItchBuild
{
    const string DefaultOutputRoot = "Builds/Windows";
    const string ProductDisplayName = "Elarion: Relics of the Forgotten";
    const string PackageBaseName = "Elarion-Relics-of-the-Forgotten";
    const ScriptingImplementation ReleaseScriptingBackend = ScriptingImplementation.Mono2x;

    [MenuItem("Tools/Build/Build Windows Release (itch.io)")]
    public static void BuildFromMenu()
    {
        Build(DefaultOutputRoot, true);
    }

    public static void BuildFromCommandLine()
    {
        string outputRoot = GetArgument("-buildOutput");
        Build(string.IsNullOrWhiteSpace(outputRoot) ? DefaultOutputRoot : outputRoot, false);
    }

    static void Build(string outputRoot, bool revealOutput)
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled && File.Exists(scene.path))
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
            throw new InvalidOperationException("Nenhuma cena valida esta habilitada para a build.");

        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Nao foi possivel localizar a raiz do projeto.");
        string resolvedOutputRoot = Path.IsPathRooted(outputRoot)
            ? outputRoot
            : Path.Combine(projectRoot, outputRoot);

        string productName = string.IsNullOrWhiteSpace(PlayerSettings.productName)
            ? ProductDisplayName
            : PlayerSettings.productName;
        string executableName = PackageBaseName;
        string packageName = $"{executableName}-Windows-x64";
        string packageDirectory = Path.Combine(resolvedOutputRoot, packageName);
        string executablePath = Path.Combine(packageDirectory, $"{executableName}.exe");
        string version = string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion)
            ? "0.1.0"
            : PlayerSettings.bundleVersion.Trim();
        string zipPath = Path.Combine(resolvedOutputRoot, $"{packageName}-{SanitizeFileName(version)}.zip");

        Directory.CreateDirectory(resolvedOutputRoot);
        DeleteGeneratedPath(packageDirectory, resolvedOutputRoot);
        if (File.Exists(zipPath))
            File.Delete(zipPath);
        Directory.CreateDirectory(packageDirectory);

        EnsureReleaseScriptingBackend();
        EnsureWindowsGraphicsApi();

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = executablePath,
            target = BuildTarget.StandaloneWindows64,
            subtarget = (int)StandaloneBuildSubtarget.Player,
            options = BuildOptions.StrictMode | BuildOptions.CompressWithLz4HC
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        if (summary.result != BuildResult.Succeeded)
            throw new Exception($"Build Windows falhou: {summary.result}");

        WritePlayerReadme(packageDirectory, productName, executableName, version);
        File.WriteAllText(
            Path.Combine(packageDirectory, "VERSION.txt"),
            $"{productName}\nVersao {version}\nWindows x64\nGerado em {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n");

        ZipFile.CreateFromDirectory(
            packageDirectory,
            zipPath,
            System.IO.Compression.CompressionLevel.Optimal,
            false);

        Debug.Log(
            $"Build itch.io concluida.\nExecutavel: {executablePath}\nZIP: {zipPath}\n" +
            $"Tamanho: {summary.totalSize / (1024f * 1024f):0.0} MB");

        if (revealOutput)
            EditorUtility.RevealInFinder(zipPath);
    }

    static void DeleteGeneratedPath(string targetPath, string outputRoot)
    {
        string resolvedTarget = Path.GetFullPath(targetPath);
        string resolvedRoot = Path.GetFullPath(outputRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!resolvedTarget.StartsWith(resolvedRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A pasta de build calculada esta fora do diretorio permitido.");

        if (Directory.Exists(resolvedTarget))
            Directory.Delete(resolvedTarget, true);
    }

    static void EnsureReleaseScriptingBackend()
    {
        ScriptingImplementation currentBackend =
            PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);

        if (currentBackend == ReleaseScriptingBackend)
            return;

        PlayerSettings.SetScriptingBackend(
            NamedBuildTarget.Standalone,
            ReleaseScriptingBackend);
        AssetDatabase.SaveAssets();

        Debug.Log(
            "Backend da build Windows alterado para Mono. " +
            "IL2CPP exige Visual Studio com C++ e Windows SDK instalados.");
    }

    static void EnsureWindowsGraphicsApi()
    {
        GraphicsDeviceType[] graphicsApis =
        {
            GraphicsDeviceType.Direct3D11
        };

        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, graphicsApis);
        AssetDatabase.SaveAssets();
    }

    static void WritePlayerReadme(
        string packageDirectory,
        string productName,
        string executableName,
        string version)
    {
        string readme =
            $"{productName} - Demo {version}\n\n" +
            "ESCOPO DA DEMO\n" +
            "Explore uma ilha procedural, colete recursos, complete a jornada inicial, escolha um legado na " +
            "Caverna dos Portadores, teste combate, mapa, bestiario, progresso e multiplayer LAN experimental.\n" +
            "Pressione F1 durante o jogo para abrir o guia rapido e F2 para mostrar/ocultar FPS.\n\n" +
            "COMO INICIAR\n" +
            $"1. Execute {executableName}.exe.\n" +
            "2. Escolha uma nova aventura ou carregue seu progresso.\n\n" +
            "CONTROLES PRINCIPAIS\n" +
            "WASD: mover\nMouse: camera e combate\nShift: correr\nEspaco: pular\n" +
            "E: interagir\nTab: inventario\nM: mapa\nB: bestiario\nJ: missoes\n" +
            "1-8 / roda do mouse: hotbar\nQ, R, F e V: habilidades do portador\nEsc: pausa\n\n" +
            "MULTIPLAYER LAN\n" +
            "O host cria a partida pelo menu Multiplayer. Jogadores na mesma rede podem usar a descoberta local " +
            "ou conectar pelo IP do host. A porta padrao e 7777.\n\n" +
            "SAVE\n" +
            "O progresso e salvo na pasta LocalLow do Windows, dentro da pasta Marped deste jogo.\n\n" +
            "Esta e uma versao de demonstracao em desenvolvimento. Feedbacks e relatorios de bugs sao bem-vindos.\n";

        File.WriteAllText(Path.Combine(packageDirectory, "LEIA-ME.txt"), readme);
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
