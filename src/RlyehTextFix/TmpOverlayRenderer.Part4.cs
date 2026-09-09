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
    private static bool InvokeHasCharacter(
        TMP_FontAsset font,
        char character)
    {
        ParameterInfo[] parameters = _hasCharacterMethod.GetParameters();
        object[] args = new object[parameters.Length];

        Type first = parameters[0].ParameterType;
        if (first == typeof(char))
            args[0] = character;
        else if (first == typeof(uint))
            args[0] = (uint)character;
        else
            args[0] = (int)character;

        for (int i = 1; i < args.Length; i++)
            args[i] = false;

        object result = _hasCharacterMethod.Invoke(font, args);
        return result is bool && (bool)result;
    }

    private static float GetFontSize(
        Text source,
        StoryTargetKind target)
    {
        float fixedSize = target == StoryTargetKind.SpeakerName
            ? Settings.SpeakerNameFontSizeOverride.Value
            : Settings.StoryFontSizeOverride.Value;

        if (fixedSize > 0.0f)
            return fixedSize;

        float scale = target == StoryTargetKind.SpeakerName
            ? Settings.SpeakerNameFontSizeScale.Value
            : Settings.StoryFontSizeScale.Value;

        return Math.Max(1.0f, source.fontSize * scale);
    }

    private static TextAlignmentOptions MapAlignment(TextAnchor anchor)
    {
        switch (anchor)
        {
            case TextAnchor.UpperLeft:
                return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter:
                return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight:
                return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft:
                return TextAlignmentOptions.Left;
            case TextAnchor.MiddleCenter:
                return TextAlignmentOptions.Center;
            case TextAnchor.MiddleRight:
                return TextAlignmentOptions.Right;
            case TextAnchor.LowerLeft:
                return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter:
                return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight:
                return TextAlignmentOptions.BottomRight;
            default:
                return TextAlignmentOptions.TopLeft;
        }
    }

    private static FontStyles MapFontStyle(FontStyle style)
    {
        switch (style)
        {
            case FontStyle.Bold:
                return FontStyles.Bold;
            case FontStyle.Italic:
                return FontStyles.Italic;
            case FontStyle.BoldAndItalic:
                return FontStyles.Bold | FontStyles.Italic;
            default:
                return FontStyles.Normal;
        }
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

    private static string SafeGetText(Text text)
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

    private static string SafeObjectName(UnityEngine.Object obj)
    {
        if (obj == null)
            return "<null>";

        try
        {
            return obj.name ?? "<unnamed>";
        }
        catch
        {
            return "<unavailable>";
        }
    }

    private static string[] SplitNames(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Array.Empty<string>();

        string[] raw = value.Split(
            new[] { ';', ',' },
            StringSplitOptions.RemoveEmptyEntries);

        var result = new List<string>(raw.Length);
        for (int i = 0; i < raw.Length; i++)
        {
            string name = raw[i].Trim();
            if (name.Length > 0)
                result.Add(name);
        }

        return result.ToArray();
    }

    private static string NormalizeName(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        char[] buffer = new char[value.Length];
        int length = 0;

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (!char.IsLetterOrDigit(c))
                continue;

            buffer[length++] = char.ToLowerInvariant(c);
        }

        return new string(buffer, 0, length);
    }

    private static void LogFontNotFoundOnce(string reason)
    {
        if (_fontNotFoundWarningLogged)
            return;

        _fontNotFoundWarningLogged = true;
        Plugin.Logger.LogWarning(
            "The XUnity-loaded TMP font asset has not been resolved yet. " +
            $"Reason={reason} The plugin will retry on later story text assignments.");
    }
}
