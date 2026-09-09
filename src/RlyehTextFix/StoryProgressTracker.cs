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

internal static class StoryProgressTracker
{
    private static readonly Dictionary<int, StoryProgressState> States =
        new Dictionary<int, StoryProgressState>();

    private static bool _trackingErrorLogged;

    internal static void Initialize()
    {
    }

    internal static TextSetterPatchState BeginTextAssignment(
        Text source,
        string incomingText)
    {
        var patchState = new TextSetterPatchState
        {
            Target = StoryTargets.Classify(source),
            InstanceId = source == null ? 0 : source.GetInstanceID(),
            IncomingText = incomingText ?? string.Empty
        };

        if (source == null ||
            !StoryTargets.IsBodyTarget(patchState.Target) ||
            !ShouldTrackTarget(patchState.Target))
        {
            return patchState;
        }

        try
        {
            patchState.PreviousText = StoryTextFix.SafeGetText(source);
            patchState.PreviousContainsHangul =
                ContainsHangul(patchState.PreviousText);

            UguiNovelText novelText = StoryTextFix.TryCastNovelText(source);
            if (novelText != null)
            {
                patchState.PreviousParsedLength =
                    StoryTextFix.GetFullVisibleLength(novelText);
                patchState.PreviousLengthOfView = novelText.LengthOfView;
            }
            else
            {
                patchState.PreviousParsedLength =
                    StoryTextFix.CountVisibleCharacters(
                        patchState.PreviousText);
            }
        }
        catch (Exception ex)
        {
            LogTrackingErrorOnce("Text assignment Prefix tracking failed", ex);
        }

        return patchState;
    }

    internal static void EndTextAssignment(
        Text source,
        string incomingText,
        TextSetterPatchState patchState)
    {
        StoryTargetKind target = StoryTargets.Classify(source);
        if (source == null ||
            !StoryTargets.IsBodyTarget(target) ||
            !ShouldTrackTarget(target))
        {
            return;
        }

        try
        {
            UguiNovelText novelText = StoryTextFix.TryCastNovelText(source);
            if (novelText == null)
                return;

            string actualText = StoryTextFix.SafeGetText(source);
            int parsedLength = StoryTextFix.GetFullVisibleLength(novelText);
            StoryProgressState state = GetOrCreateState(source, target);

            if (string.IsNullOrEmpty(actualText))
            {
                ResetForClear(state);
                return;
            }

            bool containsHangul = ContainsHangul(actualText);

            if (!containsHangul)
            {
                bool newSequence =
                    state.AwaitingOriginalAfterClear ||
                    string.IsNullOrEmpty(state.OriginalText);

                if (newSequence)
                {
                    state.Generation++;
                    state.LastRequestedOriginalLength = 0;
                    state.LastMappedTranslatedLength = -1;
                    state.CompletionLoggedGeneration = -1;
                    state.LastProgressLoggedOriginal = -1;
                }
                else if (patchState != null &&
                         patchState.PreviousContainsHangul)
                {
                    state.LastRequestedOriginalLength = Math.Max(
                        state.LastRequestedOriginalLength,
                        Math.Max(0, patchState.PreviousLengthOfView));
                }

                state.OriginalText = actualText;
                state.OriginalTotalLength = parsedLength;
                state.TranslatedText = string.Empty;
                state.TranslatedTotalLength = -1;
                state.TranslationActive = false;
                state.AwaitingOriginalAfterClear = false;
                return;
            }

            int previousOriginalLength = -1;
            if (patchState != null &&
                !patchState.PreviousContainsHangul &&
                !string.IsNullOrEmpty(patchState.PreviousText) &&
                patchState.PreviousParsedLength > 0)
            {
                previousOriginalLength = patchState.PreviousParsedLength;
            }

            if (previousOriginalLength > 0)
                state.OriginalTotalLength = previousOriginalLength;

            bool translatedTextChanged = !string.Equals(
                state.TranslatedText,
                actualText,
                StringComparison.Ordinal);

            state.TranslatedText = actualText;
            state.TranslatedTotalLength = parsedLength;
            state.TranslationActive = true;
            state.AwaitingOriginalAfterClear = false;

            if (Settings.TypewriterDebugLogging.Value && translatedTextChanged)
            {
                int mapped = ComputeMappedVisibleLength(state, parsedLength);
                Plugin.Logger.LogInfo(
                    "Proportional typewriter prepared: " +
                    $"generation={state.Generation}, " +
                    $"originalTotal={state.OriginalTotalLength}, " +
                    $"translatedTotal={state.TranslatedTotalLength}, " +
                    $"currentOriginal={state.LastRequestedOriginalLength}, " +
                    $"mappedTranslated={mapped}");
            }
        }
        catch (Exception ex)
        {
            LogTrackingErrorOnce("Text assignment Postfix tracking failed", ex);
        }
    }

