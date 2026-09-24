using System.Text.Json;
using AbilityKit.Game.Cooking;

namespace AbilityKit.Game.Cooking.Tests.Harness;

public enum CookingLanMessageKind
{
    HandshakeRequest,
    HandshakeAccepted,
    RecipeCommand,
    RecipeCommandResult,
    RecipeSnapshot,
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
    OrderId? Order);

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
