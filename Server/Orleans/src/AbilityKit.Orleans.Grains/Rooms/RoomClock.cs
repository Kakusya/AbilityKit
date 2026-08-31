using System;

namespace AbilityKit.Orleans.Grains.Rooms;

/// <summary>
/// 时钟抽象，便于测试注入可控时间。Grain 内部使用真实时间，TickAsync 使用请求传入的时间。
/// </summary>
internal interface IRoomClock
{
    long NowTicks { get; }

    long NowUnixMs { get; }
}

internal sealed class RoomClock : IRoomClock
{
    public static readonly RoomClock Instance = new();

    public long NowTicks => DateTime.UtcNow.Ticks;

    public long NowUnixMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