    internal static void OnLengthOfViewRequested(
        UguiNovelText novelText,
        ref int requestedOriginalLength)
    {
        if (novelText == null || requestedOriginalLength < 0)
            return;

        try
        {
            StoryTargetKind target = StoryTargets.Classify(novelText);
            if (!ShouldTrackTarget(target))
                return;

            int originalRequest = requestedOriginalLength;
            StoryProgressState state = GetOrCreateState(novelText, target);
            state.LastRequestedOriginalLength = originalRequest;

            if (!state.TranslationActive)
                return;

            int previousMapped = state.LastMappedTranslatedLength;
            int mapped = ComputeMappedVisibleLength(
                state,
                state.TranslatedTotalLength);

            state.LastMappedTranslatedLength = mapped;
            bool overlayHandled = TmpOverlayRenderer.OnBodyProgress(
                novelText,
                mapped);

            // TMP 오버레이를 사용할 수 없는 경우에도 UGUI 자체 LengthOfView를
            // 번역문 길이에 비례시켜 잘림을 방지합니다.
            if (!overlayHandled)
                requestedOriginalLength = mapped;

            if (Settings.TypewriterDebugLogging.Value &&
                mapped != previousMapped &&
                ShouldLogProgress(state, originalRequest))
            {
                state.LastProgressLoggedOriginal = originalRequest;
                Plugin.Logger.LogInfo(
                    "Proportional typewriter progress: " +
                    $"target={target}, generation={state.Generation}, " +
                    $"original={originalRequest}/{state.OriginalTotalLength}, " +
                    $"translated={mapped}/{state.TranslatedTotalLength}, " +
                    $"overlay={overlayHandled}");
            }

            if (Settings.TypewriterDebugLogging.Value &&
                state.OriginalTotalLength > 0 &&
                originalRequest >= state.OriginalTotalLength &&
                state.CompletionLoggedGeneration != state.Generation)
            {
                state.CompletionLoggedGeneration = state.Generation;
                Plugin.Logger.LogInfo(
                    "Proportional typewriter completed: " +
                    $"target={target}, generation={state.Generation}, " +
                    $"original={state.OriginalTotalLength}, " +
                    $"translated={state.TranslatedTotalLength}, " +
                    $"overlay={overlayHandled}");
            }
        }
        catch (Exception ex)
        {
            LogTrackingErrorOnce("LengthOfView progress tracking failed", ex);
        }
    }

    internal static bool TryGetDesiredVisibleLength(
        Text source,
        int overlayVisibleLength,
        out int desiredVisibleLength)
    {
        desiredVisibleLength = overlayVisibleLength;

        StoryTargetKind target = StoryTargets.Classify(source);
        if (source == null || !ShouldTrackTarget(target))
            return false;

        StoryProgressState state;
        if (!States.TryGetValue(source.GetInstanceID(), out state) ||
            state == null ||
            state.Source != source ||
            !state.TranslationActive)
        {
            if (Settings.FallbackToImmediateFullWhenOriginalLengthUnknown.Value)
            {
                desiredVisibleLength = overlayVisibleLength;
                return true;
            }

            desiredVisibleLength = 0;
            return true;
        }

        if (overlayVisibleLength > state.TranslatedTotalLength)
            state.TranslatedTotalLength = overlayVisibleLength;

        desiredVisibleLength = ComputeMappedVisibleLength(
            state,
            overlayVisibleLength);
        state.LastMappedTranslatedLength = desiredVisibleLength;
        return true;
    }

