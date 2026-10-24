using HUBTechUpdater;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

static class UpdaterChecks
{
    const string Repository = "TechSavage1989/SwitchPort_Diagram_Builder";
    const string Product = "HUBTechDiagramBuilder";
    const string Current = "1.12.0";
    const string Tag = "v1.12.0";
    const string PackageName = "HUBTech-Diagram-Builder-1.12.0-Windows-x64.zip";
    const string ExeName = "HUBTechDiagramBuilder.exe";
    const string Api = "https://api.github.com/repos/TechSavage1989/SwitchPort_Diagram_Builder/releases/latest";
    static readonly string ReleaseBase = "https://github.com/" + Repository + "/releases/download/" + Tag + "/";
    static readonly string Root = Path.Combine(Path.GetTempPath(), "Diagram updater checks " + Guid.NewGuid().ToString("N"));
    static readonly byte[] Payload = Encoding.UTF8.GetBytes("Deterministic test payload for ZIP and atomic file operations. This is not executed.");
    static int checks;

    static void Check(bool value, string detail)
    {
        checks++;
        if (!value) throw new Exception(detail);
    }
    static void Reject(Action action, string detail)
    {
        checks++;
        try { action(); }
        catch { return; }
        throw new Exception("Expected rejection: " + detail);
    }
    static async Task RejectAsync(Func<Task> action, string detail)
    {
        checks++;
        try { await action(); }
        catch { return; }
        throw new Exception("Expected rejection: " + detail);
    }
    static string Sha(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    static string ShaFile(string path)
    {
        using var source = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant();
    }
    static byte[] Bytes(JsonObject json) => Encoding.UTF8.GetBytes(json.ToJsonString());
    static JsonObject Manifest()
    {
        return new JsonObject
        {
            ["schema_version"] = 1,
            ["product"] = Product,
            ["version"] = Current,
            ["architecture"] = "x64",
            ["package"] = new JsonObject
            {
                ["name"] = PackageName,
                ["url"] = ReleaseBase + PackageName,
                ["size"] = 2000,
                ["sha256"] = new string('a', 64)
            },
            ["executable"] = new JsonObject
            {
                ["path"] = ExeName,
                ["size"] = Payload.Length,
                ["sha256"] = Sha(Payload)
            }
        };
    }
    static JsonObject Asset(string name, int size, int id)
    {
        return new JsonObject
        {
            ["id"] = id,
            ["name"] = name,
            ["size"] = size,
            ["state"] = "uploaded",
            ["url"] = "https://api.github.com/repos/" + Repository + "/releases/assets/" + id,
            ["browser_download_url"] = ReleaseBase + name
        };
    }
    static JsonObject Release(byte[] manifest)
    {
        return new JsonObject
        {
            ["tag_name"] = Tag,
            ["name"] = "Diagram Builder " + Current,
            ["draft"] = false,
            ["prerelease"] = false,
            ["html_url"] = "https://github.com/" + Repository + "/releases/tag/" + Tag,
            ["body"] = "A tested stable release.",
            ["published_at"] = "2026-10-24T00:00:00Z",
            ["assets"] = new JsonArray(Asset("update-manifest.json", manifest.Length, 101), Asset(PackageName, 2000, 102))
        };
    }
    static void ManifestChecks()
    {
        UpdateManifest.Parse(Bytes(Manifest()), Tag);
        checks++;
        var mutations = new List<(string Name, Action<JsonObject> Change)>
        {
            ("future schema", m => m["schema_version"] = 2),
            ("wrong product", m => m["product"] = "AnotherApplication"),
            ("wrong architecture", m => m["architecture"] = "arm64"),
            ("prerelease version", m => m["version"] = "1.12.0-beta.1"),
            ("malformed version", m => m["version"] = "latest"),
            ("missing version", m => m.Remove("version")),
            ("negative package length", m => m["package"]!["size"] = -1),
            ("unbounded package length", m => m["package"]!["size"] = long.MaxValue),
            ("malformed package digest", m => m["package"]!["sha256"] = "abc"),
            ("nonhex package digest", m => m["package"]!["sha256"] = new string('z', 64)),
            ("wrong asset name", m => m["package"]!["name"] = "unrelated.zip"),
            ("different repository", m => m["package"]!["url"] = "https://github.com/Other/Project/releases/download/" + Tag + "/" + PackageName),
            ("different download host", m => m["package"]!["url"] = "https://example.com/" + PackageName),
            ("HTTP transport", m => m["package"]!["url"] = (ReleaseBase + PackageName).Replace("https:", "http:")),
            ("credential URL", m => m["package"]!["url"] = (ReleaseBase + PackageName).Replace("https://", "https://person:secret@")),
            ("query on artifact URL", m => m["package"]!["url"] = ReleaseBase + PackageName + "?redirect=elsewhere"),
            ("fragment on artifact URL", m => m["package"]!["url"] = ReleaseBase + PackageName + "#part"),
            ("wrong release tag in URL", m => m["package"]!["url"] = (ReleaseBase + PackageName).Replace(Tag + "/", "v1.11.0/")),
            ("wrong executable path", m => m["executable"]!["path"] = "other.exe"),
            ("executable traversal", m => m["executable"]!["path"] = "../" + ExeName),
            ("unbounded executable length", m => m["executable"]!["size"] = long.MaxValue),
            ("missing executable digest", m => m["executable"]!.AsObject().Remove("sha256"))
        };
        foreach (var item in mutations)
        {
            var manifest = Manifest();
            item.Change(manifest);
            Reject(() => UpdateManifest.Parse(Bytes(manifest), Tag), item.Name);
        }
        Reject(() => UpdateManifest.Parse(Bytes(Manifest()), "v1.13.0"), "release tag/version mismatch");
        Reject(() => UpdateManifest.Parse(Encoding.UTF8.GetBytes("not JSON"), Tag), "invalid JSON");
        Console.WriteLine("PASS manifest product, version, transport, repository, size, digest and filename constraints.");
    }

    sealed class FakeHandler : HttpMessageHandler
    {
        readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response;
        public readonly List<Uri> Requests = new();
        public FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) { this.response = response; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request.RequestUri!);
            var result = await response(request, cancellationToken);
            result.RequestMessage ??= request;
            return result;
        }
    }
    static HttpResponseMessage Response(byte[] value, HttpStatusCode code = HttpStatusCode.OK)
        => new(code) { Content = new ByteArrayContent(value) };
    static FakeHandler Feed(JsonObject release, byte[] manifest)
        => new((request, _) => Task.FromResult(Response(request.RequestUri!.AbsoluteUri == Api ? Bytes(release) : manifest)));

    static async Task NetworkChecks()
    {
        byte[] manifest = Bytes(Manifest());
        var valid = Release(manifest);
        using (var handler = Feed(valid, manifest))
        using (var http = new HttpClient(handler))
        {
            var offer = await new UpdateService(http).CheckAsync(new Version(1, 11, 3));
            Check(offer != null && offer.Version == new Version(1, 12, 0), "Stable newer version must be offered.");
            Check(offer!.PackageUrl.AbsoluteUri == ReleaseBase + PackageName, "Offer must retain verified repository asset URL.");
            Check(offer.PackageSize == 2000 && offer.ExecutableSize == Payload.Length, "Offer must retain bounded manifest sizes.");
        }
        foreach (var version in new[] { new Version(1, 12, 0), new Version(1, 12, 0, 0), new Version(1, 13, 0) })
        {
            using var handler = Feed(valid, manifest);
            using var http = new HttpClient(handler);
            Check(await new UpdateService(http).CheckAsync(version) == null, "Equal and older releases must not be offered.");
        }
        foreach (string flag in new[] { "draft", "prerelease" })
        {
            var release = Release(manifest); release[flag] = true;
            using var handler = Feed(release, manifest);
            using var http = new HttpClient(handler);
            Check(await new UpdateService(http).CheckAsync(new Version(1, 11, 3)) == null, flag + " must not become a normal update.");
            Check(handler.Requests.Count == 1, flag + " must not download its manifest.");
        }
        using (var handler = new FakeHandler((_, _) => Task.FromResult(Response(Array.Empty<byte>(), HttpStatusCode.NotFound))))
        using (var http = new HttpClient(handler))
            Check(await new UpdateService(http).CheckAsync(new Version(1, 11, 3)) == null, "An empty release feed is a normal no-update state.");
        using (var handler = new FakeHandler((_, _) => Task.FromResult(Response(Array.Empty<byte>(), HttpStatusCode.Forbidden))))
        using (var http = new HttpClient(handler))
            await RejectAsync(async () => await new UpdateService(http).CheckAsync(new Version(1, 11, 3)), "rate-limit/failure response");

        var attacks = new List<(string Name, Action<JsonObject> Change)>
        {
            ("untrusted manifest URL", release => release["assets"]![0]!["browser_download_url"] = "https://example.com/update-manifest.json"),
            ("wrong manifest repository", release => release["assets"]![0]!["browser_download_url"] = "https://github.com/Other/Project/releases/download/" + Tag + "/update-manifest.json"),
            ("untrusted package URL", release => release["assets"]![1]!["browser_download_url"] = "https://example.com/" + PackageName),
            ("duplicate manifest asset", release => release["assets"]!.AsArray().Add(Asset("update-manifest.json", manifest.Length, 104))),
            ("mismatched package size", release => release["assets"]![1]!["size"] = 9999),
            ("unbounded manifest size", release => release["assets"]![0]!["size"] = long.MaxValue),
            ("untrusted release URL", release => release["html_url"] = "https://example.com/release")
        };
        foreach (var attack in attacks)
        {
            var release = Release(manifest); attack.Change(release);
            using var handler = Feed(release, manifest);
            using var http = new HttpClient(handler);
            await RejectAsync(async () => await new UpdateService(http).CheckAsync(new Version(1, 11, 3)), attack.Name);
            Check(handler.Requests.All(uri => uri.Host != "example.com"), "Reject untrusted URLs before making requests.");
        }
        using (var cancellation = new CancellationTokenSource())
        using (var handler = Feed(valid, manifest))
        using (var http = new HttpClient(handler))
        {
            cancellation.Cancel();
            await RejectAsync(async () => await new UpdateService(http).CheckAsync(new Version(1, 11, 3), cancellation.Token), "cancelled check");
        }
        using (var handler = new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri("https://example.com/redirect") } })))
        using (var http = new HttpClient(handler))
        {
            await RejectAsync(async () => await new UpdateService(http).CheckAsync(new Version(1, 11, 3)), "untrusted redirect");
            Check(handler.Requests.Count == 1, "Untrusted redirect must not be followed.");
        }
        Console.WriteLine("PASS stable-only discovery, release/asset agreement, rate-limit, cancellation and hostile URL handling.");
    }

    static string MakeZip(List<(string Name, byte[] Data, int? Attributes)> entries)
    {
        string path = Path.Combine(Root, Guid.NewGuid().ToString("N") + ".zip");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var item in entries)
        {
            var entry = zip.CreateEntry(item.Name, CompressionLevel.Optimal);
            if (item.Attributes.HasValue) entry.ExternalAttributes = item.Attributes.Value;
            using var stream = entry.Open(); stream.Write(item.Data);
        }
        return path;
    }
    static List<(string Name, byte[] Data, int? Attributes)> Entries()
        => new()
        {
            (ExeName, Payload, null),
            ("READ_ME.txt", Encoding.UTF8.GetBytes("Portable application test fixture."), null),
            ("SHA256SUMS.txt", Encoding.UTF8.GetBytes(Sha(Payload) + "  " + ExeName + "\n"), null),
            ("UPDATE_INFO.txt", Encoding.UTF8.GetBytes("This package is not executed by the test."), null)
        };
    static UpdateOffer Offer(string zipPath)
        => new(new Version(1, 12, 0), "Notes", new Uri("https://github.com/" + Repository + "/releases/tag/" + Tag),
               new Uri(ReleaseBase + PackageName), PackageName, new FileInfo(zipPath).Length,
               ShaFile(zipPath), Payload.Length, Sha(Payload));
    static string StageRoot() => Path.Combine(Root, "staged " + Guid.NewGuid().ToString("N"));
    sealed class StallingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
    sealed class NonSeekableReadStream : Stream
    {
        readonly MemoryStream source;
        public NonSeekableReadStream(byte[] bytes) { source = new MemoryStream(bytes, writable: false); }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => source.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => source.ReadAsync(buffer, cancellationToken);
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) source.Dispose(); base.Dispose(disposing); }
    }
    static HttpResponseMessage UnknownLengthResponse(byte[] bytes)
        => new(HttpStatusCode.OK) { Content = new StreamContent(new NonSeekableReadStream(bytes)) };
    static async Task DownloadFailureChecks()
    {
        string zip = MakeZip(Entries()); var offer = Offer(zip); byte[] bytes = File.ReadAllBytes(zip);
        using (var handler = new FakeHandler((_, _) => Task.FromResult(Response(bytes))))
        using (var http = new HttpClient(handler))
        {
            string stage = StageRoot();
            await RejectAsync(async () => await new UpdateService(http).DownloadAsync(offer with { PackageSha256 = new string('0', 64) }, stage), "streamed outer hash mismatch");
            Check(!Directory.EnumerateFileSystemEntries(stage).Any(), "Rejected download must leave no staged payload behind.");
        }
        using (var handler = new FakeHandler((_, _) => Task.FromResult(UnknownLengthResponse(bytes.Concat(new byte[] { 1 }).ToArray()))))
        using (var http = new HttpClient(handler))
        {
            string stage = StageRoot();
            await RejectAsync(async () => await new UpdateService(http).DownloadAsync(offer, stage), "streamed unknown-length overflow");
            Check(!Directory.EnumerateFileSystemEntries(stage).Any(), "Oversized download must be discarded.");
        }
        using (var handler = new FakeHandler((_, _) => Task.FromResult(UnknownLengthResponse(bytes[..^1]))))
        using (var http = new HttpClient(handler))
        {
            string stage = StageRoot();
            await RejectAsync(async () => await new UpdateService(http).DownloadAsync(offer, stage), "incomplete HTTP download");
            Check(!Directory.EnumerateFileSystemEntries(stage).Any(), "Incomplete download must be discarded.");
        }
        using (var handler = new FakeHandler((_, _) => Task.FromResult(Response(bytes))))
        using (var http = new HttpClient(handler))
        {
            await RejectAsync(async () => await new UpdateService(http).DownloadAsync(offer with { PackageUrl = new Uri("https://example.com/attack.zip") }, StageRoot()), "untrusted offer URL");
            Check(handler.Requests.Count == 0, "A forged download offer must be rejected before network access.");
        }
        using (var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50)))
        using (var handler = new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new StallingStream()) })))
        using (var http = new HttpClient(handler))
        {
            string stage = StageRoot(); bool cancelled = false;
            try { await new UpdateService(http).DownloadAsync(offer, stage, cancellationToken: cancellation.Token); }
            catch (OperationCanceledException) { cancelled = true; }
            Check(cancelled, "Cancellation during a stalled payload read must propagate promptly.");
            Check(!Directory.EnumerateFileSystemEntries(stage).Any(), "Cancellation must discard the current download directory.");
        }
        Console.WriteLine("PASS streamed download bounds, hash/incomplete rejection, cancellation and staging cleanup.");
    }
    static void ZipChecks()
    {
        string valid = MakeZip(Entries()); var offer = Offer(valid);
        string staged = PackageValidator.Stage(valid, offer, StageRoot(), validateExecutable: false);
        Check(File.Exists(staged) && File.ReadAllBytes(staged).SequenceEqual(Payload), "Staging must retain exact executable bytes.");
        Reject(() => PackageValidator.Stage(valid, offer with { PackageSha256 = new string('0', 64) }, StageRoot(), false), "outer package hash mismatch");
        Reject(() => PackageValidator.Stage(valid, offer with { ExecutableSha256 = new string('0', 64) }, StageRoot(), false), "inner executable hash mismatch");
        Reject(() => PackageValidator.Stage(valid, offer with { PackageSize = offer.PackageSize + 1 }, StageRoot(), false), "package length mismatch");
        Reject(() => PackageValidator.Stage(valid, offer with { ExecutableSize = offer.ExecutableSize + 1 }, StageRoot(), false), "executable length mismatch");
        var attacks = new List<(string Name, Action<List<(string Name, byte[] Data, int? Attributes)>> Change)>
        {
            ("parent traversal", e => e[1] = ("../outside.exe", Payload, null)),
            ("absolute path", e => e[1] = ("/outside.exe", Payload, null)),
            ("Windows traversal", e => e[1] = ("..\\outside.exe", Payload, null)),
            ("Windows drive", e => e[1] = ("C:\\outside.exe", Payload, null)),
            ("directory member", e => e[1] = ("directory/", Array.Empty<byte>(), null)),
            ("unexpected file", e => e[1] = ("install.ps1", Payload, null)),
            ("duplicate executable", e => e[1] = (ExeName, Payload, null)),
            ("case duplicate executable", e => e[1] = (ExeName.ToLowerInvariant(), Payload, null)),
            ("missing executable", e => e.RemoveAt(0)),
            ("symbolic link", e => e[0] = (ExeName, Payload, unchecked((int)0xA1FF0000))),
            ("large/overcompressed documentation", e => e[1] = ("READ_ME.txt", new byte[5 * 1024 * 1024], null))
        };
        foreach (var attack in attacks)
        {
            var entries = Entries(); attack.Change(entries);
            string path = MakeZip(entries);
            Reject(() => PackageValidator.Stage(path, Offer(path), StageRoot(), false), attack.Name);
            Check(!File.Exists(Path.Combine(Root, "outside.exe")), "ZIP input must never create a file outside its stage.");
        }
        string truncated = Path.Combine(Root, "truncated.zip"); File.WriteAllBytes(truncated, File.ReadAllBytes(valid)[..25]);
        Reject(() => PackageValidator.Stage(truncated, Offer(truncated), StageRoot(), false), "truncated ZIP");
        string notExecutable = Path.Combine(Root, "invalid.exe"); File.WriteAllBytes(notExecutable, Payload);
        Reject(() => ExecutableValidator.Validate(notExecutable, new Version(1, 12, 0), Sha(Payload), Payload.Length), "non-PE executable");
        Console.WriteLine("PASS ZIP/EXE integrity, bounded extraction, flat allowlist, duplicate/path/symlink and malformed-container rejection.");
    }

    static void TransactionChecks()
    {
        byte[] previous = Encoding.UTF8.GetBytes("Previous working application bytes."), replacement = Encoding.UTF8.GetBytes("Verified replacement application bytes.");
        string folder = Path.Combine(Root, "atomic transaction"); Directory.CreateDirectory(folder);
        string target = Path.Combine(folder, "app.exe"), staged = Path.Combine(folder, "stage.exe"), backup = Path.Combine(folder, "rollback", "previous.exe");
        File.WriteAllBytes(target, previous); File.WriteAllBytes(staged, replacement);
        var apply = FileInstallTransaction.Begin(staged, target, backup, Sha(replacement), Sha(previous));
        Check(File.ReadAllBytes(target).SequenceEqual(replacement), "Replacement must install exact staged bytes.");
        Check(File.ReadAllBytes(backup).SequenceEqual(previous), "Previous working executable must remain available.");
        apply.Rollback();
        Check(File.ReadAllBytes(target).SequenceEqual(previous), "Failed startup rollback must restore exact previous bytes.");
        Check(File.Exists(staged), "Staged source is retained independently of the target transaction.");
        apply = FileInstallTransaction.Begin(staged, target, backup, Sha(replacement));
        apply.Commit();
        Check(File.ReadAllBytes(target).SequenceEqual(replacement) && File.ReadAllBytes(backup).SequenceEqual(previous), "Commit retains the new target and one previous rollback copy.");

        File.WriteAllBytes(target, previous);
        byte[] existingBackup = File.ReadAllBytes(backup);
        Reject(() => FileInstallTransaction.Begin(staged, target, backup, Sha(replacement), new string('0', 64)), "original application changed since install authorization");
        Check(File.ReadAllBytes(target).SequenceEqual(previous) && File.ReadAllBytes(backup).SequenceEqual(existingBackup), "Rejected original digest must preserve current application and existing rollback bytes.");
        Reject(() => FileInstallTransaction.Begin(staged, target, backup, new string('0', 64)), "unverified replacement payload");
        Check(File.ReadAllBytes(target).SequenceEqual(previous), "Rejected replacement leaves current application intact.");
        Reject(() => FileInstallTransaction.Begin(staged, target, target, Sha(replacement)), "backup cannot be target");
        Check(File.ReadAllBytes(target).SequenceEqual(previous), "Aliased paths cannot damage target.");
        Reject(() => FileInstallTransaction.Begin(staged, staged, backup, Sha(replacement)), "staged file cannot be target");
        Reject(() => FileInstallTransaction.Begin(staged, target, staged, Sha(replacement)), "backup cannot be staged file");
        string badBackup = Path.Combine(folder, "directory-not-file"); Directory.CreateDirectory(badBackup);
        Reject(() => FileInstallTransaction.Begin(staged, target, badBackup, Sha(replacement)), "backup write failure");
        Check(File.ReadAllBytes(target).SequenceEqual(previous), "Failed backup must not replace current app.");
        apply = FileInstallTransaction.Begin(staged, target, backup, Sha(replacement));
        File.WriteAllBytes(backup, Encoding.UTF8.GetBytes("damaged rollback copy"));
        Reject(() => apply.Rollback(), "corrupt rollback copy");
        Check(File.ReadAllBytes(target).SequenceEqual(replacement), "Invalid rollback must not overwrite the installed application.");
        Console.WriteLine("PASS atomic replacement, previous-copy retention, verified rollback and failure-path preservation.");
    }

    static ushort U16(byte[] data, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2));
    static uint U32(byte[] data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));
    static void Put16(byte[] data, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset, 2), value);
    static void Put32(byte[] data, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset, 4), value);
    static int Aligned(int offset) => checked((offset + 3) & ~3);

    // This fixture locator follows the real PE section table and RT_VERSION tree.
    // It does not search the single-file bundle for similar strings in embedded DLLs.
    sealed record NativeResourceFixture(int Pe, int Optional, int ResourceDirectory, int ResourceRoot,
        int VersionEntry, int VersionBlob, int VersionBlobSize, int FixedInfo, int ProductValue);
    static NativeResourceFixture LocateNativeResource(byte[] data)
    {
        int pe = checked((int)U32(data, 0x3c)), optional = pe + 24;
        int sections = U16(data, pe + 6), sectionTable = optional + U16(data, pe + 20);
        int directory = optional + 112 + 2 * 8;
        uint resourceRva = U32(data, directory);
        int Map(uint rva, uint length)
        {
            for (int i = 0; i < sections; i++)
            {
                int entry = sectionTable + i * 40;
                uint address = U32(data, entry + 12), rawSize = U32(data, entry + 16), rawOffset = U32(data, entry + 20);
                if (rva >= address && (ulong)rva + length <= (ulong)address + rawSize)
                    return checked((int)((ulong)rawOffset + rva - address));
            }
            throw new Exception("The real-release resource fixture has no mapped PE section.");
        }
        int root = Map(resourceRva, U32(data, directory + 4));
        IEnumerable<(uint Name, uint Value)> Entries(int relative)
        {
            int location = checked(root + relative);
            int count = checked(U16(data, location + 12) + U16(data, location + 14));
            for (int i = 0; i < count; i++) yield return (U32(data, location + 16 + i * 8), U32(data, location + 20 + i * 8));
        }
        var versionType = Entries(0).Single(entry => entry.Name == 16);
        Check((versionType.Value & 0x80000000) != 0, "The real PE RT_VERSION entry must be a resource directory.");
        int FindLeaf(int relative, int depth)
        {
            if (depth > 4) throw new Exception("Unexpected real PE RT_VERSION depth.");
            foreach (var entry in Entries(relative))
            {
                if ((entry.Value & 0x80000000) != 0) return FindLeaf(checked((int)(entry.Value & 0x7fffffff)), depth + 1);
                return checked(root + (int)entry.Value);
            }
            throw new Exception("The real PE RT_VERSION directory is empty.");
        }
        int leaf = FindLeaf(checked((int)(versionType.Value & 0x7fffffff)), 0);
        int blobSize = checked((int)U32(data, leaf + 4)), blob = Map(U32(data, leaf), (uint)blobSize);
        int StringEnd(int start, int end)
        {
            int at = start;
            while (at + 2 <= end && U16(data, at) != 0) at += 2;
            if (at + 2 > end) throw new Exception("Malformed key in the real PE fixture.");
            return at;
        }
        int rootKeyEnd = StringEnd(blob + 6, blob + blobSize), fixedInfo = Aligned(rootKeyEnd + 2);
        Check(Encoding.Unicode.GetString(data, blob + 6, rootKeyEnd - blob - 6) == "VS_VERSION_INFO" && U32(data, fixedInfo) == 0xfeef04bd,
            "Fixture mutations must target the mapped native VS_VERSION_INFO fixed block.");
        int FindValue(int start, int parentEnd, string wanted)
        {
            int length = U16(data, start), end = checked(start + length);
            if (length < 6 || end > parentEnd) throw new Exception("Malformed real PE resource block.");
            int keyEnd = StringEnd(start + 6, end), value = Aligned(keyEnd + 2);
            string key = Encoding.Unicode.GetString(data, start + 6, keyEnd - start - 6);
            if (key == wanted) return value;
            int valueBytes = checked(U16(data, start + 2) * (U16(data, start + 4) == 1 ? 2 : 1));
            for (int child = Aligned(value + valueBytes); child + 6 <= end;)
            {
                int childLength = U16(data, child);
                if (childLength == 0) break;
                int found = FindValue(child, end, wanted);
                if (found >= 0) return found;
                child = Aligned(checked(child + childLength));
            }
            return -1;
        }
        int productValue = FindValue(blob, blob + blobSize, "ProductName");
        Check(productValue >= 0 && Encoding.Unicode.GetString(data, productValue, Product.Length * 2) == Product,
            "Fixture must locate ProductName in the native version resource, not bundled assembly bytes.");
        return new(pe, optional, directory, root, leaf, blob, blobSize, fixedInfo, productValue);
    }
    static void NativeResourceRejections(string executable, UpdateOffer offer, Version version)
    {
        byte[] original = File.ReadAllBytes(executable);
        var fixture = LocateNativeResource(original);
        string mutated = Path.Combine(Path.GetDirectoryName(executable)!, "mutated-native.exe");
        void RejectMutation(string name, Action<byte[]> change)
        {
            byte[] altered = (byte[])original.Clone(); change(altered);
            File.WriteAllBytes(mutated, altered);
            // Recompute the expected digest: these cases must reach metadata guards.
            string changedDigest = Sha(altered);
            Check(changedDigest != offer.ExecutableSha256, name + " fixture must change actual bytes.");
            Reject(() => ExecutableValidator.Validate(mutated, version, changedDigest, altered.Length), name);
        }
        Reject(() => ExecutableValidator.Validate(executable, new Version(version.Major, version.Minor, version.Build + 1), offer.ExecutableSha256, offer.ExecutableSize),
            "native file version differs from the expected release version");
        RejectMutation("non-x64 native machine", data => Put16(data, fixture.Pe + 4, 0x14c));
        RejectMutation("console instead of GUI subsystem", data => Put16(data, fixture.Optional + 68, 3));
        RejectMutation("unmapped PE resource directory RVA", data => Put32(data, fixture.ResourceDirectory, 0xfffffff0));
        RejectMutation("unmapped version-data pointer", data => Put32(data, fixture.VersionEntry, 0xfffffff0));
        RejectMutation("truncated native version-data boundary", data => Put32(data, fixture.VersionEntry + 4, 52));
        RejectMutation("native ProductName belongs to a different app", data => Put16(data, fixture.ProductValue, 'X'));
        RejectMutation("native fixed file-version revision differs", data => Put32(data, fixture.FixedInfo + 12, U32(data, fixture.FixedInfo + 12) ^ 1));
        if (!OperatingSystem.IsWindows())
        {
            // Windows' own resource APIs may ignore the optional-header size field;
            // the bounded cross-platform decoder must enforce it explicitly.
            RejectMutation("native fixed-version signature", data => Put32(data, fixture.FixedInfo, 0));
            RejectMutation("unbounded resource-directory declared size", data => Put32(data, fixture.ResourceDirectory + 4, uint.MaxValue));
            RejectMutation("resource-directory declared boundary excludes its tree", data => Put32(data, fixture.ResourceDirectory + 4, 16));
            RejectMutation("unbounded version-data declared size", data => Put32(data, fixture.VersionEntry + 4, uint.MaxValue));
        }
        File.Delete(mutated);
        Console.WriteLine("PASS real native resource mapping, malformed RVA/size/boundary/pointer rejection, product/version and x64 GUI guards.");
    }

    static async Task RealRelease(string directory)
    {
        string full = Path.GetFullPath(directory);
        byte[] manifestBytes = File.ReadAllBytes(Path.Combine(full, "update-manifest.json"));
        var json = JsonNode.Parse(manifestBytes)!.AsObject();
        string versionText = json["version"]!.GetValue<string>();
        Version version = Version.Parse(versionText);
        UpdateManifest.Parse(manifestBytes, "v" + versionText); checks++;
        var package = json["package"]!.AsObject(); var executable = json["executable"]!.AsObject();
        string zip = Path.Combine(full, package["name"]!.GetValue<string>());
        var offer = new UpdateOffer(version, "Actual release validation", new Uri("https://github.com/" + Repository + "/releases/tag/v" + versionText),
            new Uri(package["url"]!.GetValue<string>()), package["name"]!.GetValue<string>(), package["size"]!.GetValue<long>(), package["sha256"]!.GetValue<string>(),
            executable["size"]!.GetValue<long>(), executable["sha256"]!.GetValue<string>());
        string staged = PackageValidator.Stage(zip, offer, StageRoot(), validateExecutable: true);
        ExecutableValidator.Validate(staged, version, offer.ExecutableSha256, offer.ExecutableSize);
        Check(ShaFile(staged) == offer.ExecutableSha256, "Actual release executable must match manifest digest after extraction.");
        NativeResourceRejections(staged, offer, version);
        var release = new JsonObject
        {
            ["tag_name"] = "v" + versionText, ["draft"] = false, ["prerelease"] = false,
            ["html_url"] = offer.ReleaseUrl.AbsoluteUri, ["body"] = "Actual stable release",
            ["assets"] = new JsonArray(
                new JsonObject { ["name"] = "update-manifest.json", ["size"] = manifestBytes.Length, ["browser_download_url"] = offer.ReleaseUrl.AbsoluteUri.Replace("/tag/", "/download/") + "/update-manifest.json" },
                new JsonObject { ["name"] = offer.PackageName, ["size"] = offer.PackageSize, ["browser_download_url"] = offer.PackageUrl.AbsoluteUri })
        };
        using (var handler = new FakeHandler((request, _) => Task.FromResult(request.RequestUri!.AbsoluteUri == Api ? Response(Bytes(release)) :
            request.RequestUri.AbsolutePath.EndsWith("/update-manifest.json", StringComparison.Ordinal) ? Response(manifestBytes) :
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead(zip)) })))
        using (var http = new HttpClient(handler))
        {
            var service = new UpdateService(http);
            var discovered = await service.CheckAsync(new Version(0, 0, 0));
            Check(discovered != null && discovered.Version == version, "The real release must be discoverable through the release protocol.");
            var downloaded = await service.DownloadAsync(discovered!, StageRoot());
            Check(ShaFile(downloaded.ExecutablePath) == offer.ExecutableSha256, "HTTP streaming, manifest/ZIP verification and real EXE staging must agree end to end.");
        }
        using (var output = new FileStream(staged, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            output.Position = output.Length / 2; int original = output.ReadByte(); output.Position--; output.WriteByte((byte)(original ^ 1));
        }
        Reject(() => ExecutableValidator.Validate(staged, version, offer.ExecutableSha256, offer.ExecutableSize), "tampered actual release executable");
        Console.WriteLine("PASS real release ZIP, manifest, native x64 GUI executable/product/version and tamper rejection.");
    }

    static async Task Main(string[] args)
    {
        Directory.CreateDirectory(Root);
        try
        {
            ManifestChecks();
            await NetworkChecks();
            ZipChecks();
            await DownloadFailureChecks();
            TransactionChecks();
            if (args.Length == 2 && args[0] == "--release-dir") await RealRelease(args[1]);
            else if (args.Length != 0) throw new ArgumentException("Usage: UpdaterChecks [--release-dir <packaged release directory>]");
            Console.WriteLine("PASS " + checks + " updater protocol, integrity, extraction, transaction and release assertions.");
        }
        finally
        {
            try { Directory.Delete(Root, true); } catch { }
        }
    }
}
