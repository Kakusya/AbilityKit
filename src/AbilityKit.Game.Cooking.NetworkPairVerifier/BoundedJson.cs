using System.Security.Cryptography;
using System.Text.Json;
namespace AbilityKit.Game.Cooking.NetworkPairVerifier;

internal static class BoundedJson
{
    public const long FileLimit = 128L * 1024 * 1024;
    private const int TokenBytes = 8 * 1024 * 1024;
    public static void SafePath(string path) {
        for (FileSystemInfo? entry = new FileInfo(Path.GetFullPath(path)); entry is not null; entry = entry is FileInfo file ? file.Directory : ((DirectoryInfo)entry).Parent) Check.That(entry.LinkTarget is null, "Symlink/reparse input forbidden: " + entry.FullName);
    }
    private static string StreamHash(FileStream stream, long limit, out long actualBytes) {
        actualBytes=0; var buffer=new byte[65536]; using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        int read; while((read=stream.Read(buffer,0,buffer.Length))>0) { actualBytes+=read; Check.That(actualBytes <= limit,"NOT_VERIFIED: incremental actual read byte admission exceeded."); hash.AppendData(buffer,0,read); }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    public static string Hash(string path) => HashBounded(path,FileLimit,out _);
    private static long _admittedBytes;
    public static string HashBounded(string path, long limit, out long actualBytes) {
        SafePath(path); using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        Check.That(stream.Length <= limit, "NOT_VERIFIED: auxiliary file admission bound.");
        var hash = StreamHash(stream,limit,out actualBytes); Check.That(actualBytes <= limit, "NOT_VERIFIED: actual read auxiliary byte bound.");
        stream.Position=0; Check.That(hash == StreamHash(stream,limit,out _), "Auxiliary original changed on same locked handle."); return hash;
    }
    public static JsonDocument Read(string path, long perFileLimit = FileLimit)

    {
        path = Path.GetFullPath(path);
        for (FileSystemInfo? entry = new FileInfo(path); entry is not null; entry = entry is FileInfo file ? file.Directory : ((DirectoryInfo)entry).Parent) Check.That(entry.LinkTarget is null, "Symlink/reparse input forbidden: " + entry.FullName);
        var size = new FileInfo(path).Length;
        Check.That(size > 0 && size <= perFileLimit, "NOT_VERIFIED: artifact admission bound128MiB: " + path);
        // Validate incrementally before creating a typed graph/document. No evidence truncation.
        Check.That((_admittedBytes += size) <= 256L * 1024 * 1024 + 16L * 1024 * 1024, "Paired256MiB plus external16MiB cumulative artifact admission bound.");
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var originalHash = StreamHash(input,perFileLimit,out _); input.Position = 0;
        {
            var buffer = new byte[65536]; var retained = 0; long tokens = 0;
            long actualBytes = 0;
            var state = new JsonReaderState(new JsonReaderOptions { MaxDepth = 32 });
            var names = new Stack<HashSet<string>?>(); var counts = new Stack<int>();
            while (true) {
                if (retained == buffer.Length) { Check.That(buffer.Length < TokenBytes, "JSON token8MiB bound."); Array.Resize(ref buffer, Math.Min(TokenBytes, buffer.Length * 2)); }
                var read = input.Read(buffer, retained, buffer.Length - retained); var final = read == 0; actualBytes += read; Check.That(actualBytes <= perFileLimit && actualBytes <= size, "Original grew during admission/read.");
                var reader = new Utf8JsonReader(buffer.AsSpan(0, retained + read), final, state);
                while (reader.Read()) {
                    Check.That(++tokens <= 16_777_216, "JSON aggregate token bound.");
                    if (reader.TokenType == JsonTokenType.PropertyName) { Check.That(reader.ValueSpan.Length <= 256 && names.Peek()!.Count < 65536, "Property name256byte/per-object count bounds."); Check.That(names.Peek()!.Add(reader.GetString()!), "Duplicate JSON property."); }
                    else if (reader.TokenType is JsonTokenType.EndArray or JsonTokenType.EndObject) { names.Pop(); counts.Pop(); }
                    else {
                        if (counts.Count > 0) { var count = counts.Pop() + 1; Check.That(count <= 65536, "JSON collection bound."); counts.Push(count); }
                        if (reader.TokenType is JsonTokenType.StartArray or JsonTokenType.StartObject) { names.Push(reader.TokenType == JsonTokenType.StartObject ? new(StringComparer.Ordinal) : null); counts.Push(0); }
                    }
                }
                var consumed = checked((int)reader.BytesConsumed); state = reader.CurrentState;
                retained = retained + read - consumed; buffer.AsSpan(consumed, retained).CopyTo(buffer);
                if (final) { Check.That(retained == 0 && names.Count == 0, "Incomplete JSON."); break; }
            }
        }
        input.Position = 0;
        using var capped = new CappedReadStream(input, perFileLimit);
        var document = JsonDocument.Parse(capped, new JsonDocumentOptions { MaxDepth = 32 });
        input.Position = 0;
        Check.That(originalHash == StreamHash(input,perFileLimit,out _), "Original changed between streamed validation and graph load.");
        return document;
    }
    private sealed class CappedReadStream(Stream input, long limit) : Stream
    {
        private long _read;
        public override int Read(byte[] buffer, int offset, int count) {
            var actual=input.Read(buffer,offset,count); _read+=actual;
            Check.That(_read<=limit,"NOT_VERIFIED: JSON graph-load actual read byte cap."); return actual;
        }
        public override bool CanRead=>true;
        public override bool CanSeek=>false;
        public override bool CanWrite=>false;
        public override long Length=>throw new NotSupportedException();
        public override long Position { get=>_read; set=>throw new NotSupportedException(); }
        public override void Flush()=>throw new NotSupportedException();
        public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();
        public override void SetLength(long value)=>throw new NotSupportedException();
        public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
    }
}

internal static class Check
{
    public static void That(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    public static string Canonical(JsonElement value) => value.ValueKind switch {
        JsonValueKind.Object => "{" + string.Join(",", value.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal).Select(x => JsonSerializer.Serialize(x.Name) + ":" + Canonical(x.Value))) + "}",
        JsonValueKind.Array => "[" + string.Join(",", value.EnumerateArray().Select(Canonical)) + "]",
        _ => value.GetRawText()
    };
    public static void Equal<T>(T a, T b, string message) => That(Canonical(JsonSerializer.SerializeToElement(a, AbilityKit.Game.Cooking.Session.CookingNetworkWireCodec.JsonOptions)) == Canonical(JsonSerializer.SerializeToElement(b, AbilityKit.Game.Cooking.Session.CookingNetworkWireCodec.JsonOptions)), message);
}
