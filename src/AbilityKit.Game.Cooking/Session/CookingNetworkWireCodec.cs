using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace AbilityKit.Game.Cooking.Session;

public enum CookingNetworkMessageKind { Join, Joined, Baseline, BaselineAck, Ready, Command, CommandResult, Rejected }
public sealed record CookingNetworkWireEnvelope(int ProtocolVersion, CookingNetworkMessageKind Kind, string CorrelationId, JsonElement Payload);
public sealed record CookingNetworkJoin(PlayerId Participant, string JoinCredential, string? ServerSessionInstance, string? RebindToken);
public sealed record CookingNetworkJoined(string ServerSessionInstance, PlayerId Participant, long ConnectionGeneration, string RebindToken);
public sealed record CookingNetworkBaselineIdentity(string ServerSessionInstance, PlayerId Participant, long ConnectionGeneration,
    CookingLevelScope Scope, long Epoch, long SnapshotSequence, string StateHash, string IssueId);
public sealed record CookingNetworkBaseline(CookingNetworkBaselineIdentity Identity, int LevelFormatVersion,
    int RecipeSchemaVersion, CookingNetworkAuthorityCapture State, CookingNetworkSessionProjection Session);
public sealed record CookingNetworkParticipantProjection(PlayerId Participant, bool ConnectedOwnerBinding,
    long ConnectionGeneration, long LastValidatedClientSequence, long LastTerminalClientSequence, bool Ready, bool CleanupPending);
public sealed record CookingNetworkSessionProjection(string ServerSessionInstance,
    IReadOnlyList<CookingNetworkParticipantProjection> Participants);
internal sealed record CookingNetworkStateHashPayload(CookingNetworkAuthorityCapture State, CookingNetworkSessionProjection Session);
public sealed record CookingNetworkWireCommand(string ServerSessionInstance, long ConnectionGeneration, long ClientSequence,
    string StableCommandId, CookingLevelScope Scope, CookingRecipeCommand Command);
public sealed record CookingNetworkWireResult(string StableCommandId, RecipeCommandId? DomainCommandId, string Reason,
    CookingRecipeCommandResult? Result, CookingNetworkDispositionKind? Disposition);
public sealed record CookingNetworkSessionOptions(int BusinessCapacity = 256, int ControlCapacity = 32,
    int ConnectionCapacity = 8, int PerConnectionCapacity = 64, int ReceiptCapacity = 16384,
    int DuplicateWaiterCapacity = 8, int MaximumPrefix = 256, int FrameBytes = 8 * 1024 * 1024,
    int CommandBytes = 16 * 1024, int ControlBytes = 4096,
    int BaselineTokenLimit = 1048576, int BaselineCollectionLimit = 16384);

