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

internal static class StoryTargets
{
    internal const string StoryBodyPath =
        "/AdvEngine/UI/MessageWindowManager/MessageWindow/" +
        "RootChildren/MessageTexts/MessageText";

    internal const string SpeakerNamePath =
        "/AdvEngine/UI/MessageWindowManager/MessageWindow/" +
        "RootChildren/MessageTexts/NameBg/NameText";

    internal const string OptionPreviewBodyPath =
        "/AdvEngine/UI/PopupParent/Page/OptionSettingPopup(Clone)/" +
        "UILayer/WindowBase/ContentArea/TextView/Viewport/Content/" +
        "AdvOptionSetting/AdvEngine/UI/MessageWindowManager/MessageWindow/" +
        "RootChildren/MessageTexts/MessageText";

    private static readonly string[] StoryBodyLeafToRoot =
    {
        "MessageText",
        "MessageTexts",
        "RootChildren",
        "MessageWindow",
        "MessageWindowManager",
        "UI",
        "AdvEngine"
    };

    private static readonly string[] SpeakerNameLeafToRoot =
    {
        "NameText",
        "NameBg",
        "MessageTexts",
        "RootChildren",
        "MessageWindow",
        "MessageWindowManager",
        "UI",
        "AdvEngine"
    };

    private static readonly string[] OptionPreviewBodyLeafToRoot =
    {
        "MessageText",
        "MessageTexts",
        "RootChildren",
        "MessageWindow",
        "MessageWindowManager",
        "UI",
        "AdvEngine",
        "AdvOptionSetting",
        "Content",
        "Viewport",
        "TextView",
        "ContentArea",
        "WindowBase",
        "UILayer",
        "OptionSettingPopup(Clone)",
        "Page",
        "PopupParent",
        "UI",
        "AdvEngine"
    };

    internal static bool IsBodyTarget(StoryTargetKind target)
    {
        return target == StoryTargetKind.StoryBody ||
               target == StoryTargetKind.OptionPreviewBody;
    }

    internal static StoryTargetKind Classify(Text text)
    {
        if (text == null)
            return StoryTargetKind.None;

        try
        {
            Transform transform = text.transform;

            if (MatchesExactRootPath(transform, StoryBodyLeafToRoot))
                return StoryTargetKind.StoryBody;

            if (MatchesExactRootPath(transform, SpeakerNameLeafToRoot))
                return StoryTargetKind.SpeakerName;

            if (MatchesExactRootPath(transform, OptionPreviewBodyLeafToRoot))
                return StoryTargetKind.OptionPreviewBody;
        }
        catch
        {
            // 다른 UI에 영향을 주지 않도록 대상 판정 실패는 조용히 무시합니다.
        }

        return StoryTargetKind.None;
    }

    private static bool MatchesExactRootPath(
        Transform current,
        string[] leafToRoot)
    {
        for (int i = 0; i < leafToRoot.Length; i++)
        {
            if (current == null ||
                !string.Equals(
                    current.name,
                    leafToRoot[i],
                    StringComparison.Ordinal))
            {
                return false;
            }

            current = current.parent;
        }

        return current == null;
    }
}

