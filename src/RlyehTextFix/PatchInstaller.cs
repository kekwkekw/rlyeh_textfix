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

internal static class PatchInstaller
{
    internal static int Apply(Harmony harmony)
    {
        int installed = 0;

        if (Patch(
                harmony,
                AccessTools.PropertySetter(typeof(Text), nameof(Text.text)),
                "UnityEngine.UI.Text.text setter",
                prefixName: nameof(Patches.TextSetterPrefix),
                postfixName: nameof(Patches.TextSetterPostfix)))
        {
            installed++;
        }

        if (Patch(
                harmony,
                AccessTools.PropertySetter(
                    typeof(UguiNovelText),
                    nameof(UguiNovelText.LengthOfView)),
                "Utage.UguiNovelText.LengthOfView setter",
                prefixName: nameof(Patches.NovelLengthPrefix),
                postfixName: null))
        {
            installed++;
        }

        return installed;
    }

    private static bool Patch(
        Harmony harmony,
        MethodBase original,
        string label,
        string prefixName,
        string postfixName)
    {
        if (original == null)
        {
            Plugin.Logger.LogError($"Patch target not found: {label}");
            return false;
        }

        MethodInfo prefix = prefixName == null
            ? null
            : AccessTools.Method(typeof(Patches), prefixName);

        MethodInfo postfix = postfixName == null
            ? null
            : AccessTools.Method(typeof(Patches), postfixName);

        if (prefixName != null && prefix == null)
        {
            Plugin.Logger.LogError($"Prefix method not found: {prefixName}");
            return false;
        }

        if (postfixName != null && postfix == null)
        {
            Plugin.Logger.LogError($"Postfix method not found: {postfixName}");
            return false;
        }

        try
        {
            harmony.Patch(
                original,
                prefix == null ? null : new HarmonyMethod(prefix),
                postfix == null ? null : new HarmonyMethod(postfix));

            return true;
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"Patch failed: {label}\n{ex}");
            return false;
        }
    }
}

