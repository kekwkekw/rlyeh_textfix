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

internal static class StoryTextFix
{
    private static PropertyInfo _generatorTextDataProperty;
    private static bool _castWarningLogged;
    private static bool _textHookErrorLogged;
    private static bool _lengthHookErrorLogged;

    internal static void Initialize()
    {
        _generatorTextDataProperty = AccessTools.Property(
            typeof(UguiNovelTextGenerator),
            "TextData");

        if (_generatorTextDataProperty == null)
        {
            Plugin.Logger.LogWarning(
                "UguiNovelTextGenerator.TextData probe was not found. " +
                "Falling back to visible-character counting.");
        }
    }

    internal static void OnTextAssigned(Text text)
    {
        if (!IsImmediateFullMode())
            return;

        try
        {
            if (StoryTargets.Classify(text) != StoryTargetKind.StoryBody)
                return;

            UguiNovelText novelText = TryCastNovelText(text);
            if (novelText == null)
            {
                LogCastWarningOnce();
                return;
            }

            int fullLength = GetFullVisibleLength(novelText);
            if (fullLength <= 0)
                return;

            novelText.LengthOfView = fullLength;

            if (Settings.DebugLogging.Value)
            {
                string value = SafeGetText(text);
                Plugin.Logger.LogInfo(
                    $"ImmediateFull applied: visibleLength={fullLength}, " +
                    $"rawLength={value.Length}");
            }
        }
        catch (Exception ex)
        {
            LogErrorOnce(
                ref _textHookErrorLogged,
                "Text.text Postfix failed",
                ex);
        }
    }

    internal static void OnLengthOfViewRequested(
        UguiNovelText novelText,
        ref int requestedLength)
    {
        if (!IsImmediateFullMode() || requestedLength < 0)
            return;

        try
        {
            if (StoryTargets.Classify(novelText) != StoryTargetKind.StoryBody)
                return;

            int fullLength = GetFullVisibleLength(novelText);
            if (fullLength <= 0)
                return;

            if (requestedLength < fullLength)
                requestedLength = fullLength;
        }
        catch (Exception ex)
        {
            LogErrorOnce(
                ref _lengthHookErrorLogged,
                "LengthOfView Prefix failed",
                ex);
        }
    }

    private static bool IsImmediateFullMode()
    {
        return Settings.DisplayMode != null &&
               Settings.DisplayMode.Value == StoryDisplayMode.ImmediateFull;
    }

    internal static UguiNovelText TryCastNovelText(Text text)
    {
        try
        {
            return text.Cast<UguiNovelText>();
        }
        catch
        {
            return null;
        }
    }

    internal static int GetFullVisibleLength(UguiNovelText novelText)
    {
        int plainLength = CountVisibleCharacters(SafeGetText(novelText));
        int parsedLength = TryGetParsedTextLength(novelText);
        return Math.Max(plainLength, parsedLength);
    }

    internal static int TryGetParsedTextLength(UguiNovelText novelText)
    {
        if (_generatorTextDataProperty == null)
            return -1;

        try
        {
            UguiNovelTextGenerator generator = novelText.TextGenerator;
            if (generator == null)
                return -1;

            object value = _generatorTextDataProperty.GetValue(generator);
            TextData textData = value as TextData;
            return textData == null ? -1 : textData.Length;
        }
        catch
        {
            return -1;
        }
    }

    internal static string SafeGetText(Text text)
    {
        try
        {
            return text.text ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    internal static int CountVisibleCharacters(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;

        int count = 0;
        bool insideRichTextTag = false;

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];

            if (!insideRichTextTag && c == '<')
            {
                insideRichTextTag = true;
                continue;
            }

            if (insideRichTextTag)
            {
                if (c == '>')
                    insideRichTextTag = false;

                continue;
            }

            if (c == '\r')
                continue;

            if (char.IsHighSurrogate(c) &&
                i + 1 < value.Length &&
                char.IsLowSurrogate(value[i + 1]))
            {
                i++;
            }

            count++;
        }

        return count;
    }

    private static void LogCastWarningOnce()
    {
        if (_castWarningLogged)
            return;

        _castWarningLogged = true;
        Plugin.Logger.LogWarning(
            "The exact story body Text could not be cast to Utage.UguiNovelText. " +
            "The visibility fix was skipped for that instance.");
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

