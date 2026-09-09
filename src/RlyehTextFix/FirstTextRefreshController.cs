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

internal static class FirstTextRefreshController
{
    private static readonly Dictionary<int, FirstTextRefreshState> States =
        new Dictionary<int, FirstTextRefreshState>();

    private static bool _refreshErrorLogged;
    private static bool _speakerRefreshErrorLogged;

    internal static void Initialize()
    {
    }

    internal static void OnTextAssigned(
        Text source,
        StoryTargetKind target,
        string incomingText,
        TextSetterPatchState patchState)
    {
        if (!IsEnabled() ||
            source == null ||
            target != StoryTargetKind.StoryBody)
        {
            return;
        }

        try
        {
            FirstTextRefreshState state = GetOrCreateState(source);
            string actualText = StoryTextFix.SafeGetText(source);
            string incoming = incomingText ?? string.Empty;
            string previous = patchState == null
                ? string.Empty
                : patchState.PreviousText ?? string.Empty;

            if (string.IsNullOrEmpty(actualText))
            {
                ResetForClear(state);
                return;
            }

            string originalCandidate = SelectOriginalCandidate(
                incoming,
                actualText,
                previous);

            bool returningToOriginalAfterTranslation =
                state.TranslationSeen &&
                patchState != null &&
                patchState.PreviousContainsHangul;

            if (!returningToOriginalAfterTranslation &&
                !string.IsNullOrEmpty(originalCandidate) &&
                !string.Equals(
                    state.OriginalText,
                    originalCandidate,
                    StringComparison.Ordinal))
            {
                StartNewGeneration(state, originalCandidate);
            }

            if (ContainsHangul(actualText) ||
                IsNonJapaneseReplacement(state.OriginalText, actualText))
            {
                bool firstResolution = !state.TranslationSeen;
                state.TranslationSeen = true;

                if (firstResolution &&
                    state.Attempts > 0 &&
                    Settings.FirstTextRefreshDebugLogging.Value)
                {
                    float elapsed = Math.Max(
                        0.0f,
                        Time.realtimeSinceStartup - state.LastDispatchAt);

                    Plugin.Logger.LogInfo(
                        "First-text translation detected: " +
                        $"generation={state.Generation}, " +
                        $"attempts={state.Attempts}, " +
                        $"elapsed={elapsed:0.000}s");
                }
            }
        }
        catch (Exception ex)
        {
            LogErrorOnce(
                ref _refreshErrorLogged,
                "First-text assignment tracking failed",
                ex);
        }
    }

    internal static void OnLengthOfViewRequested(
        UguiNovelText source,
        int requestedVisibleCharacters)
    {
        if (!IsEnabled() || source == null)
            return;

        try
        {
            if (StoryTargets.Classify(source) != StoryTargetKind.StoryBody)
                return;

            int trigger = Math.Max(
                0,
                Settings.FirstTextRefreshTriggerAtVisibleCharacters.Value);

            if (requestedVisibleCharacters < trigger)
                return;

            FirstTextRefreshState state = GetOrCreateState(source);
            state.LastRequestedLength = requestedVisibleCharacters;

            string currentText = StoryTextFix.SafeGetText(source);
            if (ContainsHangul(currentText) ||
                IsNonJapaneseReplacement(state.OriginalText, currentText))
            {
                state.TranslationSeen = true;
                return;
            }

            if (!IsJapaneseSourceCandidate(currentText))
                return;

            // 번역이 한 번 적용된 동일 문장을 Alt+T로 원문 표시한 경우에는
            // 자동 재감지를 다시 실행하지 않습니다. 새 대사는 정상적으로
            // 들어오는 빈 문자열 전환에서 TranslationSeen이 초기화됩니다.
            if (state.TranslationSeen)
                return;

            if (string.IsNullOrEmpty(state.OriginalText) ||
                !string.Equals(
                    state.OriginalText,
                    currentText,
                    StringComparison.Ordinal))
            {
                StartNewGeneration(state, currentText);
            }

            if (state.TranslationSeen || state.RefreshInProgress)
                return;

            int maxAttempts = Math.Max(
                1,
                Settings.FirstTextRefreshMaxAttemptsPerLine.Value);

            if (state.Attempts >= maxAttempts)
                return;

            float now = Time.realtimeSinceStartup;
            if (now < state.EligibleAt || now < state.NextAttemptAt)
                return;

            DispatchRefresh(source, state, currentText, now, maxAttempts);
        }
        catch (Exception ex)
        {
            LogErrorOnce(
                ref _refreshErrorLogged,
                "First-text refresh dispatch failed",
                ex);
        }
    }

    private static void DispatchRefresh(
        UguiNovelText source,
        FirstTextRefreshState state,
        string originalText,
        float now,
        int maxAttempts)
    {
        state.RefreshInProgress = true;
        state.Attempts++;
        state.LastDispatchAt = now;
        state.NextAttemptAt = now + Math.Max(
            0.1f,
            Settings.FirstTextRefreshRetryIntervalSeconds.Value);

        bool speakerRefreshed = false;

        try
        {
            // XUnity의 IL2CPP 텍스트 변경 훅이 컷씬 전환 직후 첫 setter를
            // 놓친 경우를 위해, 정확한 스토리 MessageText만 자기 자신으로
            // 다시 설정합니다. 전역 텍스트 검색이나 MonoBehaviour는 사용하지
            // 않습니다.
            source.text = originalText;

            if (Settings.FirstTextRefreshSpeakerName.Value)
                speakerRefreshed = RefreshSpeakerName(source);
        }
        finally
        {
            state.RefreshInProgress = false;
        }

        string after = StoryTextFix.SafeGetText(source);
        bool translatedImmediately =
            ContainsHangul(after) ||
            IsNonJapaneseReplacement(originalText, after);

        if (translatedImmediately)
            state.TranslationSeen = true;

        if (Settings.FirstTextRefreshDebugLogging.Value)
        {
            Plugin.Logger.LogInfo(
                "First-text refresh dispatched: " +
                $"generation={state.Generation}, " +
                $"attempt={state.Attempts}/{maxAttempts}, " +
                $"visible={state.LastRequestedLength}, " +
                $"immediateTranslation={translatedImmediately}, " +
                $"speakerName={speakerRefreshed}");
        }
    }

