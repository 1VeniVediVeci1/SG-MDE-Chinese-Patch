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

string[] RuntimeFiles =
{
    "OP01.zh.ass", "prologue01.zh.ass", "fonts/NotoSansCJK-Regular.ttc",
    "lib/libass-5.dll", "lib/libbz2-1.dll", "lib/libexpat-1.dll",
    "lib/libfontconfig-1.dll", "lib/libfreetype-6.dll", "lib/libfribidi-0.dll",
    "lib/libgcc_s_dw2-1.dll", "lib/libglib-2.0-0.dll", "lib/libgraphite2.dll",
    "lib/libharfbuzz-0.dll", "lib/libiconv-2.dll", "lib/libintl-8.dll",
    "lib/libpcre-1.dll", "lib/libpng16-16.dll", "lib/libstdc++-6.dll",
    "lib/libwinpthread-1.dll", "lib/zlib1.dll",
};

void WritePayload(string payload, string proxyVersion)
{
    File.WriteAllText(Path.Combine(payload, "bink2w32.dll"), proxyVersion);
    foreach (string relative in RuntimeFiles)
    {
        string path = Path.Combine(payload, "runtime", relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "runtime:" + relative);
    }
}

string testRoot = Path.Combine(Path.GetTempPath(), "sgmde-video-runtime-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(testRoot);
try
{
    string payload = Path.Combine(testRoot, "payload");
    string game = Path.Combine(testRoot, "game");
    Directory.CreateDirectory(payload);
    Directory.CreateDirectory(game);
    Directory.CreateDirectory(Path.Combine(game, "USRDIR"));
    string original = "original-bink-decoder";
    VideoSubtitleRuntime.TestOriginalHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(original)));
    VideoSubtitleRuntime.TestOriginalLength = original.Length;
    File.WriteAllText(Path.Combine(game, "bink2w32.dll"), original);
    WritePayload(payload, "proxy-v1");

    VideoSubtitleRuntime.Install(game, payload);
    Expect(File.ReadAllText(Path.Combine(game, "bink2w32.dll")) == "proxy-v1", "First install must switch to the Bink proxy");
    Expect(File.ReadAllText(Path.Combine(game, "bink2w32_original.dll")) == original, "First install must preserve original decoder bytes");
    Expect(File.Exists(Path.Combine(game, "sgmde-video-subs", "installed.manifest")), "Install must record ownership");
    Expect(File.ReadAllText(Path.Combine(game, "sgmde-video-subs", "OP01.zh.ass")) == "runtime:OP01.zh.ass", "Install must copy OP subtitles");
    VideoSubtitleRuntime.Install(game, payload);
    Expect(File.ReadAllText(Path.Combine(game, "bink2w32.dll")) == "proxy-v1", "Reapply must accept its own Bink proxy");

    File.WriteAllText(Path.Combine(game, "sgmde-video-subs", "foreign.txt"), "must-survive");
    bool extraRejected = false;
    try { VideoSubtitleRuntime.Remove(game); }
    catch (InvalidOperationException) { extraRejected = true; }
    Expect(extraRejected, "Remove must reject an extra unowned runtime file");
    Expect(File.Exists(Path.Combine(game, "sgmde-video-subs", "foreign.txt")), "Extra runtime file must survive rejection");
    File.Delete(Path.Combine(game, "sgmde-video-subs", "foreign.txt"));

    string managedSubtitle = Path.Combine(game, "sgmde-video-subs", "OP01.zh.ass");
    File.WriteAllText(managedSubtitle, "tampered subtitle");
    bool changedManagedRejected = false;
    try { VideoSubtitleRuntime.Install(game, payload); }
    catch (InvalidOperationException) { changedManagedRejected = true; }
    Expect(changedManagedRejected, "Reapply must reject a changed managed file");
    Expect(File.ReadAllText(managedSubtitle) == "tampered subtitle", "Changed managed data must survive rejection");
    File.WriteAllText(managedSubtitle, "runtime:OP01.zh.ass");

    File.WriteAllText(Path.Combine(payload, "bink2w32.dll"), "proxy-v2");
    VideoSubtitleRuntime.TestCommitHook = step => { if (step == "after-old-proxy-move") throw new IOException("simulated Bink switch failure"); };
    bool rollbackObserved = false;
    try { VideoSubtitleRuntime.Install(game, payload); }
    catch (IOException) { rollbackObserved = true; }
    finally { VideoSubtitleRuntime.TestCommitHook = null; }
    Expect(rollbackObserved, "Install must report a forced proxy-switch failure");
    Expect(File.ReadAllText(Path.Combine(game, "bink2w32.dll")) == "proxy-v1", "Failed reapply must restore old proxy");
    Expect(File.ReadAllText(Path.Combine(game, "sgmde-video-subs", "OP01.zh.ass")) == "runtime:OP01.zh.ass", "Failed reapply must restore old runtime");
    Expect(File.ReadAllText(Path.Combine(game, "bink2w32_original.dll")) == original, "Rollback must retain original decoder backup");

    VideoSubtitleRuntime.Remove(game);
    Expect(File.ReadAllText(Path.Combine(game, "bink2w32.dll")) == original, "Remove must restore original decoder losslessly");
    Expect(File.ReadAllText(Path.Combine(game, "bink2w32_original.dll")) == original, "Remove must retain original recovery bytes");
    Expect(!Directory.Exists(Path.Combine(game, "sgmde-video-subs")), "Remove must delete the owned runtime directory");

    string collisionGame = Path.Combine(testRoot, "collision");
    Directory.CreateDirectory(Path.Combine(collisionGame, "USRDIR"));
    File.WriteAllText(Path.Combine(collisionGame, "bink2w32.dll"), "foreign-decoder");
    bool collisionRejected = false;
    try { VideoSubtitleRuntime.Install(collisionGame, payload); }
    catch (InvalidOperationException) { collisionRejected = true; }
    Expect(collisionRejected, "Install must refuse an unrelated Bink decoder");
    Expect(File.ReadAllText(Path.Combine(collisionGame, "bink2w32.dll")) == "foreign-decoder", "Collision refusal must not clobber decoder");

    string cleanupGame = Path.Combine(testRoot, "cleanup");
    Directory.CreateDirectory(Path.Combine(cleanupGame, "USRDIR"));
    File.WriteAllText(Path.Combine(cleanupGame, "bink2w32.dll"), original);
    VideoSubtitleRuntime.Install(cleanupGame, payload);
    File.WriteAllText(Path.Combine(payload, "bink2w32.dll"), "proxy-v3");
    VideoSubtitleRuntime.TestCommitHook = step => { if (step == "before-postcommit-cleanup") throw new IOException("simulated cleanup failure"); };
    VideoSubtitleRuntime.Install(cleanupGame, payload);
    VideoSubtitleRuntime.TestCommitHook = null;
    Expect(File.ReadAllText(Path.Combine(cleanupGame, "bink2w32.dll")) == "proxy-v3", "Cleanup failure must not roll back a committed reapply");
    Expect(Directory.GetDirectories(cleanupGame, "sgmde-video-subs.previous-*").Length == 1, "Cleanup failure must retain only generated previous runtime");
    Expect(Directory.GetFiles(cleanupGame, "bink2w32.dll.previous-*").Length == 1, "Cleanup failure must retain generated previous proxy");
}
finally
{
    VideoSubtitleRuntime.TestCommitHook = null;
    VideoSubtitleRuntime.TestOriginalHash = null;
    VideoSubtitleRuntime.TestOriginalLength = null;
    if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
}

Console.WriteLine($"PATCH_SELECTION_TESTS=PASS UI_RESOURCES={UiNames.Length}");
