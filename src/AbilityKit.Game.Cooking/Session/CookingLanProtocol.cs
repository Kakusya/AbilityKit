using System.Text.Json;

namespace AbilityKit.Game.Cooking.Session;

public enum CookingLanMessageKind
{
    HandshakeRequest,
    HandshakeAccepted,
    RecipeCommand,
    RecipeCommandResult,
    RecipeSnapshot,
    SessionSnapshot,
}

public sealed record CookingLanEnvelope(
    CookingLanMessageKind Kind,
    string CorrelationId,
    JsonElement Payload);

public sealed record CookingLanHandshakeRequest(
    string PlayerId,
    string? ReconnectToken = null);

public sealed record CookingLanHandshakeAccepted(
    string AssignedPlayerId,
    string ReconnectToken);

public sealed record CookingLanRecipeCommandPacket(
    long CommandId,
    CookingRecipeOperation Operation,
    ItemId? Item,
    StationSlotId? Station,
    ItemId? Container,
    RecipeId? Recipe,
    OrderId? Order,
    CookingLevelScope? LevelScope = null);

public sealed record CookingLanRecipeCommandResultPacket(
    long CommandId,
    CookingRecipeOutcome Outcome,
    CookingRecipeRejectionReason Reason,
    long StateVersion,
    bool IsDuplicate,
    IReadOnlyList<CookingRecipeEvent> Events);

public sealed record CookingLanSnapshotPacket(
    long Sequence,
    CookingRecipeSnapshot Snapshot);

public sealed record CookingLanSessionSnapshotPacket(
    CookingSessionSnapshot Snapshot,
    string Sha256);

public static class CookingLanCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static byte[] Encode<T>(CookingLanMessageKind kind, string correlationId, T payload)
    {
        var element = JsonSerializer.SerializeToElement(payload, Options);
        var envelope = new CookingLanEnvelope(kind, correlationId, element);
        return JsonSerializer.SerializeToUtf8Bytes(envelope, Options);
    }

    public static bool TryDecode(ReadOnlySpan<byte> datagram, out CookingLanEnvelope? envelope)
    {
        try
        {
            envelope = JsonSerializer.Deserialize<CookingLanEnvelope>(datagram, Options);
            return envelope != null;
        }
        catch
        {
            envelope = null;
            return false;
        }
    }

    public static bool TryReadPayload<T>(CookingLanEnvelope envelope, out T? payload)
    {
        try
        {
            payload = envelope.Payload.Deserialize<T>(Options);
            return payload != null;
        }
        catch
        {
            payload = default;
            return false;
        }
    }
}
