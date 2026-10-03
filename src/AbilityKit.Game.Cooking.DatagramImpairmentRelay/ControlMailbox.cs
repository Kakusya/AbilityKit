using System.Collections.Concurrent;
using System.Text.Json;

namespace AbilityKit.Game.Cooking.ImpairmentControl;

// Application-owned stdin is a scheduling/network-policy control plane, never a game wire API.
internal sealed class ControlMailbox
{
    private readonly ConcurrentQueue<JsonElement> _queue = new();
    private readonly string _nonce;
    private Exception? _error;
    private long _sequence;
    internal ControlMailbox(string nonce)
    {
        _nonce = nonce;
        _ = Task.Run(async () => {
            try {
                var buffer = new char[1];
                var builder = new System.Text.StringBuilder(256);
                while (await Console.In.ReadAsync(buffer.AsMemory()) != 0) {
                    var character = buffer[0];
                    if (character != '\n') {
                        if (builder.Length >= 16384) throw new InvalidOperationException("Control input bound.");
                        builder.Append(character);
                        continue;
                    }
                    var line = builder.ToString().TrimEnd('\r');
                    builder.Clear();
                    if (_queue.Count >= 64) throw new InvalidOperationException("Control queue bound.");
                    using var document = JsonDocument.Parse(line, new JsonDocumentOptions { MaxDepth = 8 });
                    _queue.Enqueue(document.RootElement.Clone());
                }
                if (builder.Length != 0) throw new InvalidOperationException("Truncated control input.");
            } catch (Exception error) { Volatile.Write(ref _error, error); }
        });
    }
    internal JsonElement? Take()
    {
        if (Volatile.Read(ref _error) is { } error) throw new InvalidOperationException("Control reader failed.", error);
        if (!_queue.TryDequeue(out var value)) return null;
        if (value.GetProperty("nonce").GetString() != _nonce || value.GetProperty("sequence").GetInt64() != _sequence + 1)
            throw new InvalidOperationException("Control nonce/sequence mismatch.");
        _sequence++;
        return value;
    }
    internal static void Event(string nonce, string kind, object data)
    {
        var line = "CONTROL_EVENT " + JsonSerializer.Serialize(new { nonce, kind, pid = Environment.ProcessId, data });
        if (line.Length > 16384) throw new InvalidOperationException("Control event bound.");
        Console.WriteLine(line); Console.Out.Flush();
    }
}
