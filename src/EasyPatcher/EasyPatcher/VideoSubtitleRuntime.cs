#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace EasyPatcher;

internal static class VideoSubtitleRuntime
{
    private const string RuntimeDirectoryName = "sgmde-video-subs";
    private const string ManifestName = "installed.manifest";
    private const string ProxyName = "bink2w32.dll";
    private const string OriginalName = "bink2w32_original.dll";
    private const string OriginalHash = "ef8a089504d74a3014a86121b28318592a128f074520242e4a52d02004d67aa6";
    private const long OriginalLength = 293376;
    private static readonly string[] RuntimeFiles =
    {
        "OP01.zh.ass", "prologue01.zh.ass", "fonts/NotoSansCJK-Regular.ttc",
        "lib/libass-5.dll", "lib/libbz2-1.dll", "lib/libexpat-1.dll",
        "lib/libfontconfig-1.dll", "lib/libfreetype-6.dll", "lib/libfribidi-0.dll",
        "lib/libgcc_s_dw2-1.dll", "lib/libglib-2.0-0.dll", "lib/libgraphite2.dll",
        "lib/libharfbuzz-0.dll", "lib/libiconv-2.dll", "lib/libintl-8.dll",
        "lib/libpcre-1.dll", "lib/libpng16-16.dll", "lib/libstdc++-6.dll",
        "lib/libwinpthread-1.dll", "lib/zlib1.dll",
    };

    // Test hooks are internal so production callers cannot alter the supported decoder check.
    internal static Action<string> TestCommitHook = null;
    internal static string TestOriginalHash = null;
    internal static long? TestOriginalLength = null;

    internal static void EnsureInstallable(string gameRoot, string payloadRoot)
    {
        if (!Directory.Exists(Path.Combine(gameRoot, "USRDIR")))
            throw new InvalidOperationException("USRDIR 不存在, 请检查你的目录设置.");
        ValidatePayload(payloadRoot);

        string runtime = Path.Combine(gameRoot, RuntimeDirectoryName);
        string manifest = Path.Combine(runtime, ManifestName);
        if (File.Exists(manifest))
        {
            ValidateInstalled(gameRoot);
            return;
        }
        if (Directory.Exists(runtime))
            throw new InvalidOperationException("检测到未知视频字幕目录，拒绝覆盖.");

        string proxy = Path.Combine(gameRoot, ProxyName);
        if (!File.Exists(proxy) || IsReparsePoint(proxy) || !IsSupportedOriginal(proxy))
            throw new InvalidOperationException("未检测到受支持的原始 bink2w32.dll，拒绝覆盖.");

        string original = Path.Combine(gameRoot, OriginalName);
        if (File.Exists(original) && (IsReparsePoint(original) || !IsSupportedOriginal(original)))
            throw new InvalidOperationException("检测到未知 bink2w32_original.dll，拒绝覆盖.");
        if (Directory.Exists(original))
            throw new InvalidOperationException("bink2w32_original.dll 不是普通文件，拒绝覆盖.");
    }

    internal static void Install(string gameRoot, string payloadRoot)
    {
        EnsureInstallable(gameRoot, payloadRoot);
        string runtime = Path.Combine(gameRoot, RuntimeDirectoryName);
        bool replacing = File.Exists(Path.Combine(runtime, ManifestName));
        string token = Guid.NewGuid().ToString("N");
        string stagedRuntime = runtime + ".stage-" + token;
        string previousRuntime = runtime + ".previous-" + token;
        string proxy = Path.Combine(gameRoot, ProxyName);
        string stagedProxy = proxy + ".stage-" + token;
        string previousProxy = proxy + ".previous-" + token;
        string original = Path.Combine(gameRoot, OriginalName);
        bool runtimeMoved = false;
        bool runtimeActivated = false;
        bool proxyMoved = false;
        bool proxyActivated = false;
        bool committed = false;

        try
        {
            CopyRuntimeStage(Path.Combine(payloadRoot, "runtime"), stagedRuntime, Path.Combine(payloadRoot, ProxyName), original);
            File.Copy(Path.Combine(payloadRoot, ProxyName), stagedProxy, false);
            if (!File.Exists(original))
            {
                File.Copy(proxy, original, false);
                if (!IsSupportedOriginal(original)) throw new InvalidOperationException("原始 bink2w32.dll 备份校验失败.");
            }

            if (replacing)
            {
                Directory.Move(runtime, previousRuntime);
                runtimeMoved = true;
            }
            Directory.Move(stagedRuntime, runtime);
            runtimeActivated = true;
            TestCommitHook?.Invoke("before-proxy-switch");
            File.Move(proxy, previousProxy);
            proxyMoved = true;
            TestCommitHook?.Invoke("after-old-proxy-move");
            File.Move(stagedProxy, proxy);
            proxyActivated = true;
            committed = true;
        }
        catch
        {
            if (!committed) RollBackInstall(runtime, stagedRuntime, previousRuntime, proxy, stagedProxy, previousProxy,
                runtimeMoved, runtimeActivated, proxyMoved, proxyActivated);
            throw;
        }
        finally
        {
            if (!committed)
            {
                DeleteStageFile(stagedProxy);
                DeleteStageDirectory(stagedRuntime);
            }
        }

        // A new proxy is now live. Cleanup must never turn a successful reapply into an uninstall.
        try
        {
            TestCommitHook?.Invoke("before-postcommit-cleanup");
            if (runtimeMoved) RemoveOwnedRuntime(previousRuntime, ReadManifest(Path.Combine(previousRuntime, ManifestName)));
            if (proxyMoved && File.Exists(previousProxy)) File.Delete(previousProxy);
        }
        catch
        {
            // Retain only generated previous copies for a later inspection/retry.
        }
    }

