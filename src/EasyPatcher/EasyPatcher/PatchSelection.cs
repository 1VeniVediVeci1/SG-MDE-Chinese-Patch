using System;
using System.Collections.Generic;

namespace EasyPatcher;

internal static class PatchSelection
{
    private static readonly HashSet<string> TranslationResourceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "FONT.DDS",
        "FONT2.DDS",
    };

    private static readonly HashSet<string> UiResourceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "ALBUM.DDS",
        "CLEARLIST.DDS",
        "DATA01.DDS",
        "GUIDE.DDS",
        "LOADMENU.DDS",
        "MAILLIST.DDS",
        "MOVMENU.DDS",
        "MUSIC.DDS",
        "OPTION.DDS",
        "PAGE1.PNG",
        "PAGE2.PNG",
        "QLOADMENU.DDS",
        "SAVEMENU.DDS",
        "TIPS.DDS",
        "TITLE_CHIP.DDS",
    };

    internal static bool HasAnySelection(bool translationSelected, bool uiSelected)
    {
        return translationSelected || uiSelected;
    }

    internal static bool ShouldApplyScx(bool translationSelected)
    {
        return translationSelected;
    }

    internal static bool IsUiResource(string resourceName)
    {
        return UiResourceNames.Contains(resourceName);
    }

    internal static Dictionary<string, object> SelectFileEntries(
        IDictionary<string, object> data,
        bool translationSelected,
        bool uiSelected)
    {
        var selected = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in data)
        {
            bool isUiResource = IsUiResource(entry.Key);
            bool isTranslationResource = TranslationResourceNames.Contains(entry.Key);
            if ((isUiResource && uiSelected) || (isTranslationResource && translationSelected))
            {
                selected.Add(entry.Key, entry.Value);
            }
        }
        return selected;
    }
}