    private static StoryProgressState GetOrCreateState(
        Text source,
        StoryTargetKind target)
    {
        int id = source.GetInstanceID();

        StoryProgressState state;
        if (States.TryGetValue(id, out state) &&
            state != null &&
            state.Source == source)
        {
            state.Target = target;
            return state;
        }

        state = new StoryProgressState
        {
            Source = source,
            Target = target
        };

        States[id] = state;
        return state;
    }

    internal static bool ShouldTrackTarget(StoryTargetKind target)
    {
        if (target == StoryTargetKind.StoryBody)
        {
            return Settings.DisplayMode.Value ==
                   StoryDisplayMode.ProportionalTypewriter;
        }

        if (target == StoryTargetKind.OptionPreviewBody)
            return Settings.ApplyToOptionPreview.Value;

        return false;
    }

    private static bool ShouldLogProgress(
        StoryProgressState state,
        int originalRequest)
    {
        if (state == null || state.OriginalTotalLength <= 0)
            return false;

        if (originalRequest <= 2 ||
            originalRequest >= state.OriginalTotalLength)
        {
            return true;
        }

        int step = Math.Max(1, state.OriginalTotalLength / 4);
        return state.LastProgressLoggedOriginal < 0 ||
               originalRequest - state.LastProgressLoggedOriginal >= step;
    }

    private static int ComputeMappedVisibleLength(
        StoryProgressState state,
        int fallbackTranslatedLength)
    {
        int translatedTotal = Math.Max(
            0,
            Math.Max(
                state == null ? -1 : state.TranslatedTotalLength,
                fallbackTranslatedLength));

        if (translatedTotal <= 0)
            return 0;

        if (state == null || state.OriginalTotalLength <= 0)
        {
            if (Settings.FallbackToImmediateFullWhenOriginalLengthUnknown.Value)
                return translatedTotal;

            int direct = state == null
                ? 0
                : state.LastRequestedOriginalLength;
            return Math.Max(0, Math.Min(direct, translatedTotal));
        }

        int originalVisible = Math.Max(
            0,
            state.LastRequestedOriginalLength);

        if (originalVisible >= state.OriginalTotalLength)
            return translatedTotal;

        double progress =
            (double)originalVisible / state.OriginalTotalLength;
        progress *= Math.Max(0.0f, Settings.TypewriterProgressScale.Value);
        progress = Math.Max(0.0, Math.Min(1.0, progress));

        int mapped = (int)Math.Ceiling(translatedTotal * progress);

        if (originalVisible > 0)
        {
            mapped = Math.Max(
                mapped,
                Settings.TypewriterMinimumVisibleCharacters.Value);
        }

        return Math.Max(0, Math.Min(mapped, translatedTotal));
    }

    private static void ResetForClear(StoryProgressState state)
    {
        state.OriginalTotalLength = -1;
        state.TranslatedTotalLength = -1;
        state.LastRequestedOriginalLength = 0;
        state.LastMappedTranslatedLength = -1;
        state.CompletionLoggedGeneration = -1;
        state.LastProgressLoggedOriginal = -1;
        state.TranslationActive = false;
        state.AwaitingOriginalAfterClear = true;
        state.OriginalText = string.Empty;
        state.TranslatedText = string.Empty;
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

    private static void LogTrackingErrorOnce(string label, Exception ex)
    {
        if (_trackingErrorLogged)
            return;

        _trackingErrorLogged = true;
        Plugin.Logger.LogError($"{label}: {ex}");
    }
}