    internal static void Remove(string gameRoot)
    {
        ValidateInstalled(gameRoot);
        string runtime = Path.Combine(gameRoot, RuntimeDirectoryName);
        string proxy = Path.Combine(gameRoot, ProxyName);
        string original = Path.Combine(gameRoot, OriginalName);
        string token = Guid.NewGuid().ToString("N");
        string previousRuntime = runtime + ".remove-" + token;
        string previousProxy = proxy + ".remove-" + token;
        string stagedOriginal = proxy + ".restore-" + token;
        bool runtimeMoved = false;
        bool proxyMoved = false;
        bool restored = false;
        bool committed = false;

        try
        {
            File.Copy(original, stagedOriginal, false);
            Directory.Move(runtime, previousRuntime);
            runtimeMoved = true;
            TestCommitHook?.Invoke("before-remove-proxy-switch");
            File.Move(proxy, previousProxy);
            proxyMoved = true;
            File.Move(stagedOriginal, proxy);
            restored = true;
            if (!IsSupportedOriginal(proxy)) throw new InvalidOperationException("原始 bink2w32.dll 恢复校验失败.");
            committed = true;
        }
        catch
        {
            if (!committed) RollBackRemove(runtime, previousRuntime, proxy, previousProxy, stagedOriginal,
                runtimeMoved, proxyMoved, restored);
            throw;
        }
        finally
        {
            if (!committed) DeleteStageFile(stagedOriginal);
        }

        try
        {
            TestCommitHook?.Invoke("before-remove-postcommit-cleanup");
            RemoveOwnedRuntime(previousRuntime, ReadManifest(Path.Combine(previousRuntime, ManifestName)));
            if (File.Exists(previousProxy)) File.Delete(previousProxy);
        }
        catch
        {
            // The original decoder is restored and its permanent backup remains intact.
        }
    }

    private static void ValidatePayload(string payloadRoot)
    {
        string proxy = Path.Combine(payloadRoot, ProxyName);
        if (!File.Exists(proxy) || IsReparsePoint(proxy)) throw new InvalidOperationException("外置视频字幕代理文件不完整.");
        string runtime = Path.Combine(payloadRoot, "runtime");
        if (!Directory.Exists(runtime) || IsReparsePoint(runtime)) throw new InvalidOperationException("外置视频字幕运行时文件不完整.");
        ValidateDirectoryContents(runtime, new HashSet<string>(RuntimeFiles, StringComparer.OrdinalIgnoreCase), false);
    }

