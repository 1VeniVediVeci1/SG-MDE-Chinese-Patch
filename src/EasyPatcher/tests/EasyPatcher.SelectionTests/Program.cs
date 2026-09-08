using EasyPatcher;

static void Expect(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

string[] UiNames =
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

HashSet<string> Keys(bool translation, bool ui)
{
    var source = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
    {
        ["FONT.DDS"] = "font1",
        ["FONT2.DDS"] = "font2",
    };
    foreach (string name in UiNames)
    {
        source[name] = "ui:" + name;
    }
    source.Remove("OPTION.DDS");
    source["option.dds"] = "ui:case-insensitive";
    source["BACKLOG.DDS"] = "must-not-apply";
    source["PHONE_OKA.DDS"] = "must-not-apply";
    source["UNKNOWN.DDS"] = "must-not-apply";
    return PatchSelection.SelectFileEntries(source, translation, ui).Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
}

Expect(!PatchSelection.HasAnySelection(false, false), "Neither option must be rejected");
Expect(PatchSelection.HasAnySelection(true, false), "Translation-only must be valid");
Expect(PatchSelection.HasAnySelection(false, true), "UI-only must be valid");
Expect(PatchSelection.HasAnySelection(true, true), "Combined selection must be valid");

Expect(PatchSelection.ShouldApplyScx(true), "Translation must include SCX");
Expect(!PatchSelection.ShouldApplyScx(false), "UI-only must exclude SCX");

var expectedUi = UiNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
var expectedAll = expectedUi.Append("FONT.DDS").Append("FONT2.DDS").ToHashSet(StringComparer.OrdinalIgnoreCase);
Expect(Keys(true, false).SetEquals(new[] { "FONT.DDS", "FONT2.DDS" }),
    "Translation-only must select only both fonts");
Expect(Keys(false, true).SetEquals(expectedUi),
    "UI-only must select only explicitly classified UI resources");
Expect(Keys(true, true).SetEquals(expectedAll),
    "Combined selection must select fonts plus explicitly classified UI resources");
Expect(Keys(false, false).Count == 0, "Neither option must select no resources");

foreach (string name in UiNames)
{
    Expect(PatchSelection.IsUiResource(name), $"{name} must be classified as UI");
}
Expect(PatchSelection.IsUiResource("data01.dds"), "UI resource matching must be case-insensitive");
Expect(!PatchSelection.IsUiResource("BACKLOG.DDS"), "Backlog background must remain original");
Expect(!PatchSelection.IsUiResource("PHONE2_OKA.DDS"), "Phone UI atlas must remain original");
Expect(!PatchSelection.IsUiResource("FONT.DDS"), "Font atlas must remain a translation resource");

Console.WriteLine($"PATCH_SELECTION_TESTS=PASS UI_RESOURCES={UiNames.Length}");