    private static bool RefreshSpeakerName(UguiNovelText storyBody)
    {
        try
        {
            Transform messageTexts = storyBody.transform == null
                ? null
                : storyBody.transform.parent;

            if (messageTexts == null)
                return false;

            Transform nameTransform = messageTexts.Find("NameBg/NameText");
            if (nameTransform == null || nameTransform.gameObject == null)
                return false;

            Component component = nameTransform.gameObject.GetComponent(
                Il2CppType.Of<Text>());

            if (component == null)
                return false;

            Text nameText;
            try
            {
                nameText = component.Cast<Text>();
            }
            catch
            {
                return false;
            }

            string value = StoryTextFix.SafeGetText(nameText);
            if (string.IsNullOrEmpty(value) ||
                ContainsHangul(value) ||
                !IsJapaneseSourceCandidate(value))
            {
                return false;
            }

            nameText.text = value;
            return true;
        }
        catch (Exception ex)
        {
            LogErrorOnce(
                ref _speakerRefreshErrorLogged,
                "Speaker-name first-text refresh failed",
                ex);
            return false;
        }
    }

    private static FirstTextRefreshState GetOrCreateState(Text source)
    {
        int id = source.GetInstanceID();

        FirstTextRefreshState state;
        if (States.TryGetValue(id, out state) &&
            state != null &&
            state.Source == source)
        {
            return state;
        }

        state = new FirstTextRefreshState
        {
            Source = source
        };

        States[id] = state;
        return state;
    }

    private static void StartNewGeneration(
        FirstTextRefreshState state,
        string originalText)
    {
        state.Generation++;
        state.OriginalText = originalText ?? string.Empty;
        state.Attempts = 0;
        state.TranslationSeen = false;
        state.RefreshInProgress = false;
        state.LastRequestedLength = 0;

        float now = Time.realtimeSinceStartup;
        state.EligibleAt = now + Math.Max(
            0.0f,
            Settings.FirstTextRefreshInitialDelaySeconds.Value);
        state.NextAttemptAt = state.EligibleAt;
        state.LastDispatchAt = 0.0f;

        if (Settings.FirstTextRefreshDebugLogging.Value)
        {
            Plugin.Logger.LogInfo(
                "First-text refresh armed: " +
                $"generation={state.Generation}, " +
                $"rawLength={state.OriginalText.Length}, " +
                $"delay={Settings.FirstTextRefreshInitialDelaySeconds.Value:0.000}s");
        }
    }

    private static void ResetForClear(FirstTextRefreshState state)
    {
        state.OriginalText = string.Empty;
        state.Attempts = 0;
        state.EligibleAt = 0.0f;
        state.NextAttemptAt = 0.0f;
        state.LastDispatchAt = 0.0f;
        state.TranslationSeen = false;
        state.RefreshInProgress = false;
        state.LastRequestedLength = 0;
    }

    private static string SelectOriginalCandidate(
        string incoming,
        string actual,
        string previous)
    {
        if (IsJapaneseSourceCandidate(incoming))
            return incoming;

        if (IsJapaneseSourceCandidate(actual))
            return actual;

        if (IsJapaneseSourceCandidate(previous))
            return previous;

        return string.Empty;
    }

    private static bool IsNonJapaneseReplacement(
        string original,
        string current)
    {
        if (string.IsNullOrEmpty(original) ||
            string.IsNullOrEmpty(current) ||
            string.Equals(original, current, StringComparison.Ordinal))
        {
            return false;
        }

        return !IsJapaneseSourceCandidate(current);
    }

    private static bool IsJapaneseSourceCandidate(string value)
    {
        if (string.IsNullOrEmpty(value) || ContainsHangul(value))
            return false;

        bool insideTag = false;

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];

            if (!insideTag && c == '<')
            {
                insideTag = true;
                continue;
            }

            if (insideTag)
            {
                if (c == '>')
                    insideTag = false;

                continue;
            }

            if ((c >= '\u3040' && c <= '\u309F') ||
                (c >= '\u30A0' && c <= '\u30FF') ||
                (c >= '\u31F0' && c <= '\u31FF') ||
                (c >= '\u3400' && c <= '\u4DBF') ||
                (c >= '\u4E00' && c <= '\u9FFF') ||
                (c >= '\uF900' && c <= '\uFAFF') ||
                (c >= '\uFF66' && c <= '\uFF9F') ||
                c == '\u3005' ||
                c == '\u3006' ||
                c == '\u3007')
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsHangul(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];

            if ((c >= '\u1100' && c <= '\u11FF') ||
                (c >= '\u3130' && c <= '\u318F') ||
                (c >= '\uA960' && c <= '\uA97F') ||
                (c >= '\uAC00' && c <= '\uD7AF') ||
                (c >= '\uD7B0' && c <= '\uD7FF'))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEnabled()
    {
        return Settings.EnableFirstTextRefresh != null &&
               Settings.EnableFirstTextRefresh.Value;
    }

    private static void LogErrorOnce(
        ref bool alreadyLogged,
        string label,
        Exception ex)
    {
        if (alreadyLogged)
            return;

        alreadyLogged = true;
        Plugin.Logger.LogError($"{label}: {ex}");
    }
}

