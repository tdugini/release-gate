using ReleaseGate.Api.Infrastructure;
using Xunit;

namespace ReleaseGate.Api.IntegrationTests;

public sealed class FeatureFlagEvaluatorTests
{
    [Fact]
    public void Disabled_flag_is_always_disabled()
    {
        var decision = FeatureFlagEvaluator.Evaluate(
            "storefront",
            "new-checkout",
            "production",
            "user-42",
            enabled: false,
            rolloutPercentage: 100);

        Assert.False(decision.Enabled);
        Assert.Null(decision.Bucket);
        Assert.Equal("flag-disabled", decision.Reason);
    }

    [Fact]
    public void Full_rollout_is_always_enabled()
    {
        var decision = FeatureFlagEvaluator.Evaluate(
            "storefront",
            "new-checkout",
            "production",
            "user-42",
            enabled: true,
            rolloutPercentage: 100);

        Assert.True(decision.Enabled);
        Assert.Null(decision.Bucket);
        Assert.Equal("rollout-match", decision.Reason);
    }

    [Fact]
    public void Same_subject_is_assigned_to_the_same_bucket()
    {
        var first = FeatureFlagEvaluator.GetBucket(
            "storefront",
            "new-checkout",
            "production",
            "user-92841");
        var second = FeatureFlagEvaluator.GetBucket(
            "storefront",
            "new-checkout",
            "production",
            "user-92841");

        Assert.Equal(first, second);
        Assert.InRange(first, 0, 9_999);
    }

    [Fact]
    public void Percentage_rollout_uses_the_deterministic_bucket_threshold()
    {
        const int rolloutPercentage = 25;
        var matchingSubject = FindSubject(bucket => bucket < rolloutPercentage * 100);
        var missingSubject = FindSubject(bucket => bucket >= rolloutPercentage * 100);

        var matchingDecision = FeatureFlagEvaluator.Evaluate(
            "storefront",
            "new-checkout",
            "production",
            matchingSubject,
            enabled: true,
            rolloutPercentage);
        var missingDecision = FeatureFlagEvaluator.Evaluate(
            "storefront",
            "new-checkout",
            "production",
            missingSubject,
            enabled: true,
            rolloutPercentage);

        Assert.True(matchingDecision.Enabled);
        Assert.Equal("rollout-match", matchingDecision.Reason);
        Assert.False(missingDecision.Enabled);
        Assert.Equal("rollout-miss", missingDecision.Reason);
    }

    private static string FindSubject(Func<int, bool> predicate)
    {
        for (var index = 0; index < 10_000; index++)
        {
            var subject = $"user-{index}";
            var bucket = FeatureFlagEvaluator.GetBucket(
                "storefront",
                "new-checkout",
                "production",
                subject);

            if (predicate(bucket))
            {
                return subject;
            }
        }

        throw new InvalidOperationException("Could not find a subject for the expected bucket range.");
    }
}
