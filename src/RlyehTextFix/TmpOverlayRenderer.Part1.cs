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
    private const string OverlayObjectName = "RlyehTextFix_TMPOverlay_XUAIGNORETREE";

    private static readonly Dictionary<int, TmpOverlayState> States =
        new Dictionary<int, TmpOverlayState>();

    private static readonly HashSet<int> CreationLogged =
        new HashSet<int>();

    private static TMP_FontAsset _resolvedFont;
    private static string _resolvedFontName = string.Empty;
    private static float _nextFontLookupTime;
    private static bool _fontNotFoundWarningLogged;
    private static bool _overlayCreationErrorLogged;
    private static bool _overlayUpdateErrorLogged;

    private static MethodInfo _hasCharacterMethod;

    internal static void Initialize()
    {
        _hasCharacterMethod = FindCompatibleHasCharacterMethod();

        if (_hasCharacterMethod == null &&
            Settings.AutoDetectHangulTmpFont.Value &&
            Settings.FontDebugLogging.Value)
        {
            Plugin.Logger.LogInfo(
                "TMP_FontAsset.HasCharacter probe was not found. " +
                "Automatic Hangul validation will be name-based only.");
        }
    }

    internal static void OnTextAssigned(
        Text source,
        StoryTargetKind target)
    {
        if (source == null || target == StoryTargetKind.None)
            return;

        if (!IsTargetEnabled(target) ||
            Settings.FontRenderMode.Value != StoryFontRenderMode.TmpOverlay)
        {
            RestoreOriginalIfKnown(source);
            return;
        }

        try
        {
            string value = SafeGetText(source);
            bool shouldUseOverlay =
                !Settings.ApplyOnlyWhenHangulPresent.Value ||
                ContainsHangul(value);

            if (!shouldUseOverlay)
            {
                if (Settings.RestoreOriginalForNonHangul.Value)
                    RestoreOriginalIfKnown(source);

                return;
            }

            TMP_FontAsset font = ResolveLoadedTmpFont();
            if (font == null)
            {
                RestoreOriginalIfKnown(source);
                return;
            }

            TmpOverlayState state = GetOrCreateState(source);
            if (state == null || state.OverlayText == null)
            {
                RestoreOriginalIfKnown(source);
                return;
            }

            UpdateOverlay(state, target, value, font);
            ShowOverlay(state);
        }
        catch (Exception ex)
        {
            if (!_overlayUpdateErrorLogged)
            {
                _overlayUpdateErrorLogged = true;
                Plugin.Logger.LogError($"TMP overlay update failed: {ex}");
            }

            RestoreOriginalIfKnown(source);
        }
    }

    internal static bool OnBodyProgress(
        UguiNovelText source,
        int mappedVisibleCharacters)
    {
        if (source == null)
            return false;

        TmpOverlayState state;
        if (!States.TryGetValue(source.GetInstanceID(), out state) ||
            state == null ||
            state.Source != source ||
            !state.IsShowingOverlay)
        {
            return false;
        }

        return ApplyVisibleCharacters(
            state,
            mappedVisibleCharacters,
            false);
    }

    private static bool IsTargetEnabled(StoryTargetKind target)
    {
        if (target == StoryTargetKind.StoryBody)
            return Settings.ApplyToStoryText.Value;

        if (target == StoryTargetKind.SpeakerName)
            return Settings.ApplyToSpeakerName.Value;

        if (target == StoryTargetKind.OptionPreviewBody)
            return Settings.ApplyToOptionPreview.Value;

        return false;
    }

    private static TmpOverlayState GetOrCreateState(Text source)
    {
        int id = source.GetInstanceID();

        TmpOverlayState existing;
        if (States.TryGetValue(id, out existing))
        {
            if (existing != null &&
                existing.Source == source &&
                existing.OverlayObject != null &&
                existing.OverlayText != null)
            {
                return existing;
            }

            States.Remove(id);
        }

        try
        {
            // RectTransform을 처음부터 갖는 자식 GameObject를 만듭니다.
            // 커스텀 MonoBehaviour나 ClassInjector는 사용하지 않습니다.
            GameObject overlayObject = new GameObject(
                OverlayObjectName,
                new[]
                {
                    Il2CppType.Of<RectTransform>(),
                    Il2CppType.Of<CanvasRenderer>()
                });

            overlayObject.hideFlags = HideFlags.DontSave;
            overlayObject.layer = source.gameObject.layer;
            overlayObject.transform.SetParent(source.transform, false);

            RectTransform overlayRect =
                overlayObject.transform.Cast<RectTransform>();

            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            overlayRect.anchoredPosition = Vector2.zero;
            overlayRect.sizeDelta = Vector2.zero;
            overlayRect.localScale = Vector3.one;
            overlayRect.localRotation = Quaternion.identity;

            TextMeshProUGUI overlayText = overlayObject
                .AddComponent(Il2CppType.Of<TextMeshProUGUI>())
                .Cast<TextMeshProUGUI>();

            overlayText.raycastTarget = false;
            overlayText.maskable = source.maskable;
            overlayText.richText = source.supportRichText;
            overlayText.margin = Vector4.zero;
            overlayText.overflowMode = TextOverflowModes.Overflow;

            float sourceAlpha = 1.0f;
            try
            {
                if (source.canvasRenderer != null)
                    sourceAlpha = source.canvasRenderer.GetAlpha();
            }
            catch
            {
                sourceAlpha = 1.0f;
            }

            var state = new TmpOverlayState
            {
                Source = source,
                OverlayObject = overlayObject,
                OverlayRect = overlayRect,
                OverlayText = overlayText,
                OriginalCanvasAlpha = sourceAlpha,
                IsShowingOverlay = false
            };

            overlayObject.SetActive(false);
            States[id] = state;

            if (Settings.FontDebugLogging.Value && CreationLogged.Add(id))
            {
                Plugin.Logger.LogInfo(
                    $"Created TMP overlay for {StoryTargets.Classify(source)} " +
                    $"(sourceId={id}).");
            }

            return state;
        }
        catch (Exception ex)
        {
            if (!_overlayCreationErrorLogged)
            {
                _overlayCreationErrorLogged = true;
                Plugin.Logger.LogError(
                    "TMP overlay creation failed. The original UGUI text was kept. " +
                    $"{ex}");
            }

            return null;
        }
    }

}
