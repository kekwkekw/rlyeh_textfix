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

internal static partial class TmpOverlayRenderer
{
    private static TMP_FontAsset ResolveLoadedTmpFont()
    {
        if (_resolvedFont != null)
            return _resolvedFont;

        float now = Time.realtimeSinceStartup;
        if (now < _nextFontLookupTime)
            return null;

        _nextFontLookupTime =
            now + Math.Max(0.1f, Settings.FontLookupRetrySeconds.Value);

        try
        {
            var loadedObjects = Resources.FindObjectsOfTypeAll(
                Il2CppType.Of<TMP_FontAsset>());

            if (loadedObjects == null || loadedObjects.Length == 0)
            {
                LogFontNotFoundOnce("No TMP_FontAsset objects are loaded yet.");
                return null;
            }

            string[] preferredNames = SplitNames(
                Settings.PreferredTmpFontAssetNames.Value);

            TMP_FontAsset best = null;
            int bestScore = int.MinValue;
            var loadedNames = new List<string>();

            for (int i = 0; i < loadedObjects.Length; i++)
            {
                TMP_FontAsset candidate;
                try
                {
                    candidate = loadedObjects[i].Cast<TMP_FontAsset>();
                }
                catch
                {
                    candidate = null;
                }

                if (candidate == null)
                    continue;

                string candidateName = SafeObjectName(candidate);
                loadedNames.Add(candidateName);

                int score = ScoreFontCandidate(
                    candidate,
                    candidateName,
                    preferredNames);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            if (best == null || bestScore < 0)
            {
                if (Settings.FontDebugLogging.Value)
                {
                    Plugin.Logger.LogInfo(
                        "Loaded TMP font assets: " +
                        string.Join(", ", loadedNames.ToArray()));
                }

                LogFontNotFoundOnce(
                    "No loaded TMP font matched the preferred names or Hangul check.");
                return null;
            }

            _resolvedFont = best;
            _resolvedFontName = SafeObjectName(best);
            _fontNotFoundWarningLogged = false;

            Plugin.Logger.LogInfo(
                "TMP overlay font resolved from XUnity-loaded assets: " +
                $"asset=\"{_resolvedFontName}\", score={bestScore}");

            return _resolvedFont;
        }
        catch (Exception ex)
        {
            LogFontNotFoundOnce(
                $"TMP font lookup failed: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static int ScoreFontCandidate(
        TMP_FontAsset candidate,
        string candidateName,
        string[] preferredNames)
    {
        string normalizedCandidate = NormalizeName(candidateName);
        int score = -1;

        for (int i = 0; i < preferredNames.Length; i++)
        {
            string normalizedPreferred = NormalizeName(preferredNames[i]);
            if (normalizedPreferred.Length == 0)
                continue;

            if (normalizedCandidate == normalizedPreferred)
                score = Math.Max(score, 10000 - i);
            else if (normalizedCandidate.Contains(normalizedPreferred) ||
                     normalizedPreferred.Contains(normalizedCandidate))
                score = Math.Max(score, 9000 - i);
        }

        // 현재 Noto Serif 번들의 파일명/내부명과 잘 맞는 보조 힌트입니다.
        if (normalizedCandidate.Contains("notoserif") ||
            normalizedCandidate.Contains("rlyehnotoserifkr"))
        {
            score = Math.Max(score, 8000);
        }

        if (Settings.AutoDetectHangulTmpFont.Value &&
            SupportsRepresentativeHangul(candidate))
        {
            score = Math.Max(score, 5000);
        }

        return score;
    }

    private static bool SupportsRepresentativeHangul(TMP_FontAsset font)
    {
        if (font == null || _hasCharacterMethod == null)
            return false;

        try
        {
            return InvokeHasCharacter(font, '가') &&
                   InvokeHasCharacter(font, '한');
        }
        catch
        {
            return false;
        }
    }

    private static MethodInfo FindCompatibleHasCharacterMethod()
    {
        try
        {
            MethodInfo[] methods = typeof(TMP_FontAsset).GetMethods(
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance);

            MethodInfo best = null;
            int bestScore = int.MinValue;

            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(
                        method.Name,
                        "HasCharacter",
                        StringComparison.Ordinal) ||
                    method.ReturnType != typeof(bool))
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 0)
                    continue;

                Type first = parameters[0].ParameterType;
                bool supportedFirst =
                    first == typeof(char) ||
                    first == typeof(int) ||
                    first == typeof(uint);

                if (!supportedFirst)
                    continue;

                bool remainingAreBool = true;
                for (int p = 1; p < parameters.Length; p++)
                {
                    if (parameters[p].ParameterType != typeof(bool))
                    {
                        remainingAreBool = false;
                        break;
                    }
                }

                if (!remainingAreBool)
                    continue;

                int score = 100 - parameters.Length;
                if (first == typeof(char))
                    score += 10;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = method;
                }
            }

            return best;
        }
        catch
        {
            return null;
        }
    }

}