    private static void ValidateInstalled(string gameRoot)
    {
        string runtime = Path.Combine(gameRoot, RuntimeDirectoryName);
        Dictionary<string, string> owned = ReadManifest(Path.Combine(runtime, ManifestName));
        if (!string.Equals(owned[OriginalName], ExpectedOriginalHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("视频字幕运行时原始解码器清单不匹配.");
        foreach (KeyValuePair<string, string> entry in owned)
        {
            string path = Path.Combine(gameRoot, entry.Key.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path) || IsReparsePoint(path) || !string.Equals(HashFile(path), entry.Value, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("视频字幕运行时文件已变更，拒绝覆盖或删除: " + entry.Key);
        }
        if (!IsSupportedOriginal(Path.Combine(gameRoot, OriginalName)))
            throw new InvalidOperationException("原始 bink2w32.dll 备份不受支持.");
        ValidateRuntimeTree(runtime, owned);
    }

    private static void CopyRuntimeStage(string source, string stage, string proxySource, string original)
    {
        try
        {
            Directory.CreateDirectory(stage);
            foreach (string relative in RuntimeFiles)
            {
                string sourcePath = Path.Combine(source, relative.Replace('/', Path.DirectorySeparatorChar));
                string targetPath = Path.Combine(stage, relative.Replace('/', Path.DirectorySeparatorChar));
                string parent = Path.GetDirectoryName(targetPath);
                if (parent == null) throw new InvalidOperationException("无效运行时路径.");
                Directory.CreateDirectory(parent);
                File.Copy(sourcePath, targetPath, false);
            }
            WriteManifest(Path.Combine(stage, ManifestName), CreateManifest(stage, proxySource, original));
        }
        catch
        {
            DeleteStageDirectory(stage);
            throw;
        }
    }

    private static Dictionary<string, string> CreateManifest(string runtimeDirectory, string proxySource, string original)
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ProxyName] = HashFile(proxySource),
            [OriginalName] = File.Exists(original) ? HashFile(original) : ExpectedOriginalHash,
        };
        foreach (string relative in RuntimeFiles)
            files.Add(RuntimeDirectoryName + "/" + relative,
                HashFile(Path.Combine(runtimeDirectory, relative.Replace('/', Path.DirectorySeparatorChar))));
        return files;
    }

