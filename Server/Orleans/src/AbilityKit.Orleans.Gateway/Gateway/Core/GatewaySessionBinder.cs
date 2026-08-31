using AbilityKit.Orleans.Gateway.Abstractions;

namespace AbilityKit.Orleans.Gateway.Core;

public sealed class GatewaySessionBinder
{
    private readonly IGatewaySessionRegistry _registry;

    public GatewaySessionBinder(IGatewaySessionRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public void Bind(GatewaySessionContext context, string accountId, string sessionToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(accountId))
            throw new ArgumentException("AccountId is required.", nameof(accountId));
        if (string.IsNullOrWhiteSpace(sessionToken))
            throw new ArgumentException("SessionToken is required.", nameof(sessionToken));

        if (!string.IsNullOrWhiteSpace(context.SessionToken)
            && !string.Equals(context.SessionToken, sessionToken, StringComparison.Ordinal))
        {
            _registry.UnbindToken(context.SessionToken);
        }

        if (!string.IsNullOrWhiteSpace(context.AccountId)
            && !string.Equals(context.AccountId, accountId, StringComparison.Ordinal))
        {
            _registry.UnbindAccount(context.AccountId);
        }

        context.AccountId = accountId;
        context.SessionToken = sessionToken;
        _registry.BindToken(sessionToken, context.ConnectionId);
        _registry.BindAccount(accountId, context.ConnectionId);
    }
}
