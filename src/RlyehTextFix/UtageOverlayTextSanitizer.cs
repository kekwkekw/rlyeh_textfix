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

internal static class UtageOverlayTextSanitizer
{
    private static string _cachedConfigValue = null;
    private static HashSet<string> _cachedTagNames =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    internal static string Sanitize(
        string value,
        out int removedTagCount)
    {
        removedTagCount = 0;

        if (string.IsNullOrEmpty(value) ||
            Settings.SanitizeUtageTagsForTmpOverlay == null ||
            !Settings.SanitizeUtageTagsForTmpOverlay.Value)
        {
            return value ?? string.Empty;
        }

        HashSet<string> stripTags = GetConfiguredTagNames();
        if (stripTags.Count == 0 || value.IndexOf('<') < 0)
            return value;

        StringBuilder builder = null;
        int copyStart = 0;

        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] != '<')
                continue;

            int tagEnd = FindTagEnd(value, i + 1);
            if (tagEnd < 0)
                break;

            string tagName = ReadTagName(value, i + 1, tagEnd);
            if (tagName.Length == 0 || !stripTags.Contains(tagName))
            {
                i = tagEnd;
                continue;
            }

            if (builder == null)
                builder = new StringBuilder(value.Length);

            builder.Append(value, copyStart, i - copyStart);
            copyStart = tagEnd + 1;
            removedTagCount++;
            i = tagEnd;
        }

        if (builder == null)
            return value;

        if (copyStart < value.Length)
        {
            builder.Append(
                value,
                copyStart,
                value.Length - copyStart);
        }

        return builder.ToString();
    }

    private static HashSet<string> GetConfiguredTagNames()
    {
        string configured = Settings.UtageControlTagsToStrip == null
            ? string.Empty
            : Settings.UtageControlTagsToStrip.Value ?? string.Empty;

        if (string.Equals(
                configured,
                _cachedConfigValue,
                StringComparison.Ordinal))
        {
            return _cachedTagNames;
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] parts = configured.Split(
            new[] { ';', ',', '|', ' ', '\t', '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < parts.Length; i++)
        {
            string name = parts[i].Trim();
            if (name.Length > 0)
                names.Add(name);
        }

        _cachedConfigValue = configured;
        _cachedTagNames = names;
        return _cachedTagNames;
    }

    private static int FindTagEnd(string value, int start)
    {
        char quote = '\0';

        for (int i = start; i < value.Length; i++)
        {
            char c = value[i];

            if (quote != '\0')
            {
                if (c == quote)
                    quote = '\0';

                continue;
            }

            if (c == '\'' || c == '"')
            {
                quote = c;
                continue;
            }

            if (c == '>')
                return i;
        }

        return -1;
    }

    private static string ReadTagName(
        string value,
        int start,
        int endExclusive)
    {
        int index = start;

        while (index < endExclusive && char.IsWhiteSpace(value[index]))
            index++;

        if (index < endExclusive && value[index] == '/')
        {
            index++;
            while (index < endExclusive && char.IsWhiteSpace(value[index]))
                index++;
        }

        int nameStart = index;
        while (index < endExclusive)
        {
            char c = value[index];
            if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-'))
                break;

            index++;
        }

        if (index <= nameStart)
            return string.Empty;

        return value.Substring(nameStart, index - nameStart);
    }
}

