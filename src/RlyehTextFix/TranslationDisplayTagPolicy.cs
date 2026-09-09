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

internal static class TranslationDisplayTagPolicy
{
    private static readonly HashSet<string> ProtectedTags =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "interval",
            "speed",
            "sound",
            "sprite",
            "br",
            "link",
            "page",
            "nobr"
        };

    private static string _cachedConfigValue = null;
    private static HashSet<string> _cachedTagNames =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static bool _protectedTagWarningLogged;

    internal static HashSet<string> GetConfiguredTagNames()
    {
        string configured = Settings.TranslationDisplayTagsToStrip == null
            ? string.Empty
            : Settings.TranslationDisplayTagsToStrip.Value ?? string.Empty;

        if (string.Equals(
                configured,
                _cachedConfigValue,
                StringComparison.Ordinal))
        {
            return _cachedTagNames;
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var protectedNames = new List<string>();
        string[] parts = configured.Split(
            new[] { ';', ',', '|', ' ', '\t', '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < parts.Length; i++)
        {
            string name = parts[i].Trim();
            if (name.Length == 0)
                continue;

            if (ProtectedTags.Contains(name))
            {
                protectedNames.Add(name);
                continue;
            }

            names.Add(name);
        }

        _cachedConfigValue = configured;
        _cachedTagNames = names;

        if (protectedNames.Count > 0 && !_protectedTagWarningLogged)
        {
            _protectedTagWarningLogged = true;
            Plugin.Logger.LogWarning(
                "Protected control/meaning tag(s) were excluded from " +
                "TranslationDisplayTagsToStrip: " +
                string.Join(", ", protectedNames));
        }

        return _cachedTagNames;
    }

    internal static bool TryCollectTargetTagNames(
        string value,
        List<string> exactTagNames,
        out int matchedTagCount)
    {
        matchedTagCount = 0;

        if (string.IsNullOrEmpty(value) || value.IndexOf('<') < 0)
            return false;

        HashSet<string> configured = GetConfiguredTagNames();
        bool stripShorthandColor =
            Settings.StripTmpShorthandColorTags != null &&
            Settings.StripTmpShorthandColorTags.Value;

        if (configured.Count == 0 && !stripShorthandColor)
            return false;

        bool matched = false;
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] != '<')
                continue;

            int tagEnd = FindTagEnd(value, i + 1);
            if (tagEnd < 0)
                break;

            bool shorthandColor;
            string tagName = ReadTagName(
                value,
                i + 1,
                tagEnd,
                out shorthandColor);

            if (shorthandColor)
            {
                if (stripShorthandColor)
                {
                    matched = true;
                    matchedTagCount++;
                }
            }
            else if (tagName.Length > 0 && configured.Contains(tagName))
            {
                matched = true;
                matchedTagCount++;

                if (exactTagNames != null &&
                    !ContainsOrdinal(exactTagNames, tagName))
                {
                    exactTagNames.Add(tagName);
                }
            }

            i = tagEnd;
        }

        return matched;
    }

    internal static bool IsTmpShorthandColorTagName(string value)
    {
        if (Settings.StripTmpShorthandColorTags == null ||
            !Settings.StripTmpShorthandColorTags.Value ||
            string.IsNullOrEmpty(value) ||
            value[0] != '#')
        {
            return false;
        }

        int hexLength = value.Length - 1;
        if (hexLength != 6 && hexLength != 8)
            return false;

        for (int i = 1; i < value.Length; i++)
        {
            char c = value[i];
            bool isHex =
                (c >= '0' && c <= '9') ||
                (c >= 'a' && c <= 'f') ||
                (c >= 'A' && c <= 'F');

            if (!isHex)
                return false;
        }

        return true;
    }

    private static bool ContainsOrdinal(List<string> values, string value)
    {
        for (int i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i], value, StringComparison.Ordinal))
                return true;
        }

        return false;
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
        int endExclusive,
        out bool shorthandColor)
    {
        shorthandColor = false;
        int index = start;

        while (index < endExclusive && char.IsWhiteSpace(value[index]))
            index++;

        bool isEndTag = index < endExclusive && value[index] == '/';
        if (isEndTag)
        {
            index++;
            while (index < endExclusive && char.IsWhiteSpace(value[index]))
                index++;
        }

        if (!isEndTag && index < endExclusive && value[index] == '#')
        {
            int nameEnd = index + 1;
            while (nameEnd < endExclusive && IsHex(value[nameEnd]))
                nameEnd++;

            int hexLength = nameEnd - index - 1;
            int trailing = nameEnd;
            while (trailing < endExclusive && char.IsWhiteSpace(value[trailing]))
                trailing++;

            if (trailing == endExclusive &&
                (hexLength == 6 || hexLength == 8))
            {
                shorthandColor = true;
                return value.Substring(index, nameEnd - index);
            }
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

    private static bool IsHex(char c)
    {
        return
            (c >= '0' && c <= '9') ||
            (c >= 'a' && c <= 'f') ||
            (c >= 'A' && c <= 'F');
    }
}

