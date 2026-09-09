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

internal static class Settings
{
    internal static ConfigEntry<StoryDisplayMode> DisplayMode = null;
    internal static ConfigEntry<bool> DebugLogging = null;

    internal static ConfigEntry<bool> FallbackToImmediateFullWhenOriginalLengthUnknown = null;
    internal static ConfigEntry<float> TypewriterProgressScale = null;
    internal static ConfigEntry<int> TypewriterMinimumVisibleCharacters = null;
    internal static ConfigEntry<bool> TypewriterDebugLogging = null;

    internal static ConfigEntry<bool> EnableFirstTextRefresh = null;
    internal static ConfigEntry<int> FirstTextRefreshTriggerAtVisibleCharacters = null;
    internal static ConfigEntry<float> FirstTextRefreshInitialDelaySeconds = null;
    internal static ConfigEntry<int> FirstTextRefreshMaxAttemptsPerLine = null;
    internal static ConfigEntry<float> FirstTextRefreshRetryIntervalSeconds = null;
    internal static ConfigEntry<bool> FirstTextRefreshSpeakerName = null;
    internal static ConfigEntry<bool> FirstTextRefreshDebugLogging = null;

    internal static ConfigEntry<bool> SanitizeDisplayTagsBeforeTranslation = null;
    internal static ConfigEntry<string> TranslationDisplayTagsToStrip = null;
    internal static ConfigEntry<bool> StripTmpShorthandColorTags = null;
    internal static ConfigEntry<bool> TranslationTagDebugLogging = null;

    internal static ConfigEntry<bool> SanitizeUtageTagsForTmpOverlay = null;
    internal static ConfigEntry<string> UtageControlTagsToStrip = null;
    internal static ConfigEntry<bool> UtageTagDebugLogging = null;

    internal static ConfigEntry<StoryFontRenderMode> FontRenderMode = null;
    internal static ConfigEntry<bool> ApplyToStoryText = null;
    internal static ConfigEntry<bool> ApplyToSpeakerName = null;
    internal static ConfigEntry<bool> ApplyToOptionPreview = null;
    internal static ConfigEntry<bool> ApplyOnlyWhenHangulPresent = null;
    internal static ConfigEntry<bool> RestoreOriginalForNonHangul = null;
    internal static ConfigEntry<string> PreferredTmpFontAssetNames = null;
    internal static ConfigEntry<bool> AutoDetectHangulTmpFont = null;
    internal static ConfigEntry<float> FontLookupRetrySeconds = null;
    internal static ConfigEntry<bool> HideOriginalText = null;
    internal static ConfigEntry<float> StoryFontSizeScale = null;
    internal static ConfigEntry<float> SpeakerNameFontSizeScale = null;
    internal static ConfigEntry<float> StoryFontSizeOverride = null;
    internal static ConfigEntry<float> SpeakerNameFontSizeOverride = null;
    internal static ConfigEntry<float> StoryVerticalOffset = null;
    internal static ConfigEntry<float> SpeakerNameVerticalOffset = null;
    internal static ConfigEntry<bool> FontDebugLogging = null;

