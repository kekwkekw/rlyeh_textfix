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

internal static class XUnityRichTextHarmonyPatches
{
    internal static void ParsePrefix(
        string __0,
        out XUnityRichTextParseScopeState __state)
    {
        __state = XUnityRichTextPatchController.EnterParse(__0);
    }

    internal static IEnumerable<CodeInstruction> ParseTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        MethodInfo thresholdMethod = AccessTools.Method(
            typeof(XUnityRichTextPatchController),
            nameof(XUnityRichTextPatchController.GetTemplateLengthThreshold));
        MethodInfo poundEvaluator = AccessTools.Method(
            typeof(XUnityRichTextPatchController),
            nameof(XUnityRichTextPatchController.EvaluateStartsWithPound));

        if (thresholdMethod == null || poundEvaluator == null)
        {
            throw new MissingMethodException(
                nameof(XUnityRichTextPatchController),
                "RichTextParser helper method");
        }

        int thresholdReplacementCount = 0;
        int poundCallReplacementCount = 0;

        for (int i = 0; i < codes.Count; i++)
        {
            CodeInstruction current = codes[i];

            if ((current.opcode == OpCodes.Call ||
                 current.opcode == OpCodes.Callvirt) &&
                current.operand is MethodInfo calledMethod &&
                calledMethod.IsStatic &&
                calledMethod.ReturnType == typeof(bool) &&
                string.Equals(
                    calledMethod.Name,
                    "StartsWithPound",
                    StringComparison.Ordinal) &&
                string.Equals(
                    calledMethod.DeclaringType == null
                        ? null
                        : calledMethod.DeclaringType.FullName,
                    "XUnity.AutoTranslator.Plugin.Core.Parsing.RichTextParser",
                    StringComparison.Ordinal))
            {
                ParameterInfo[] parameters = calledMethod.GetParameters();
                if (parameters.Length == 2 &&
                    parameters[0].ParameterType == typeof(string) &&
                    parameters[1].ParameterType == typeof(int))
                {
                    current.opcode = OpCodes.Call;
                    current.operand = poundEvaluator;
                    poundCallReplacementCount++;
                }
            }
        }

        for (int i = 1; i + 1 < codes.Count; i++)
        {
            if (codes[i].opcode != OpCodes.Ldc_I4_5)
                continue;

            if (codes[i + 1].opcode != OpCodes.Cgt)
                continue;

            CodeInstruction previous = codes[i - 1];
            bool isStringLengthCall =
                (previous.opcode == OpCodes.Call ||
                 previous.opcode == OpCodes.Callvirt) &&
                previous.operand is MethodInfo method &&
                method.DeclaringType == typeof(string) &&
                string.Equals(
                    method.Name,
                    "get_Length",
                    StringComparison.Ordinal);

            if (!isStringLengthCall)
                continue;

            // 원래 ldc.i4.5를 호출별 동적 임계값(5 또는 4)으로 교체합니다.
            // CodeInstruction 객체를 그대로 수정하여 labels/blocks를 보존합니다.
            codes[i].opcode = OpCodes.Call;
            codes[i].operand = thresholdMethod;
            thresholdReplacementCount++;
        }

        if (thresholdReplacementCount != 1 ||
            poundCallReplacementCount != 1)
        {
            throw new InvalidOperationException(
                "Expected exactly one RichTextParser template-length guard " +
                "and one StartsWithPound call, but found guard=" +
                thresholdReplacementCount + ", poundCall=" +
                poundCallReplacementCount + ".");
        }

        return codes;
    }

    internal static Exception ParseFinalizer(
        Exception __exception,
        XUnityRichTextParseScopeState __state)
    {
        XUnityRichTextPatchController.ExitParse(__state);
        return __exception;
    }

}

