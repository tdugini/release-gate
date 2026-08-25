using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace ReleaseGate.Api.Infrastructure;

public static class FeatureFlagEvaluator
{
    private const int BucketCount = 10_000;

    public static EvaluationDecision Evaluate(
        string projectKey,
        string flagKey,
        string environmentKey,
        string subjectKey,
        bool enabled,
        int rolloutPercentage)
    {
        if (!enabled || rolloutPercentage <= 0)
        {
            return new EvaluationDecision(false, null, "flag-disabled");
        }

        if (rolloutPercentage >= 100)
        {
            return new EvaluationDecision(true, null, "rollout-match");
        }

        var bucket = GetBucket(projectKey, flagKey, environmentKey, subjectKey);
        var threshold = rolloutPercentage * 100;
        var isEnabled = bucket < threshold;

        return new EvaluationDecision(
            isEnabled,
            bucket,
            isEnabled ? "rollout-match" : "rollout-miss");
    }

    public static int GetBucket(
        string projectKey,
        string flagKey,
        string environmentKey,
        string subjectKey)
    {
        var input = $"{projectKey}:{flagKey}:{environmentKey}:{subjectKey}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var value = BinaryPrimitives.ReadUInt32BigEndian(hash.AsSpan(0, 4));

        return (int)(value % BucketCount);
    }
}

public sealed record EvaluationDecision(bool Enabled, int? Bucket, string Reason);