    internal static void Bind(ConfigFile config)
    {
        DisplayMode = config.Bind(
            "General",
            "DisplayMode",
            StoryDisplayMode.ProportionalTypewriter,
            "Disabled: 스토리 잘림 보정을 끕니다. " +
            "ImmediateFull: 번역문 전체를 즉시 표시합니다. " +
            "ProportionalTypewriter: 원문 타이핑 진행률을 번역문 길이에 " +
            "비례시켜 TMP 오버레이를 점진적으로 표시합니다.");

        DebugLogging = config.Bind(
            "Logging",
            "DebugLogging",
            false,
            "true이면 ImmediateFull 보정의 간단한 로그를 기록합니다.");

        FallbackToImmediateFullWhenOriginalLengthUnknown = config.Bind(
            "Typewriter",
            "FallbackToImmediateFullWhenOriginalLengthUnknown",
            true,
            "원문 전체 길이를 확보하지 못한 문장은 잘림 방지를 위해 " +
            "번역문 전체를 즉시 표시합니다.");

        TypewriterProgressScale = config.Bind(
            "Typewriter",
            "TypewriterProgressScale",
            1.0f,
            new ConfigDescription(
                "원문 진행률에 곱하는 배율입니다. 1은 원래 타이밍, " +
                "1보다 크면 번역문이 더 빨리 완성됩니다.",
                new AcceptableValueRange<float>(0.25f, 4.0f)));

        TypewriterMinimumVisibleCharacters = config.Bind(
            "Typewriter",
            "TypewriterMinimumVisibleCharacters",
            0,
            new ConfigDescription(
                "타이핑이 시작된 뒤 최소 표시할 번역문 문자 수입니다.",
                new AcceptableValueRange<int>(0, 32)));

        TypewriterDebugLogging = config.Bind(
            "Typewriter",
            "TypewriterDebugLogging",
            false,
            "true이면 문장별 원문/번역문 길이와 일부 진행률 매핑을 기록합니다.");

        EnableFirstTextRefresh = config.Bind(
            "FirstTextRefresh",
            "Enable",
            true,
            "컷씬 전환 직후 XUnity가 놓친 첫 스토리 원문을 정확한 MessageText에 " +
            "한해 자기 자신으로 한 번 재설정하여 번역 감지를 다시 발생시킵니다.");

        FirstTextRefreshTriggerAtVisibleCharacters = config.Bind(
            "FirstTextRefresh",
            "TriggerAtVisibleCharacters",
            0,
            new ConfigDescription(
                "원문 LengthOfView가 이 값 이상이 된 뒤 새로고침을 시도합니다. " +
                "기본값 0은 원문이 준비된 첫 LengthOfView 갱신 시점입니다.",
                new AcceptableValueRange<int>(0, 8)));

        FirstTextRefreshInitialDelaySeconds = config.Bind(
            "FirstTextRefresh",
            "InitialDelaySeconds",
            0.0f,
            new ConfigDescription(
                "새 원문을 감지한 뒤 첫 자기 재설정까지 기다릴 시간입니다.",
                new AcceptableValueRange<float>(0.0f, 2.0f)));

        FirstTextRefreshMaxAttemptsPerLine = config.Bind(
            "FirstTextRefresh",
            "MaxAttemptsPerLine",
            1,
            new ConfigDescription(
                "한 문장에 수행할 최대 자기 재설정 횟수입니다. " +
                "중복 온라인 요청을 피하려면 기본값 1을 유지하십시오.",
                new AcceptableValueRange<int>(1, 3)));

        FirstTextRefreshRetryIntervalSeconds = config.Bind(
            "FirstTextRefresh",
            "RetryIntervalSeconds",
            0.75f,
            new ConfigDescription(
                "MaxAttemptsPerLine이 2 이상일 때 재시도 간격입니다.",
                new AcceptableValueRange<float>(0.1f, 5.0f)));

        FirstTextRefreshSpeakerName = config.Bind(
            "FirstTextRefresh",
            "RefreshSpeakerName",
            true,
            "본문 첫 문장을 재감지할 때 같은 메시지 창의 일본어 화자 이름도 " +
            "한 번 자기 자신으로 재설정합니다.");

        FirstTextRefreshDebugLogging = config.Bind(
            "FirstTextRefresh",
            "DebugLogging",
            false,
            "true이면 문장별 새로고침 준비, 발송 및 번역 도착 정보를 기록합니다.");

        SanitizeDisplayTagsBeforeTranslation = config.Bind(
            "TextTags",
            "SanitizeDisplayTagsBeforeTranslation",
            true,
            "true이면 XUnity RichTextParser가 문장을 나누기 전에 지정된 " +
            "표시 전용 태그를 번역 템플릿에서 제외하고, 태그 양쪽의 문장을 " +
            "하나의 번역 단위로 합칩니다. 게임 오브젝트의 원문 문자열 자체는 " +
            "변경하지 않습니다.");

        TranslationDisplayTagsToStrip = config.Bind(
            "TextTags",
            "TranslationDisplayTagsToStrip",
            "cspace;size;color;indent;line-indent;line-height;scale;" +
            "voffset;pos;width;margin;margin-left;margin-right",
            "번역 문맥에서는 제거할 TMP 표시 태그 이름입니다. 세미콜론, " +
            "쉼표, 세로줄 또는 공백으로 구분합니다. interval, speed, sound, " +
            "sprite, br, link, page, nobr는 설정에 적어도 안전상 보존됩니다.");

        StripTmpShorthandColorTags = config.Bind(
            "TextTags",
            "StripTmpShorthandColorTags",
            true,
            "true이면 <#RRGGBB> 및 <#RRGGBBAA> TMP 축약 색상 태그도 " +
            "번역 템플릿에서 제거합니다. 대응하는 </color>는 color 설정에 " +
            "따라 함께 제거됩니다.");

        TranslationTagDebugLogging = config.Bind(
            "TextTags",
            "TranslationTagDebugLogging",
            false,
            "true이면 표시 태그가 제거되어 하나의 번역 단위로 합쳐진 " +
            "문자열을 제한적으로 기록합니다.");

        SanitizeUtageTagsForTmpOverlay = config.Bind(
            "TextTags",
            "SanitizeUtageTagsForTmpOverlay",
            true,
            "true이면 원래 UTAGE UGUI만 해석할 수 있는 제어 태그를 " +
            "TMP 오버레이에 복사하기 전에 제거합니다. 원본 UGUI 문자열은 " +
            "건드리지 않으므로 interval/speed 타이밍은 게임이 계속 처리합니다.");

        UtageControlTagsToStrip = config.Bind(
            "TextTags",
            "UtageControlTagsToStrip",
            "interval;speed",
            "TMP 오버레이에서 제거할 UTAGE 전용 제어 태그 이름입니다. " +
            "세미콜론, 쉼표, 세로줄 또는 공백으로 구분합니다. " +
            "여는 태그와 닫는 태그를 모두 제거합니다.");

        UtageTagDebugLogging = config.Bind(
            "TextTags",
            "UtageTagDebugLogging",
            false,
            "true이면 TMP 오버레이에서 제거한 UTAGE 제어 태그 수를 기록합니다.");

        FontRenderMode = config.Bind(
            "Font",
            "FontRenderMode",
            StoryFontRenderMode.TmpOverlay,
            "Disabled: 폰트 통일 기능을 끕니다. " +
            "TmpOverlay: 기존 UGUI 위에 TextMeshProUGUI를 겹쳐 " +
            "XUnity가 로드한 동일 TMP 폰트 에셋으로 표시합니다.");

        ApplyToStoryText = config.Bind(
            "Font",
            "ApplyToStoryText",
            true,
            "스토리 본문 MessageText에 TMP 오버레이를 적용합니다.");

        ApplyToSpeakerName = config.Bind(
            "Font",
            "ApplyToSpeakerName",
            true,
            "화자 이름 NameText에 TMP 오버레이를 적용합니다.");

        ApplyToOptionPreview = config.Bind(
            "Font",
            "ApplyToOptionPreview",
            true,
            "옵션 화면의 텍스트 속도 예시 MessageText에도 동일 TMP 폰트와 " +
            "원문 진행률 기반 타이핑 보정을 적용합니다.");

        ApplyOnlyWhenHangulPresent = config.Bind(
            "Font",
            "ApplyOnlyWhenHangulPresent",
            true,
            "true이면 현재 문자열에 한글이 있을 때만 TMP 오버레이를 표시합니다. " +
            "Alt+T의 일본어 원문에는 원래 UGUI 폰트를 유지합니다.");

        RestoreOriginalForNonHangul = config.Bind(
            "Font",
            "RestoreOriginalForNonHangul",
            true,
            "ApplyOnlyWhenHangulPresent=true일 때 빈 문자열이나 일본어 원문으로 " +
            "바뀌면 오버레이를 숨기고 원래 UGUI 렌더링을 복원합니다.");

        PreferredTmpFontAssetNames = config.Bind(
            "Font",
            "PreferredTmpFontAssetNames",
            "RlyehNotoSerifKR SDF;notoserifkr_sdf",
            "우선 탐색할 TMP_FontAsset 이름입니다. 세미콜론 또는 쉼표로 구분합니다. " +
            "공백, 밑줄, 대소문자는 비교에서 무시합니다. " +
            "현재 번들의 내부 에셋 이름은 RlyehNotoSerifKR SDF입니다.");

        AutoDetectHangulTmpFont = config.Bind(
            "Font",
            "AutoDetectHangulTmpFont",
            true,
            "선호 이름이 바뀐 교체 번들이더라도, 로드된 TMP 폰트 중 한글 글리프를 " +
            "지원하는 에셋을 자동 탐색합니다.");

        FontLookupRetrySeconds = config.Bind(
            "Font",
            "FontLookupRetrySeconds",
            1.0f,
            new ConfigDescription(
                "XUnity 폰트 로드가 아직 끝나지 않았을 때 재탐색하는 간격입니다.",
                new AcceptableValueRange<float>(0.1f, 10.0f)));

        HideOriginalText = config.Bind(
            "Font",
            "HideOriginalText",
            true,
            "true이면 TMP 오버레이가 표시되는 동안 원래 UGUI 글자를 숨깁니다. " +
            "false는 겹침 확인용 진단 설정입니다.");

        StoryFontSizeScale = config.Bind(
            "Font",
            "StoryFontSizeScale",
            1.0f,
            new ConfigDescription(
                "StoryFontSizeOverride=0일 때 기존 본문 fontSize에 곱할 값입니다.",
                new AcceptableValueRange<float>(0.25f, 4.0f)));

        SpeakerNameFontSizeScale = config.Bind(
            "Font",
            "SpeakerNameFontSizeScale",
            1.0f,
            new ConfigDescription(
                "SpeakerNameFontSizeOverride=0일 때 기존 이름 fontSize에 곱할 값입니다.",
                new AcceptableValueRange<float>(0.25f, 4.0f)));

        StoryFontSizeOverride = config.Bind(
            "Font",
            "StoryFontSizeOverride",
            0.0f,
            new ConfigDescription(
                "0이면 기존 본문 fontSize와 StoryFontSizeScale을 사용합니다. " +
                "양수이면 TMP 본문 크기를 해당 값으로 고정합니다.",
                new AcceptableValueRange<float>(0.0f, 128.0f)));

        SpeakerNameFontSizeOverride = config.Bind(
            "Font",
            "SpeakerNameFontSizeOverride",
            0.0f,
            new ConfigDescription(
                "0이면 기존 이름 fontSize와 SpeakerNameFontSizeScale을 사용합니다. " +
                "양수이면 TMP 이름 크기를 해당 값으로 고정합니다.",
                new AcceptableValueRange<float>(0.0f, 128.0f)));

        StoryVerticalOffset = config.Bind(
            "Font",
            "StoryVerticalOffset",
            0.0f,
            new ConfigDescription(
                "TMP 본문 오버레이의 세로 위치 보정값입니다.",
                new AcceptableValueRange<float>(-100.0f, 100.0f)));

        SpeakerNameVerticalOffset = config.Bind(
            "Font",
            "SpeakerNameVerticalOffset",
            0.0f,
            new ConfigDescription(
                "TMP 화자 이름 오버레이의 세로 위치 보정값입니다.",
                new AcceptableValueRange<float>(-100.0f, 100.0f)));

        FontDebugLogging = config.Bind(
            "Font",
            "FontDebugLogging",
            false,
            "true이면 TMP 폰트 탐색, 오버레이 생성 및 적용 정보를 기록합니다.");
    }
}


