#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace AbilityKit.Core.Recording.FrameRecord
{
    public enum FrameRecordDiffStatus
    {
        Identical = 0,
        Diverged = 1,
        WorldMismatch = 2,
        NoComparableStateHashes = 3
    }

    public enum FrameRecordDiffReason
    {
        None = 0,
        LeftStateHashesMissing = 1,
        RightStateHashesMissing = 2,
        BothStateHashesMissing = 3
    }

    [Serializable]
    public sealed class FrameRecordDiffOptions
    {
        public int ContextFrames { get; set; } = 2;
    }

    [Serializable]
    public sealed class FrameRecordDiffReport
    {
        public int SchemaVersion { get; set; } = 2;
        public FrameRecordDiffStatus Status { get; set; }
        public FrameRecordDiffReason Reason { get; set; }
        public bool Matched { get; set; }
        public int ContextFrames { get; set; }
        public FrameRecordMetaSummary Left { get; set; } = new FrameRecordMetaSummary();
        public FrameRecordMetaSummary Right { get; set; } = new FrameRecordMetaSummary();
        public FrameRecordStateHashDiff? FirstDivergence { get; set; }
        public FrameRecordDiffContext? Context { get; set; }
    }

    [Serializable]
    public sealed class FrameRecordMetaSummary
    {
        public string WorldId { get; set; } = string.Empty;
        public string WorldType { get; set; } = string.Empty;
        public int TickRate { get; set; }
        public int RandomSeed { get; set; }
        public string PlayerId { get; set; } = string.Empty;
        public long StartedAtUnixMs { get; set; }
        public int InputCount { get; set; }
        public int StateHashCount { get; set; }
        public int SnapshotCount { get; set; }
    }

    [Serializable]
    public sealed class FrameRecordStateHashDiff
    {
        public int Frame { get; set; }
        public int Ordinal { get; set; }
        public FrameRecordStateHashSummary? Left { get; set; }
        public FrameRecordStateHashSummary? Right { get; set; }
    }

    [Serializable]
    public sealed class FrameRecordDiffContext
    {
        public int StartFrame { get; set; }
        public int EndFrame { get; set; }
        public FrameRecordSideContext Left { get; set; } = new FrameRecordSideContext();
        public FrameRecordSideContext Right { get; set; } = new FrameRecordSideContext();
    }

    [Serializable]
    public sealed class FrameRecordSideContext
    {
        public List<FrameRecordInputSummary> Inputs { get; set; } = new List<FrameRecordInputSummary>();
        public List<FrameRecordStateHashSummary> StateHashes { get; set; } = new List<FrameRecordStateHashSummary>();
        public List<FrameRecordSnapshotSummary> Snapshots { get; set; } = new List<FrameRecordSnapshotSummary>();
    }

    [Serializable]
    public sealed class FrameRecordInputSummary
    {
        public int Frame { get; set; }
        public string PlayerId { get; set; } = string.Empty;
        public int OpCode { get; set; }
        public int PayloadBytes { get; set; }
        public bool PayloadValid { get; set; }
        public string PayloadSha256 { get; set; } = string.Empty;
    }

    [Serializable]
    public sealed class FrameRecordStateHashSummary
    {
        public int Frame { get; set; }
        public int Ordinal { get; set; }
        public int Version { get; set; }
        public uint Hash { get; set; }
    }

    [Serializable]
    public sealed class FrameRecordSnapshotSummary
    {
        public int Frame { get; set; }
        public int OpCode { get; set; }
        public int PayloadBytes { get; set; }
        public bool PayloadValid { get; set; }
        public string PayloadSha256 { get; set; } = string.Empty;
    }

    public sealed class FrameRecordDiffAnalyzer
    {
        public FrameRecordDiffReport Compare(
            FrameRecordFile left,
            FrameRecordFile right,
            FrameRecordDiffOptions? options = null)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));

            var contextFrames = Math.Max(0, options != null ? options.ContextFrames : 2);
            var report = new FrameRecordDiffReport
            {
                ContextFrames = contextFrames,
                Left = SummarizeMeta(left),
                Right = SummarizeMeta(right),
            };

            if (!string.Equals(report.Left.WorldId, report.Right.WorldId, StringComparison.Ordinal))
            {
                report.Status = FrameRecordDiffStatus.WorldMismatch;
                return report;
            }

            var leftHashes = BuildHashSummaries(left.StateHashes);
            var rightHashes = BuildHashSummaries(right.StateHashes);
            if (leftHashes.Count == 0 || rightHashes.Count == 0)
            {
                report.Status = FrameRecordDiffStatus.NoComparableStateHashes;
                report.Reason = GetMissingStateHashReason(leftHashes.Count, rightHashes.Count);
                return report;
            }

            var divergence = FindFirstDivergence(leftHashes, rightHashes);
            if (divergence == null)
            {
                report.Status = FrameRecordDiffStatus.Identical;
                report.Matched = true;
                return report;
            }

            report.Status = FrameRecordDiffStatus.Diverged;
            report.FirstDivergence = divergence;
            report.Context = BuildContext(left, right, divergence.Frame, contextFrames);
            return report;
        }

        private static FrameRecordMetaSummary SummarizeMeta(FrameRecordFile record)
        {
            var meta = record.Meta;
            return new FrameRecordMetaSummary
            {
                WorldId = meta != null ? meta.WorldId ?? string.Empty : string.Empty,
                WorldType = meta != null ? meta.WorldType ?? string.Empty : string.Empty,
                TickRate = meta != null ? meta.TickRate : 0,
                RandomSeed = meta != null ? meta.RandomSeed : 0,
                PlayerId = meta != null ? meta.PlayerId ?? string.Empty : string.Empty,
                StartedAtUnixMs = meta != null ? meta.StartedAtUnixMs : 0L,
                InputCount = record.Inputs != null ? record.Inputs.Count : 0,
                StateHashCount = record.StateHashes != null ? record.StateHashes.Count : 0,
                SnapshotCount = record.Snapshots != null ? record.Snapshots.Count : 0,
            };
        }

        private static List<FrameRecordStateHashSummary> BuildHashSummaries(
            List<FrameRecordStateHashFrame> hashes)
        {
            var result = new List<FrameRecordStateHashSummary>(hashes != null ? hashes.Count : 0);
            if (hashes == null) return result;

            var ordinals = new Dictionary<int, int>();
            for (var i = 0; i < hashes.Count; i++)
            {
                var hash = hashes[i];
                if (hash == null) continue;

                ordinals.TryGetValue(hash.Frame, out var ordinal);
                ordinals[hash.Frame] = ordinal + 1;
                result.Add(new FrameRecordStateHashSummary
                {
                    Frame = hash.Frame,
                    Ordinal = ordinal,
                    Version = hash.Version,
                    Hash = hash.Hash,
                });
            }

            result.Sort(CompareHashKey);
            return result;
        }

        private static FrameRecordDiffReason GetMissingStateHashReason(int leftCount, int rightCount)
        {
            if (leftCount == 0 && rightCount == 0) return FrameRecordDiffReason.BothStateHashesMissing;
            return leftCount == 0
                ? FrameRecordDiffReason.LeftStateHashesMissing
                : FrameRecordDiffReason.RightStateHashesMissing;
        }

        private static FrameRecordStateHashDiff? FindFirstDivergence(
            List<FrameRecordStateHashSummary> left,
            List<FrameRecordStateHashSummary> right)
        {
            var leftIndex = 0;
            var rightIndex = 0;
            while (leftIndex < left.Count || rightIndex < right.Count)
            {
                if (leftIndex >= left.Count)
                {
                    var rightOnly = right[rightIndex];
                    return CreateDiff(rightOnly.Frame, rightOnly.Ordinal, null, rightOnly);
                }

                if (rightIndex >= right.Count)
                {
                    var leftOnly = left[leftIndex];
                    return CreateDiff(leftOnly.Frame, leftOnly.Ordinal, leftOnly, null);
                }

                var leftValue = left[leftIndex];
                var rightValue = right[rightIndex];
                var keyComparison = CompareHashKey(leftValue, rightValue);
                if (keyComparison == 0)
                {
                    if (leftValue.Version != rightValue.Version || leftValue.Hash != rightValue.Hash)
                    {
                        return CreateDiff(leftValue.Frame, leftValue.Ordinal, leftValue, rightValue);
                    }

                    leftIndex++;
                    rightIndex++;
                    continue;
                }

                if (keyComparison < 0)
                {
                    return CreateDiff(leftValue.Frame, leftValue.Ordinal, leftValue, null);
                }

                return CreateDiff(rightValue.Frame, rightValue.Ordinal, null, rightValue);
            }

            return null;
        }

        private static FrameRecordStateHashDiff CreateDiff(
            int frame,
            int ordinal,
            FrameRecordStateHashSummary? left,
            FrameRecordStateHashSummary? right)
        {
            return new FrameRecordStateHashDiff
            {
                Frame = frame,
                Ordinal = ordinal,
                Left = left,
                Right = right,
            };
        }

        private static FrameRecordDiffContext BuildContext(
            FrameRecordFile left,
            FrameRecordFile right,
            int frame,
            int contextFrames)
        {
            var startFrame = (int)Math.Max(0L, (long)frame - contextFrames);
            var endFrame = (int)Math.Min(int.MaxValue, (long)frame + contextFrames);
            return new FrameRecordDiffContext
            {
                StartFrame = startFrame,
                EndFrame = endFrame,
                Left = BuildSideContext(left, startFrame, endFrame),
                Right = BuildSideContext(right, startFrame, endFrame),
            };
        }

        private static FrameRecordSideContext BuildSideContext(
            FrameRecordFile record,
            int startFrame,
            int endFrame)
        {
            var context = new FrameRecordSideContext();
            AddInputs(context.Inputs, record.Inputs, startFrame, endFrame);
            AddHashes(context.StateHashes, record.StateHashes, startFrame, endFrame);
            AddSnapshots(context.Snapshots, record.Snapshots, startFrame, endFrame);
            return context;
        }

        private static void AddInputs(
            List<FrameRecordInputSummary> target,
            List<FrameRecordInputFrame> source,
            int startFrame,
            int endFrame)
        {
            if (source == null) return;
            for (var i = 0; i < source.Count; i++)
            {
                var input = source[i];
                if (input == null || input.Frame < startFrame || input.Frame > endFrame) continue;
                SummarizePayload(
                    input.PayloadBase64,
                    out var payloadBytes,
                    out var payloadValid,
                    out var payloadSha256);
                target.Add(new FrameRecordInputSummary
                {
                    Frame = input.Frame,
                    PlayerId = input.PlayerId ?? string.Empty,
                    OpCode = input.OpCode,
                    PayloadBytes = payloadBytes,
                    PayloadValid = payloadValid,
                    PayloadSha256 = payloadSha256,
                });
            }
        }

        private static void AddHashes(
            List<FrameRecordStateHashSummary> target,
            List<FrameRecordStateHashFrame> source,
            int startFrame,
            int endFrame)
        {
            var summaries = BuildHashSummaries(source);
            for (var i = 0; i < summaries.Count; i++)
            {
                var hash = summaries[i];
                if (hash.Frame >= startFrame && hash.Frame <= endFrame) target.Add(hash);
            }
        }

        private static void AddSnapshots(
            List<FrameRecordSnapshotSummary> target,
            List<FrameRecordSnapshotFrame> source,
            int startFrame,
            int endFrame)
        {
            if (source == null) return;
            for (var i = 0; i < source.Count; i++)
            {
                var snapshot = source[i];
                if (snapshot == null || snapshot.Frame < startFrame || snapshot.Frame > endFrame) continue;
                SummarizePayload(
                    snapshot.PayloadBase64,
                    out var payloadBytes,
                    out var payloadValid,
                    out var payloadSha256);
                target.Add(new FrameRecordSnapshotSummary
                {
                    Frame = snapshot.Frame,
                    OpCode = snapshot.OpCode,
                    PayloadBytes = payloadBytes,
                    PayloadValid = payloadValid,
                    PayloadSha256 = payloadSha256,
                });
            }
        }

        private static void SummarizePayload(
            string? payloadBase64,
            out int payloadBytes,
            out bool payloadValid,
            out string payloadSha256)
        {
            byte[] payload;
            try
            {
                payload = string.IsNullOrEmpty(payloadBase64)
                    ? Array.Empty<byte>()
                    : Convert.FromBase64String(payloadBase64);
                payloadValid = true;
            }
            catch (FormatException)
            {
                payload = Array.Empty<byte>();
                payloadValid = false;
            }

            payloadBytes = payload.Length;
            using (var sha256 = SHA256.Create())
            {
                payloadSha256 = ToLowerHex(sha256.ComputeHash(payload));
            }
        }
        
        #pragma warning restore CS1591

        private static string ToLowerHex(byte[] bytes)
        {
            var chars = new char[bytes.Length * 2];
            const string alphabet = "0123456789abcdef";
            for (var i = 0; i < bytes.Length; i++)
            {
                chars[i * 2] = alphabet[bytes[i] >> 4];
                chars[i * 2 + 1] = alphabet[bytes[i] & 0x0F];
            }

            return new string(chars);
        }

        private static int CompareHashKey(
            FrameRecordStateHashSummary? left,
            FrameRecordStateHashSummary? right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;
            var frameComparison = left.Frame.CompareTo(right.Frame);
            return frameComparison != 0 ? frameComparison : left.Ordinal.CompareTo(right.Ordinal);
        }
    }
}