/// <summary>Wire v3 freezes typed input and bounds JSON before constructing its object graph.</summary>
public static class CookingNetworkWireCodec
{
    public const int ProtocolVersion = 3;
    public const uint OpCode = 0x434F4F33;
    public static JsonSerializerOptions JsonOptions { get; } = CreateOptions();
    private static JsonSerializerOptions CreateOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info => {
            foreach (var property in info.Properties)
                if (property.Set is not null) property.IsRequired = true;
        });
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            MaxDepth = 32, TypeInfoResolver = resolver, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        options.Converters.Add(new ReadOnlyListConverterFactory());
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        return options;
    }
    private sealed class ReadOnlyListConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>);
        public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(typeof(ReadOnlyListConverter<>).MakeGenericType(type.GetGenericArguments()))!;
    }
    private sealed class ReadOnlyListConverter<T> : JsonConverter<IReadOnlyList<T>>
    {
        public override IReadOnlyList<T>? Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            var values = JsonSerializer.Deserialize<T[]>(ref reader, options);
            return values is null ? null : Array.AsReadOnly(values);
        }
        public override void Write(Utf8JsonWriter writer, IReadOnlyList<T> value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value.ToArray(), options);
    }
    public static byte[] Encode<T>(CookingNetworkMessageKind kind, string correlation, T payload,
        CookingNetworkSessionOptions? bounds = null)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new CookingNetworkWireEnvelope(ProtocolVersion, kind, correlation,
            JsonSerializer.SerializeToElement(payload, JsonOptions)), JsonOptions);
        if (!TryDecode(bytes, bounds ?? new(), out _)) throw new ArgumentException("Encoded frame exceeds wire bounds.");
        return bytes;
    }
    public static bool TryDecode(ReadOnlySpan<byte> bytes, CookingNetworkSessionOptions bounds,
        out CookingNetworkWireEnvelope? envelope)
    {
        envelope = null;
        if (bytes.Length == 0 || bytes.Length > bounds.FrameBytes) return false;
        try {
            // Locate only the root discriminator, without allocating a JSON object graph. A second
            // complete scan applies the correct kind-specific bounds and rejects duplicate fields.
            var header = new Utf8JsonReader(bytes, new JsonReaderOptions { MaxDepth = 32 });
            var baseline = false;
            while (header.Read()) if (header.TokenType == JsonTokenType.PropertyName && header.CurrentDepth == 1 && header.ValueTextEquals("kind")) {
                if (!header.Read()) return false;
                baseline = header.TokenType == JsonTokenType.String && header.ValueTextEquals("Baseline"); break;
            }
            var tokenLimit = baseline ? bounds.BaselineTokenLimit : 65536;
            var collectionLimit = baseline ? bounds.BaselineCollectionLimit : 4096;
            if (tokenLimit <= 0 || collectionLimit <= 0) return false;
            var scan = new Utf8JsonReader(bytes, new JsonReaderOptions { MaxDepth = 32 });
            var nodes = 0; string? propertyName = null;
            var counts = new Stack<int>();
            var names = new Stack<HashSet<string>>();
            while (scan.Read()) {
                if (++nodes > tokenLimit) return false;
                if (scan.TokenType is JsonTokenType.StartArray or JsonTokenType.StartObject) {
                    if (counts.Count > 0) { var n = counts.Pop() + 1; if (n > collectionLimit) return false; counts.Push(n); }
                    counts.Push(0); names.Push(new HashSet<string>(StringComparer.Ordinal));
                } else if (scan.TokenType is JsonTokenType.EndArray or JsonTokenType.EndObject) { counts.Pop(); names.Pop(); }
                else if (scan.TokenType == JsonTokenType.PropertyName) {
                    propertyName = scan.GetString(); if (propertyName is null || Encoding.UTF8.GetByteCount(propertyName) > 128 || !names.Peek().Add(propertyName)) return false;
                } else {
                    if (counts.Count > 0) { var n = counts.Pop() + 1; if (n > collectionLimit) return false; counts.Push(n); }
                    if (scan.TokenType == JsonTokenType.String) { var limit = propertyName is "value" or "correlationId" or "stableCommandId" or "serverSessionInstance" or "supplierId" or "deliveryId" or "supplyRequestId" or "worldAnchor" ? 128 : propertyName is "rebindToken" or "joinCredential" ? 256 : 1024; if (Encoding.UTF8.GetByteCount(scan.GetString()!) > limit) return false; }
                }
            }
            envelope = JsonSerializer.Deserialize<CookingNetworkWireEnvelope>(bytes, JsonOptions);
            if (envelope is null || envelope.ProtocolVersion != ProtocolVersion || !Identifier(envelope.CorrelationId)) return false;
            var max = envelope.Kind == CookingNetworkMessageKind.Baseline ? bounds.FrameBytes :
                envelope.Kind == CookingNetworkMessageKind.Command ? bounds.CommandBytes : bounds.ControlBytes;
            return bytes.Length <= max;
        } catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException) { return false; }
    }
    public static T? Read<T>(CookingNetworkWireEnvelope envelope) where T : class
    {
        try { var value = envelope.Payload.Deserialize<T>(JsonOptions); return value is not null && ValidNullability(value) ? value : null; }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException or NullReferenceException or System.Reflection.TargetInvocationException) { return null; }
    }
    private sealed record PropertyValidation(PropertyInfo Property, bool AllowsNull);
    private static readonly ConcurrentDictionary<Type, PropertyValidation[]> PropertyValidations = new();
    private static PropertyValidation[] PropertiesFor(Type type) => PropertyValidations.GetOrAdd(type, static current => {
        var nullability = new NullabilityInfoContext();
        return current.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetIndexParameters().Length == 0 && property.GetMethod is not null)
            .Select(property => new PropertyValidation(property, nullability.Create(property).ReadState != NullabilityState.NotNull))
            .ToArray();
    });
    private static bool ValidNullability(object value)
    {
        bool Visit(object? current, int depth)
        {
            if (current is null || depth > 32) return false;
            var type = current.GetType();
            if (type.IsPrimitive || type.IsEnum || current is string or decimal) return true;
            if (current is System.Collections.IEnumerable collection) {
                foreach (var element in collection) if (element is null || !Visit(element, depth + 1)) return false;
                return true;
            }
            foreach (var descriptor in PropertiesFor(type)) {
                // Metadata is shared; values and collection elements are always validated for this instance.
                var child = descriptor.Property.GetValue(current);
                if (child is null) { if (!descriptor.AllowsNull) return false; }
                else if (!Visit(child, depth + 1)) return false;
            }
            return true;
        }
        return Visit(value, 0);
    }
    public static T Freeze<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions), JsonOptions)!;
    public static bool Identifier(string? value) => !string.IsNullOrWhiteSpace(value) && Encoding.UTF8.GetByteCount(value) <= 128;
    public static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions))).ToLowerInvariant();
    public static string BaselineHash(CookingNetworkAuthorityCapture state, CookingNetworkSessionProjection session) =>
        Hash(new CookingNetworkStateHashPayload(state, session));
    public static RecipeCommandId DomainId(string instance, CookingLevelScope scope, PlayerId participant, string stableId)
    {
        using var stream = new MemoryStream();
        void Text(string text) { var bytes = Encoding.UTF8.GetBytes(text); Span<byte> length = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length); stream.Write(length); stream.Write(bytes); }
        void Number(long value) { Span<byte> bytes = stackalloc byte[8]; BinaryPrimitives.WriteInt64BigEndian(bytes, value); stream.Write(bytes); }
        Text("cooking-network-command-v3"); Text(instance); Text(scope.MatchScope.Session.Value);
        Text(scope.MatchScope.World.Value); Text(scope.MatchScope.Match.Value); Number(scope.RestaurantRuntime.Value);
        Text(scope.Level.Value); Number(scope.LevelEpoch); Text(participant.Value); Text(stableId);
        return new RecipeCommandId("net3-" + Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant());
    }
}
