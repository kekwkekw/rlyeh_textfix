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

internal static class Patches
{
    internal static void TextSetterPrefix(
        Text __instance,
        string __0,
        out TextSetterPatchState __state)
    {
        __state = StoryProgressTracker.BeginTextAssignment(__instance, __0);
    }

    internal static void TextSetterPostfix(
        Text __instance,
        string __0,
        TextSetterPatchState __state)
    {
        StoryTargetKind target = __state == null
            ? StoryTargets.Classify(__instance)
            : __state.Target;

        if (target == StoryTargetKind.None)
            return;

        if (StoryTargets.IsBodyTarget(target))
        {
            StoryProgressTracker.EndTextAssignment(
                __instance,
                __0,
                __state);

            FirstTextRefreshController.OnTextAssigned(
                __instance,
                target,
                __0,
                __state);

            if (target == StoryTargetKind.StoryBody)
                StoryTextFix.OnTextAssigned(__instance);
        }

        TmpOverlayRenderer.OnTextAssigned(__instance, target);
    }

    internal static void NovelLengthPrefix(UguiNovelText __instance, ref int __0)
    {
        FirstTextRefreshController.OnLengthOfViewRequested(__instance, __0);
        StoryProgressTracker.OnLengthOfViewRequested(__instance, ref __0);
        StoryTextFix.OnLengthOfViewRequested(__instance, ref __0);
    }
}

