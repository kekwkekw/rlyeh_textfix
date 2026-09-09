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
    private static void UpdateOverlay(
        TmpOverlayState state,
        StoryTargetKind target,
        string value,
        TMP_FontAsset font)
    {
        Text source = state.Source;
        TextMeshProUGUI overlay = state.OverlayText;

        if (!state.OverlayObject.activeSelf)
            state.OverlayObject.SetActive(true);

        overlay.enabled = true;
        overlay.font = font;
        overlay.richText = source.supportRichText;
        overlay.raycastTarget = false;
        overlay.maskable = source.maskable;
        overlay.color = source.color;
        overlay.alignment = MapAlignment(source.alignment);
        overlay.fontStyle = MapFontStyle(source.fontStyle);
        overlay.enableWordWrapping =
            source.horizontalOverflow == HorizontalWrapMode.Wrap;
        overlay.overflowMode = TextOverflowModes.Overflow;

        bool useAutoSizing = source.resizeTextForBestFit;
        overlay.enableAutoSizing = useAutoSizing;

        if (useAutoSizing)
        {
            overlay.fontSizeMin = source.resizeTextMinSize;
            overlay.fontSizeMax = source.resizeTextMaxSize;
        }

        overlay.fontSize = GetFontSize(source, target);

        float verticalOffset = target == StoryTargetKind.SpeakerName
            ? Settings.SpeakerNameVerticalOffset.Value
            : Settings.StoryVerticalOffset.Value;

        state.OverlayRect.anchoredPosition =
            new Vector2(0.0f, verticalOffset);

        // 원래 UguiNovelText는 <interval=...>, <speed=...> 같은
        // UTAGE 전용 태그를 제어 명령으로 해석하지만, 일반 TMP는 이를
        // 알 수 없어 화면에 글자로 표시합니다. 원본 UGUI 문자열은 그대로
        // 두고, 화면용 TMP 복사본에서만 해당 태그를 제거합니다.
        string rawText = value ?? string.Empty;
        int removedUtageTagCount;
        string fullText = UtageOverlayTextSanitizer.Sanitize(
            rawText,
            out removedUtageTagCount);

        if (removedUtageTagCount > 0 &&
            Settings.UtageTagDebugLogging.Value)
        {
            Plugin.Logger.LogInfo(
                "Removed UTAGE control tag(s) from TMP overlay: " +
                $"target={target}, count={removedUtageTagCount}, " +
                $"rawLength={rawText.Length}, displayLength={fullText.Length}");
        }

        int fullVisibleCharacters =
            StoryTextFix.CountVisibleCharacters(fullText);

        state.FullVisibleCharacters = fullVisibleCharacters;
        state.CurrentVisibleCharacters = -1;

        // 전체 텍스트를 먼저 설정한 다음 현재 비례 진행률에 해당하는
        // 가시 문자 수로 메시를 한 번만 재생성합니다.
        overlay.text = fullText;

        int desiredVisibleCharacters = fullVisibleCharacters;
        if (StoryProgressTracker.ShouldTrackTarget(target))
        {
            StoryProgressTracker.TryGetDesiredVisibleLength(
                source,
                fullVisibleCharacters,
                out desiredVisibleCharacters);
        }

        if (!ApplyVisibleCharacters(
                state,
                desiredVisibleCharacters,
                true))
        {
            // 매우 드문 writer 실패 시에는 글자를 잃지 않도록 전체 표시합니다.
            try
            {
                overlay.maxVisibleCharacters = fullVisibleCharacters;
                overlay.SetAllDirty();
                overlay.ForceMeshUpdate();
            }
            catch
            {
            }
        }

        if (overlay.canvasRenderer != null)
            overlay.canvasRenderer.SetAlpha(state.OriginalCanvasAlpha);
    }

    private static bool ApplyVisibleCharacters(
        TmpOverlayState state,
        int requestedVisibleCharacters,
        bool force)
    {
        if (state == null || state.OverlayText == null)
            return false;

        int full = Math.Max(0, state.FullVisibleCharacters);
        int clamped = Math.Max(
            0,
            Math.Min(requestedVisibleCharacters, full));

        if (!force && state.CurrentVisibleCharacters == clamped)
            return true;

        if (TmpVisibilityController.SetVisibleCharacters(
                state.OverlayText,
                clamped))
        {
            state.CurrentVisibleCharacters = clamped;
            return true;
        }

        return false;
    }

    private static void ShowOverlay(TmpOverlayState state)
    {
        if (state == null || state.Source == null)
            return;

        if (Settings.HideOriginalText.Value)
        {
            try
            {
                if (state.Source.canvasRenderer != null)
                    state.Source.canvasRenderer.SetAlpha(0.0f);
            }
            catch
            {
                // 오버레이가 보이는 한 원문 숨김 실패는 치명적이지 않습니다.
            }
        }
        else
        {
            RestoreSourceAlpha(state);
        }

        state.IsShowingOverlay = true;
    }

    private static void RestoreOriginalIfKnown(Text source)
    {
        if (source == null)
            return;

        TmpOverlayState state;
        if (!States.TryGetValue(source.GetInstanceID(), out state) ||
            state == null)
        {
            return;
        }

        RestoreSourceAlpha(state);

        try
        {
            if (state.OverlayObject != null)
                state.OverlayObject.SetActive(false);
        }
        catch
        {
            // ignored
        }

        state.IsShowingOverlay = false;
    }

    private static void RestoreSourceAlpha(TmpOverlayState state)
    {
        try
        {
            if (state.Source != null && state.Source.canvasRenderer != null)
            {
                state.Source.canvasRenderer.SetAlpha(
                    state.OriginalCanvasAlpha);
            }
        }
        catch
        {
            // ignored
        }
    }

}
