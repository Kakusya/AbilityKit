using System;

namespace AbilityKit.Demo.Moba.Diagnostics
{
    public enum BattleDiagnosticQueryPhase
    {
        Idle = 0,
        Loading = 1,
        Ready = 2,
        Empty = 3,
        Partial = 4,
        Unavailable = 5,
        Error = 6
    }

    public readonly struct BattleDiagnosticQueryStatus : IEquatable<BattleDiagnosticQueryStatus>
    {
        public BattleDiagnosticQueryStatus(
            long requestId,
            long storeRevision,
            BattleDiagnosticQueryPhase phase,
            BattleDiagnosticDataAvailability availability,
            int resultCount,
            bool hasMore,
            string errorCode = "",
            string message = "")
        {
            if (resultCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(resultCount));
            }

            RequestId = requestId;
            StoreRevision = storeRevision;
            Phase = phase;
            Availability = availability;
            ResultCount = resultCount;
            HasMore = hasMore;
            ErrorCode = errorCode ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public long RequestId { get; }
        public long StoreRevision { get; }
        public BattleDiagnosticQueryPhase Phase { get; }
        public BattleDiagnosticDataAvailability Availability { get; }
        public int ResultCount { get; }
        public bool HasMore { get; }
        public string ErrorCode { get; }
        public string Message { get; }

        public bool IsTerminal =>
            Phase != BattleDiagnosticQueryPhase.Idle &&
            Phase != BattleDiagnosticQueryPhase.Loading;

        public bool CanDisplayResults =>
            ResultCount > 0 &&
            (Phase == BattleDiagnosticQueryPhase.Ready ||
             Phase == BattleDiagnosticQueryPhase.Partial);

        public bool IsStaleComparedTo(long requestId, long storeRevision)
        {
            return RequestId != requestId || StoreRevision != storeRevision;
        }

        public static BattleDiagnosticQueryStatus Loading(long requestId, long storeRevision)
        {
            return new BattleDiagnosticQueryStatus(
                requestId,
                storeRevision,
                BattleDiagnosticQueryPhase.Loading,
                BattleDiagnosticDataAvailability.Available,
                0,
                false);
        }

        public static BattleDiagnosticQueryStatus Ready(
            long requestId,
            long storeRevision,
            int resultCount,
            bool hasMore)
        {
            var phase = resultCount == 0
                ? BattleDiagnosticQueryPhase.Empty
                : BattleDiagnosticQueryPhase.Ready;

            return new BattleDiagnosticQueryStatus(
                requestId,
                storeRevision,
                phase,
                BattleDiagnosticDataAvailability.Available,
                resultCount,
                hasMore);
        }

        public static BattleDiagnosticQueryStatus Partial(
            long requestId,
            long storeRevision,
            int resultCount,
            BattleDiagnosticDataAvailability availability,
            string message = "")
        {
            if (availability != BattleDiagnosticDataAvailability.Truncated &&
                availability != BattleDiagnosticDataAvailability.Evicted)
            {
                throw new ArgumentException(
                    "Partial results require Truncated or Evicted availability.",
                    nameof(availability));
            }

            return new BattleDiagnosticQueryStatus(
                requestId,
                storeRevision,
                BattleDiagnosticQueryPhase.Partial,
                availability,
                resultCount,
                false,
                message: message);
        }

        public static BattleDiagnosticQueryStatus Unavailable(
            long requestId,
            long storeRevision,
            BattleDiagnosticDataAvailability availability,
            string message = "")
        {
            if (availability == BattleDiagnosticDataAvailability.Available)
            {
                throw new ArgumentException(
                    "Unavailable results must provide a non-Available reason.",
                    nameof(availability));
            }

            return new BattleDiagnosticQueryStatus(
                requestId,
                storeRevision,
                BattleDiagnosticQueryPhase.Unavailable,
                availability,
                0,
                false,
                message: message);
        }

        public static BattleDiagnosticQueryStatus Failed(
            long requestId,
            long storeRevision,
            string errorCode,
            string message)
        {
            return new BattleDiagnosticQueryStatus(
                requestId,
                storeRevision,
                BattleDiagnosticQueryPhase.Error,
                BattleDiagnosticDataAvailability.Error,
                0,
                false,
                errorCode,
                message);
        }

        public bool Equals(BattleDiagnosticQueryStatus other)
        {
            return RequestId == other.RequestId &&
                   StoreRevision == other.StoreRevision &&
                   Phase == other.Phase &&
                   Availability == other.Availability &&
                   ResultCount == other.ResultCount &&
                   HasMore == other.HasMore &&
                   string.Equals(ErrorCode, other.ErrorCode, StringComparison.Ordinal) &&
                   string.Equals(Message, other.Message, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is BattleDiagnosticQueryStatus other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = RequestId.GetHashCode();
                hashCode = (hashCode * 397) ^ StoreRevision.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)Phase;
                hashCode = (hashCode * 397) ^ (int)Availability;
                hashCode = (hashCode * 397) ^ ResultCount;
                hashCode = (hashCode * 397) ^ HasMore.GetHashCode();
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(ErrorCode ?? string.Empty);
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Message ?? string.Empty);
                return hashCode;
            }
        }
    }

    public readonly struct BattleDiagnosticPageRequest : IEquatable<BattleDiagnosticPageRequest>
    {
        public const int DefaultPageSize = 500;
        public const int MaximumPageSize = 500;

        public BattleDiagnosticPageRequest(long storeRevision, int offset, int limit)
        {
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if (limit <= 0 || limit > MaximumPageSize)
            {
                throw new ArgumentOutOfRangeException(nameof(limit));
            }

            StoreRevision = storeRevision;
            Offset = offset;
            Limit = limit;
        }

        public long StoreRevision { get; }
        public int Offset { get; }
        public int Limit { get; }

        public BattleDiagnosticPageRequest NextPage()
        {
            return new BattleDiagnosticPageRequest(StoreRevision, checked(Offset + Limit), Limit);
        }

        public bool Equals(BattleDiagnosticPageRequest other)
        {
            return StoreRevision == other.StoreRevision &&
                   Offset == other.Offset &&
                   Limit == other.Limit;
        }

        public override bool Equals(object obj)
        {
            return obj is BattleDiagnosticPageRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = StoreRevision.GetHashCode();
                hashCode = (hashCode * 397) ^ Offset;
                hashCode = (hashCode * 397) ^ Limit;
                return hashCode;
            }
        }
    }
}
