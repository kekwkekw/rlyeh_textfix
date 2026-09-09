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

internal static class XUnityRichTextPatchController
{
    private const string CoreAssemblyName =
        "XUnity.AutoTranslator.Plugin.Core";
    private const string ParserTypeName =
        "XUnity.AutoTranslator.Plugin.Core.Parsing.RichTextParser";
    private const string ParserResultTypeName =
        "XUnity.AutoTranslator.Plugin.Core.Parsing.ParserResult";

    private static readonly object Sync = new object();

    private static Harmony _harmony;
    private static bool _assemblyLoadSubscribed;
    private static bool _installAttempted;
    private static bool _installed;
    private static string _statusDescription = "not initialized";
    private static FieldInfo _ignoreTagsField;
    private static FieldInfo _knownTagsField;
    private static ISet<string> _ignoreTags;
    private static ISet<string> _knownTags;
    private static int _debugLogCount;

    [ThreadStatic]
    private static bool _acceptSingleFragmentForCurrentParse;

    internal static string StatusDescription
    {
        get
        {
            lock (Sync)
                return _statusDescription;
        }
    }

    internal static void Initialize(Harmony harmony)
    {
        _harmony = harmony;

        if (Settings.SanitizeDisplayTagsBeforeTranslation == null ||
            !Settings.SanitizeDisplayTagsBeforeTranslation.Value)
        {
            SetStatus("disabled by configuration");
            return;
        }

        // 먼저 AssemblyLoad를 구독한 뒤 현재 어셈블리를 다시 훑어,
        // 탐색과 구독 사이에 XUnity가 로드되는 아주 작은 경쟁 구간도 닫습니다.
        lock (Sync)
        {
            if (!_assemblyLoadSubscribed)
            {
                AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
                _assemblyLoadSubscribed = true;
            }

            _statusDescription = "waiting for XUnity.AutoTranslator.Plugin.Core";
        }

        Assembly[] loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < loadedAssemblies.Length; i++)
        {
            if (IsCoreAssembly(loadedAssemblies[i]))
            {
                TryInstall(loadedAssemblies[i]);
                return;
            }
        }
    }

    internal static XUnityRichTextParseScopeState EnterParse(string input)
    {
        var state = new XUnityRichTextParseScopeState
        {
            PreviousAcceptSingleFragment =
                _acceptSingleFragmentForCurrentParse
        };

        bool acceptSingleFragment = false;

        try
        {
            if (_installed &&
                Settings.SanitizeDisplayTagsBeforeTranslation != null &&
                Settings.SanitizeDisplayTagsBeforeTranslation.Value)
            {
                EnsureParserTagSetsReady();

                var exactTagNames = new List<string>();
                int matchedTagCount;
                acceptSingleFragment =
                    TranslationDisplayTagPolicy.TryCollectTargetTagNames(
                        input,
                        exactTagNames,
                        out matchedTagCount);

                if (acceptSingleFragment)
                {
                    AddExactIgnoredTagNames(exactTagNames);
                    LogMergedInput(input, matchedTagCount);
                }
            }
        }
        catch (Exception ex)
        {
            LogRuntimeErrorOnce(
                "RichTextParser Prefix preparation failed",
                ex);
            acceptSingleFragment = false;
        }

        _acceptSingleFragmentForCurrentParse = acceptSingleFragment;
        return state;
    }

    internal static void ExitParse(XUnityRichTextParseScopeState state)
    {
        _acceptSingleFragmentForCurrentParse =
            state != null && state.PreviousAcceptSingleFragment;
    }

    internal static int GetTemplateLengthThreshold()
    {
        // XUnity 5.6.1의 원래 조건은 template.Length > 5입니다.
        // 대상 표시 태그가 실제로 있었던 호출만 > 4로 완화하여
        // [[A]] 한 조각도 ParserResult로 유지합니다.
        return _acceptSingleFragmentForCurrentParse ? 4 : 5;
    }

    internal static bool EvaluateStartsWithPound(
        string value,
        int endIdx)
    {
        if (_acceptSingleFragmentForCurrentParse &&
            TranslationDisplayTagPolicy.IsTmpShorthandColorTagName(value))
        {
            return false;
        }

        // XUnity 5.6.1 RichTextParser.StartsWithPound의 원래 동작을
        // 그대로 보존합니다. 축약 색상 태그인 현재 호출만 false입니다.
        return 0 < value.Length && value[0] == '#';
    }

    private static void OnAssemblyLoad(
        object sender,
        AssemblyLoadEventArgs eventArgs)
    {
        Assembly assembly = eventArgs == null
            ? null
            : eventArgs.LoadedAssembly;

        if (IsCoreAssembly(assembly))
            TryInstall(assembly);
    }

    private static bool IsCoreAssembly(Assembly assembly)
    {
        if (assembly == null)
            return false;

        try
        {
            return string.Equals(
                assembly.GetName().Name,
                CoreAssemblyName,
                StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static void TryInstall(Assembly coreAssembly)
    {
        lock (Sync)
        {
            if (_installAttempted || _installed)
                return;

            _installAttempted = true;
        }

        MethodInfo parseMethod = null;
        MethodInfo startsWithPoundMethod = null;

        try
        {
            if (_harmony == null)
                throw new InvalidOperationException("Harmony is not initialized.");

            Type parserType = coreAssembly.GetType(
                ParserTypeName,
                throwOnError: false,
                ignoreCase: false);

            if (parserType == null)
                throw new MissingMemberException(ParserTypeName);

            parseMethod = parserType.GetMethod(
                "Parse",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(string), typeof(int) },
                modifiers: null);

            if (parseMethod == null ||
                !string.Equals(
                    parseMethod.ReturnType.FullName,
                    ParserResultTypeName,
                    StringComparison.Ordinal))
            {
                throw new MissingMethodException(
                    ParserTypeName,
                    "Parse(string, int)");
            }

            startsWithPoundMethod = parserType.GetMethod(
                "StartsWithPound",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(string), typeof(int) },
                modifiers: null);

            if (startsWithPoundMethod == null ||
                startsWithPoundMethod.ReturnType != typeof(bool))
            {
                throw new MissingMethodException(
                    ParserTypeName,
                    "StartsWithPound(string, int)");
            }

            _ignoreTagsField = parserType.GetField(
                "IgnoreTags",
                BindingFlags.Static | BindingFlags.NonPublic);
            _knownTagsField = parserType.GetField(
                "KnownTags",
                BindingFlags.Static | BindingFlags.NonPublic);

            if (_ignoreTagsField == null ||
                _knownTagsField == null ||
                !typeof(ISet<string>).IsAssignableFrom(
                    _ignoreTagsField.FieldType) ||
                !typeof(ISet<string>).IsAssignableFrom(
                    _knownTagsField.FieldType))
            {
                throw new MissingFieldException(
                    ParserTypeName,
                    "IgnoreTags/KnownTags");
            }

            MethodInfo parsePrefix = AccessTools.Method(
                typeof(XUnityRichTextHarmonyPatches),
                nameof(XUnityRichTextHarmonyPatches.ParsePrefix));
            MethodInfo parseTranspiler = AccessTools.Method(
                typeof(XUnityRichTextHarmonyPatches),
                nameof(XUnityRichTextHarmonyPatches.ParseTranspiler));
            MethodInfo parseFinalizer = AccessTools.Method(
                typeof(XUnityRichTextHarmonyPatches),
                nameof(XUnityRichTextHarmonyPatches.ParseFinalizer));
            if (parsePrefix == null ||
                parseTranspiler == null ||
                parseFinalizer == null)
            {
                throw new MissingMethodException(
                    "RlyehTextFix Harmony patch methods were not found.");
            }

            _harmony.Patch(
                parseMethod,
                prefix: new HarmonyMethod(parsePrefix),
                postfix: null,
                transpiler: new HarmonyMethod(parseTranspiler),
                finalizer: new HarmonyMethod(parseFinalizer),
                ilmanipulator: null);

            lock (Sync)
            {
                _installed = true;
                _statusDescription =
                    "installed for " +
                    coreAssembly.GetName().Name + " " +
                    coreAssembly.GetName().Version;

                if (_assemblyLoadSubscribed)
                {
                    AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
                    _assemblyLoadSubscribed = false;
                }
            }

            Plugin.Logger.LogInfo(
                "Installed XUnity RichTextParser display-tag merge patch: " +
                "Parse(string,int); guarded IL substitutions=" +
                "template-length + StartsWithPound call; assembly=" +
                coreAssembly.GetName().Version);
        }
        catch (Exception ex)
        {
            try
            {
                if (parseMethod != null)
                {
                    _harmony.Unpatch(
                        parseMethod,
                        HarmonyPatchType.All,
                        Plugin.PluginGuid);
                }

            }
            catch
            {
                // 원래 실패 원인을 보존합니다.
            }

            lock (Sync)
            {
                _installed = false;
                _statusDescription =
                    "failed: " + ex.GetType().Name + ": " + ex.Message;

                if (_assemblyLoadSubscribed)
                {
                    AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
                    _assemblyLoadSubscribed = false;
                }
            }

            Plugin.Logger.LogError(
                "XUnity RichTextParser patch installation failed. " +
                "No TMP setter fallback was installed.\n" + ex);
        }
    }

    private static void EnsureParserTagSetsReady()
    {
        if (_ignoreTags != null && _knownTags != null)
            return;

        lock (Sync)
        {
            if (_ignoreTags != null && _knownTags != null)
                return;

            _ignoreTags = _ignoreTagsField == null
                ? null
                : _ignoreTagsField.GetValue(null) as ISet<string>;
            _knownTags = _knownTagsField == null
                ? null
                : _knownTagsField.GetValue(null) as ISet<string>;

            if (_ignoreTags == null || _knownTags == null)
            {
                throw new InvalidOperationException(
                    "XUnity RichTextParser tag sets were not initialized.");
            }

            ConfigureBaseIgnoredTagNames();
        }
    }

    private static void ConfigureBaseIgnoredTagNames()
    {
        HashSet<string> configured =
            TranslationDisplayTagPolicy.GetConfiguredTagNames();

        foreach (string tagName in configured)
        {
            if (string.IsNullOrEmpty(tagName))
                continue;

            _ignoreTags.Add(tagName);
            _knownTags.Remove(tagName);
        }
    }

    private static void AddExactIgnoredTagNames(List<string> exactTagNames)
    {
        if (exactTagNames == null || exactTagNames.Count == 0)
            return;

        // XUnity의 기본 HashSet은 대소문자를 구분합니다. 입력에서 실제로
        // 관찰한 철자를 추가하되, 드문 병렬 Parse와의 동시 쓰기를 피합니다.
        lock (Sync)
        {
            for (int i = 0; i < exactTagNames.Count; i++)
            {
                string tagName = exactTagNames[i];
                if (string.IsNullOrEmpty(tagName))
                    continue;

                _ignoreTags.Add(tagName);
                _knownTags.Remove(tagName);
            }
        }
    }

    private static void LogMergedInput(string input, int matchedTagCount)
    {
        if (Settings.TranslationTagDebugLogging == null ||
            !Settings.TranslationTagDebugLogging.Value ||
            _debugLogCount >= 100)
        {
            return;
        }

        _debugLogCount++;
        string preview = input ?? string.Empty;
        preview = preview.Replace("\r", "\\r").Replace("\n", "\\n");
        if (preview.Length > 160)
            preview = preview.Substring(0, 160) + "...";

        Plugin.Logger.LogInfo(
            "XUnity display-tag merge prepared: " +
            "matchedTags=" + matchedTagCount +
            "; text=" + preview);
    }

    private static bool _runtimeErrorLogged;

    private static void LogRuntimeErrorOnce(string context, Exception ex)
    {
        if (_runtimeErrorLogged)
            return;

        _runtimeErrorLogged = true;
        Plugin.Logger.LogError(context + "\n" + ex);
    }

    private static void SetStatus(string status)
    {
        lock (Sync)
            _statusDescription = status;
    }
}

