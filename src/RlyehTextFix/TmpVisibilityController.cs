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

internal static class TmpVisibilityController
{
    private static PropertyInfo _backingProperty;
    private static FieldInfo _backingField;
    private static PropertyInfo _publicProperty;
    private static string _writerDescription = "uninitialized";
    private static string _selectedWriter = string.Empty;
    private static bool _writeFailureLogged;
    private static bool _overrideWarningLogged;
    private static bool _meshRefreshWarningLogged;

    internal static string WriterDescription
    {
        get { return _writerDescription; }
    }

    internal static void Initialize()
    {
        // Harmony AccessTools logs a warning whenever an optional field is
        // absent. This Unity/TMP build exposes the IL2CPP backing member as a
        // property, so probe the hierarchy silently and keep the same
        // property -> field -> public-property fallback order.
        _backingProperty = FindPropertyInHierarchy(
            typeof(TMP_Text),
            "m_maxVisibleCharacters");

        _backingField = FindFieldInHierarchy(
            typeof(TMP_Text),
            "m_maxVisibleCharacters");

        _publicProperty = FindPropertyInHierarchy(
            typeof(TMP_Text),
            nameof(TMP_Text.maxVisibleCharacters));

        var candidates = new List<string>();
        if (_backingProperty != null && _backingProperty.CanWrite)
            candidates.Add("m_maxVisibleCharacters-property");
        if (_backingField != null)
            candidates.Add("m_maxVisibleCharacters-field");
        if (_publicProperty != null && _publicProperty.CanWrite)
            candidates.Add("maxVisibleCharacters-public-property");

        _writerDescription = candidates.Count == 0
            ? "none"
            : string.Join(" -> ", candidates.ToArray()) +
              " + SetAllDirty/ForceMeshUpdate";
    }

    private static PropertyInfo FindPropertyInHierarchy(
        Type type,
        string name)
    {
        const BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly;

        for (Type current = type; current != null; current = current.BaseType)
        {
            PropertyInfo property = current.GetProperty(name, flags);
            if (property != null)
                return property;
        }

        return null;
    }

    private static FieldInfo FindFieldInHierarchy(
        Type type,
        string name)
    {
        const BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly;

        for (Type current = type; current != null; current = current.BaseType)
        {
            FieldInfo field = current.GetField(name, flags);
            if (field != null)
                return field;
        }

        return null;
    }

    internal static bool SetVisibleCharacters(
        TMP_Text text,
        int visibleCharacters)
    {
        if (text == null)
            return false;

        int value = Math.Max(0, visibleCharacters);

        if (TrySetBackingProperty(text, value) ||
            TrySetBackingField(text, value) ||
            TrySetPublicProperty(text, value))
        {
            RefreshMesh(text);
            return true;
        }

        if (!_writeFailureLogged)
        {
            _writeFailureLogged = true;
            Plugin.Logger.LogError(
                "No usable TMP max-visible-character writer was found. " +
                "ProportionalTypewriter cannot update the overlay progressively. " +
                "ImmediateFull mode remains available.");
        }

        return false;
    }

    private static void RefreshMesh(TMP_Text text)
    {
        bool forceSucceeded = false;

        try
        {
            text.SetAllDirty();
        }
        catch
        {
            try
            {
                text.SetVerticesDirty();
            }
            catch
            {
            }
        }

        try
        {
            text.ForceMeshUpdate();
            forceSucceeded = true;
        }
        catch
        {
            try
            {
                text.SetVerticesDirty();
            }
            catch
            {
            }
        }

        if (!forceSucceeded && !_meshRefreshWarningLogged)
        {
            _meshRefreshWarningLogged = true;
            Plugin.Logger.LogWarning(
                "TMP ForceMeshUpdate failed. The canvas will retry the dirty " +
                "overlay on its normal rebuild pass.");
        }
    }

    private static bool TrySetBackingProperty(TMP_Text text, int value)
    {
        if (_backingProperty == null || !_backingProperty.CanWrite)
            return false;

        try
        {
            _backingProperty.SetValue(text, value);
            if (!VerifyAppliedValue(text, value))
            {
                _backingProperty = null;
                return false;
            }

            SelectWriterOnce("m_maxVisibleCharacters-property");
            return true;
        }
        catch
        {
            _backingProperty = null;
            return false;
        }
    }

    private static bool TrySetBackingField(TMP_Text text, int value)
    {
        if (_backingField == null)
            return false;

        try
        {
            _backingField.SetValue(text, value);
            if (!VerifyAppliedValue(text, value))
            {
                _backingField = null;
                return false;
            }

            SelectWriterOnce("m_maxVisibleCharacters-field");
            return true;
        }
        catch
        {
            _backingField = null;
            return false;
        }
    }

    private static bool TrySetPublicProperty(TMP_Text text, int value)
    {
        if (_publicProperty == null || !_publicProperty.CanWrite)
            return false;

        try
        {
            _publicProperty.SetValue(text, value);
            if (!VerifyAppliedValue(text, value))
            {
                if (!_overrideWarningLogged)
                {
                    _overrideWarningLogged = true;
                    Plugin.Logger.LogWarning(
                        "TMP maxVisibleCharacters did not retain the requested value. " +
                        "Another hook may be forcing full visibility. " +
                        "For an A/B test only, set XUnity's " +
                        "DisableTextMeshProScrollInEffects=False and restart.");
                }

                _publicProperty = null;
                return false;
            }

            SelectWriterOnce("maxVisibleCharacters-public-property");
            return true;
        }
        catch
        {
            _publicProperty = null;
            return false;
        }
    }

    private static bool VerifyAppliedValue(TMP_Text text, int expected)
    {
        try
        {
            return text.maxVisibleCharacters == expected;
        }
        catch
        {
            return true;
        }
    }

    private static void SelectWriterOnce(string writer)
    {
        if (string.Equals(_selectedWriter, writer, StringComparison.Ordinal))
            return;

        _selectedWriter = writer;

        if (Settings.TypewriterDebugLogging.Value)
        {
            Plugin.Logger.LogInfo(
                "TMP proportional visibility writer selected: " +
                $"{writer}; meshRefresh=SetAllDirty+ForceMeshUpdate");
        }
    }
}

