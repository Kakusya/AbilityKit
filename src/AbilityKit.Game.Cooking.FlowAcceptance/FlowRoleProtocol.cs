using System.Net;
using System.Text;
using System.Text.Json;

namespace AbilityKit.Game.Cooking.FlowAcceptance;

// Test control protocol only. The product network remains the existing v3 codec/session.
internal static class FlowRoleProtocol
{
    internal const int MaxLineBytes = 65536;
    private static readonly UTF8Encoding Utf8 = new(false, true);

    internal static async Task<string?> ReadLineAsync(Stream stream, CancellationToken token)
    {
        // Bound the bytes before allocating/deserializing a line, including unterminated input.
        var bytes = new byte[MaxLineBytes];
        var one = new byte[1];
        var count = 0;
        while (await stream.ReadAsync(one, token) != 0)
        {
            if (one[0] == '\n') return Utf8.GetString(bytes, 0, count);
            if (count == bytes.Length) throw new InvalidDataException("RoleMessageTooLarge");
            bytes[count++] = one[0];
        }
        if (count != 0) throw new InvalidDataException("PartialRoleMessage");
        return null;
    }

    internal static async Task WriteAsync<T>(Stream stream, T value, CancellationToken token)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, FlowJson.Options);
        if (bytes.Length > MaxLineBytes) throw new InvalidDataException("RoleMessageTooLarge");
        await stream.WriteAsync(bytes, token);
        await stream.WriteAsync(new byte[] { (byte)'\n' }, token);
        await stream.FlushAsync(token);
    }

    internal static void ValidateControl(RoleControl value, string role, RunIdentity? run, long sequence)
    {
        if (value.SchemaVersion != 1 || value.RoleId != role || value.ControlSequence != sequence || !Enum.IsDefined(value.Kind))
            throw new InvalidDataException("RoleControlIdentityOrSequence");
        FlowJson.RequireId(value.ControlId);
        FlowJson.RequireId(value.Run.RequestId); FlowJson.RequireId(value.Run.RunId); FlowJson.RequireId(value.Run.AttemptId);
        if (value.Run.RunGeneration <= 0 || run is not null && value.Run != run)
            throw new InvalidDataException("RoleRunChanged");
        if (run is null && value.Kind != RoleControlKind.Initialize || run is not null && value.Kind == RoleControlKind.Initialize)
            throw new InvalidDataException("RoleInitializeOnce");
        var valid = value.Kind switch
        {
            RoleControlKind.Initialize => value.Init is not null && value.Action is null && value.ReplayOfCallId is null &&
                value.BarrierId is null && value.ObservationPoint is null,
            RoleControlKind.ArmAction => value.Init is null && (value.Action is null) != (value.ReplayOfCallId is null) &&
                value.BarrierId is not null && value.ObservationPoint is null && (value.Action is null || value.Action.CallId == value.ControlId),
            RoleControlKind.ReleaseBarrier => value.Init is null && value.Action is null && value.ReplayOfCallId is null &&
                value.BarrierId is not null && value.ObservationPoint is null,
            RoleControlKind.Observe => value.Init is null && value.Action is null && value.ReplayOfCallId is null &&
                value.BarrierId is null && value.ObservationPoint is not null,
            RoleControlKind.Stop => value.Init is null && value.Action is null && value.ReplayOfCallId is null &&
                value.BarrierId is null && value.ObservationPoint is null,
            _ => false
        };
        if (!valid) throw new InvalidDataException("RoleControlPayload");
        if (value.BarrierId is not null) FlowJson.RequireId(value.BarrierId);
        if (value.ObservationPoint is not null) FlowJson.RequireId(value.ObservationPoint);
        if (value.ReplayOfCallId is not null) FlowJson.RequireId(value.ReplayOfCallId);
        if (value.Init is { } init) ValidateInit(init, role);
    }

    internal static void ValidateInit(RoleInit init, string role)
    {
        if (init.RoleId != role || init.Budgets is null || init.Logs is null ||
            init.Budgets.StartupMs != 10000 || init.Budgets.StepMs != 10000 || init.Budgets.ConvergenceMs is < 1000 or > 10000 ||
            init.Budgets.OverallMs != 120000 || init.Budgets.ResetMs != 10000 || init.Budgets.PublishMs != 2000 ||
            init.Logs != new FlowLogLimits(256, 65536, 4096, 8388608, 262144))
            throw new InvalidDataException("RoleInitLimits");
        if (role == "server")
        {
            var f = init.Fixture;
            var registered = FlowFixture.Spec;
            if (init.ActorId is not null || init.Endpoint is not null || f is null || f.Id != registered.Id ||
                f.Version != registered.Version || f.ItemId != registered.ItemId || f.InitialSlot != registered.InitialSlot ||
                f.DropSlot != registered.DropSlot || !f.Actors.SequenceEqual(registered.Actors))
                throw new InvalidDataException("RoleServerInit");
        }
        else if (role is not ("client-a" or "client-b") || init.ActorId != (role == "client-a" ? "A" : "B") ||
            init.Fixture is not null || !TryEndpoint(init.Endpoint, out _)) throw new InvalidDataException("RoleClientInit");
    }

    internal static bool TryEndpoint(string? value, out IPEndPoint endpoint)
    {
        endpoint = null!;
        return value is not null && IPEndPoint.TryParse(value, out endpoint!) && endpoint.Address.Equals(IPAddress.Loopback) &&
            endpoint.Port is > 0 and <= 65535;
    }

    internal static void ValidateReply(RoleReply reply, RunIdentity run, string role, long sequence, long eventSequence)
    {
        if (reply.SchemaVersion != 1 || reply.Run != run || reply.RoleId != role || reply.HostSequence != sequence ||
            !Enum.IsDefined(reply.Kind)) throw new InvalidDataException("RoleReplyIdentityOrSequence");
        if (reply.ControlId is not null) FlowJson.RequireId(reply.ControlId);
        if (reply.Kind == RoleReplyKind.Armed)
        {
            if (reply.Event is not null || reply.ControlId is null || reply.BoundEndpoint is not null || reply.ErrorCode is not null)
                throw new InvalidDataException("RoleArmedPayload");
            return;
        }
        var e = reply.Event ?? throw new InvalidDataException("RoleReplyEventMissing");
        if (e.SchemaVersion != 1 || e.RunId != run.RunId || e.RunGeneration != run.RunGeneration || e.HostId != role ||
            e.HostSequence != eventSequence) throw new InvalidDataException("RoleEventIdentityOrSequence");
        if (e.Observation is { } observation && (observation.Fence.RunGeneration != run.RunGeneration ||
            observation.Fence.Scope.MatchScope.Session.Value != run.RunId ||
            (role == "server" ? observation.Origin != ObservationOrigin.Authority || observation.ObserverId != "authority" ||
                observation.Fence.ConnectionGeneration is not null : observation.Origin != ObservationOrigin.Client || observation.ObserverId != role)))
            throw new InvalidDataException("RoleObservationIdentity");
        if (e.Command is { } command && (reply.ControlId != command.CallId || role == "server" ||
            command.ActorId != (role == "client-a" ? "A" : "B") || command.FrozenCommand.Scope.Session.Value != run.RunId))
            throw new InvalidDataException("RoleCommandIdentity");
        bool payload = e.Kind switch
        {
            FlowEventKind.CommandObserved => e.Command is not null && e.CallId == e.Command.CallId && e.Observation is null && e.Check is null,
            FlowEventKind.RoleReady or FlowEventKind.StateObserved => e.Observation is not null && e.Command is null && e.Check is null,
            FlowEventKind.StepStarted or FlowEventKind.RoleFaulted or FlowEventKind.RoleStopped => e.Command is null && e.Observation is null && e.Check is null,
            _ => false
        };
        bool kind = reply.Kind switch
        {
            RoleReplyKind.Ready => e.Kind == FlowEventKind.RoleReady && reply.ControlId is not null && reply.ErrorCode is null &&
                (role == "server" ? TryEndpoint(reply.BoundEndpoint, out _) : reply.BoundEndpoint is null),
            RoleReplyKind.Event => (e.Kind is FlowEventKind.StepStarted or FlowEventKind.CommandObserved or FlowEventKind.StateObserved) &&
                reply.BoundEndpoint is null && reply.ErrorCode is null,
            RoleReplyKind.Fault => e.Kind == FlowEventKind.RoleFaulted && reply.BoundEndpoint is null && reply.ErrorCode is not null,
            RoleReplyKind.Stopped => e.Kind == FlowEventKind.RoleStopped && reply.ControlId is not null && reply.BoundEndpoint is null && reply.ErrorCode is null,
            _ => false
        };
        if (!payload || !kind) throw new InvalidDataException("RoleReplyPayload");
    }
}