    private static void WriteManifest(string path, Dictionary<string, string> files)
    {
        File.WriteAllLines(path, files.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => pair.Key + "|" + pair.Value));
    }

    private static Dictionary<string, string> ReadManifest(string path)
    {
        if (!File.Exists(path) || IsReparsePoint(path)) throw new InvalidOperationException("视频字幕运行时清单不存在或不安全.");
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in File.ReadAllLines(path))
        {
            int separator = line.IndexOf('|');
            if (separator <= 0 || separator != line.LastIndexOf('|') || separator == line.Length - 1)
                throw new InvalidOperationException("视频字幕运行时清单已损坏.");
            string relative = line.Substring(0, separator);
            string hash = line.Substring(separator + 1);
            if (!IsManifestPath(relative) || hash.Length != 64 || !hash.All(IsHex) || files.ContainsKey(relative))
                throw new InvalidOperationException("视频字幕运行时清单不安全.");
            files.Add(relative, hash);
        }
        var expected = new HashSet<string>(ExpectedManifestKeys(), StringComparer.OrdinalIgnoreCase);
        if (!expected.SetEquals(files.Keys)) throw new InvalidOperationException("视频字幕运行时清单不完整.");
        return files;
    }

    private static IEnumerable<string> ExpectedManifestKeys()
    {
        yield return ProxyName;
        yield return OriginalName;
        foreach (string relative in RuntimeFiles) yield return RuntimeDirectoryName + "/" + relative;
    }

    private static bool IsManifestPath(string path)
    {
        return path.IndexOf('\\') < 0 && ExpectedManifestKeys().Contains(path, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsHex(char value)
    {
        return (value >= '0' && value <= '9') || (value >= 'A' && value <= 'F') || (value >= 'a' && value <= 'f');
    }

    private static void ValidateRuntimeTree(string runtimeDirectory, Dictionary<string, string> owned)
    {
        if (IsReparsePoint(runtimeDirectory)) throw new InvalidOperationException("视频字幕运行时目录不安全.");
        var expectedFiles = new HashSet<string>(RuntimeFiles, StringComparer.OrdinalIgnoreCase) { ManifestName };
        ValidateDirectoryContents(runtimeDirectory, expectedFiles, true);
        foreach (string key in owned.Keys.Where(key => key.StartsWith(RuntimeDirectoryName + "/", StringComparison.OrdinalIgnoreCase)))
        {
            string relative = key.Substring(RuntimeDirectoryName.Length + 1);
            if (!expectedFiles.Contains(relative)) throw new InvalidOperationException("视频字幕目录清单不匹配.");
        }
    }

    private static void ValidateDirectoryContents(string directory, HashSet<string> expectedFiles, bool allowsManifest)
    {
        var actualFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var actualDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectDirectoryContents(directory, directory, actualFiles, actualDirectories);
        if (!actualFiles.SetEquals(expectedFiles)) throw new InvalidOperationException("视频字幕运行时缺少文件或含有未知文件.");
        var expectedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string file in expectedFiles)
        {
            if (!allowsManifest && string.Equals(file, ManifestName, StringComparison.OrdinalIgnoreCase)) continue;
            int separator = file.LastIndexOf('/');
            while (separator > 0)
            {
                expectedDirectories.Add(file.Substring(0, separator));
                separator = file.LastIndexOf('/', separator - 1);
            }
        }
        if (!actualDirectories.SetEquals(expectedDirectories)) throw new InvalidOperationException("视频字幕目录含有未知目录.");
    }

    private static void CollectDirectoryContents(string root, string directory, ISet<string> files, ISet<string> directories)
    {
        if (IsReparsePoint(directory)) throw new InvalidOperationException("视频字幕目录含有重解析点.");
        foreach (string file in Directory.GetFiles(directory))
        {
            if (IsReparsePoint(file)) throw new InvalidOperationException("视频字幕目录含有重解析点.");
            files.Add(NormalizeRelative(root, file));
        }
        foreach (string child in Directory.GetDirectories(directory))
        {
            if (IsReparsePoint(child)) throw new InvalidOperationException("视频字幕目录含有重解析点.");
            directories.Add(NormalizeRelative(root, child));
            CollectDirectoryContents(root, child, files, directories);
        }
    }

    private static void RemoveOwnedRuntime(string runtimeDirectory, Dictionary<string, string> owned)
    {
        ValidateRuntimeTree(runtimeDirectory, owned);
        foreach (string relative in RuntimeFiles)
        {
            string path = Path.Combine(runtimeDirectory, relative.Replace('/', Path.DirectorySeparatorChar));
            string key = RuntimeDirectoryName + "/" + relative;
            if (!File.Exists(path) || IsReparsePoint(path) ||
                !string.Equals(HashFile(path), owned[key], StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("视频字幕运行时文件已变更，拒绝删除: " + key);
            File.Delete(path);
        }
        File.Delete(Path.Combine(runtimeDirectory, ManifestName));
        foreach (string directory in Directory.GetDirectories(runtimeDirectory, "*", SearchOption.AllDirectories)
            .OrderByDescending(path => path.Length)) Directory.Delete(directory);
        Directory.Delete(runtimeDirectory);
    }

    private static void RollBackInstall(string runtime, string stagedRuntime, string previousRuntime, string proxy,
        string stagedProxy, string previousProxy, bool runtimeMoved, bool runtimeActivated, bool proxyMoved, bool proxyActivated)
    {
        if (proxyActivated && File.Exists(proxy)) File.Move(proxy, stagedProxy);
        if (proxyMoved && File.Exists(previousProxy)) File.Move(previousProxy, proxy);
        if (runtimeActivated && Directory.Exists(runtime)) Directory.Move(runtime, stagedRuntime);
        if (runtimeMoved && Directory.Exists(previousRuntime)) Directory.Move(previousRuntime, runtime);
    }

    private static void RollBackRemove(string runtime, string previousRuntime, string proxy, string previousProxy,
        string stagedOriginal, bool runtimeMoved, bool proxyMoved, bool restored)
    {
        if (restored && File.Exists(proxy)) File.Move(proxy, stagedOriginal);
        if (proxyMoved && File.Exists(previousProxy)) File.Move(previousProxy, proxy);
        if (runtimeMoved && Directory.Exists(previousRuntime)) Directory.Move(previousRuntime, runtime);
    }

    private static void DeleteStageFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static void DeleteStageDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }

    private static bool IsSupportedOriginal(string path)
    {
        return File.Exists(path) && new FileInfo(path).Length == ExpectedOriginalLength &&
            string.Equals(HashFile(path), ExpectedOriginalHash, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExpectedOriginalHash => TestOriginalHash ?? OriginalHash;
    private static long ExpectedOriginalLength => TestOriginalLength ?? OriginalLength;

    private static bool IsReparsePoint(string path)
    {
        return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    }

    private static string NormalizeRelative(string root, string path)
    {
        return path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Replace('\\', '/');
    }

    private static string HashFile(string path)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
    }
}
