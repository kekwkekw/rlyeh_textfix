#nullable disable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utage;

namespace RlyehTextFix;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInProcess("rlyehshoujotaix_cl.exe")]
public sealed class Plugin : BasePlugin
{
    public const string PluginGuid = "dev.rlyehshoujotai.textfix";
    public const string PluginName = "RlyehTextFix";
    public const string PluginVersion = "1.1.0";

    internal static ManualLogSource Logger = null;

    private Harmony _harmony;

    public override void Load()
    {
        Logger = Log;

        Settings.Bind(Config);
        StoryTextFix.Initialize();
        StoryProgressTracker.Initialize();
        FirstTextRefreshController.Initialize();
        TmpVisibilityController.Initialize();
        TmpOverlayRenderer.Initialize();

        _harmony = new Harmony(PluginGuid);
        int installedHooks = PatchInstaller.Apply(_harmony);
        XUnityRichTextPatchController.Initialize(_harmony);

        Logger.LogInfo(
            $"{PluginName} {PluginVersion} loaded. " +
            $"DisplayMode={Settings.DisplayMode.Value}; " +
            $"FontRenderMode={Settings.FontRenderMode.Value}; " +
            $"FirstTextRefresh={Settings.EnableFirstTextRefresh.Value}; " +
            $"TagSanitizer={Settings.SanitizeUtageTagsForTmpOverlay.Value}; " +
            $"TranslationTagMerge={Settings.SanitizeDisplayTagsBeforeTranslation.Value}");
        Logger.LogInfo($"Installed hooks: {installedHooks}/2");
        Logger.LogInfo(
            "XUnity RichTextParser patch: " +
            XUnityRichTextPatchController.StatusDescription);

        if (Settings.TypewriterDebugLogging.Value)
        {
            Logger.LogInfo(
                $"TMP visibility writer candidates=" +
                $"{TmpVisibilityController.WriterDescription}");
        }

        if (installedHooks != 2)
        {
            Logger.LogError(
                "RlyehTextFix did not install every required hook. " +
                "Keep the current game/BepInEx interop files together and " +
                "check LogOutput.log before continuing.");
        }
    }
}

