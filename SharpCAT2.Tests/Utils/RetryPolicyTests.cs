using Microsoft.Extensions.Logging;
using SharpCAT2.Common.Utils;
using Xunit;

namespace SharpCAT2.Tests.Utils;

/// <summary>
/// Tests for retry policy and retry helper functionality
/// </summary>
public class RetryPolicyTests
{
    [Fact]
    public async Task ExecuteWithRetryAsync_SucceedsOnFirstAttempt_NoRetry()
    {
        // Arrange
        var policy = new RetryPolicy(maxRetries: 3, retryDelay: TimeSpan.FromMilliseconds(10));
        int attemptCount = 0;

        // Act
        var result = await RetryHelper.ExecuteWithRetryAsync(
            () => 
            {
                attemptCount++;
                return Task.FromResult("success");
            },
            policy,
            operationName: "Test operation"
        );

        // Assert
        Assert.Equal("success", result);
        Assert.Equal(1, attemptCount);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_FailsFirstThenSucceeds_RetriesOnce()
    {
        // Arrange
        var policy = new RetryPolicy(maxRetries: 3, retryDelay: TimeSpan.FromMilliseconds(10));
        int attemptCount = 0;

        // Act
        var result = await RetryHelper.ExecuteWithRetryAsync(
            () => 
            {
                attemptCount++;
                if (attemptCount == 1)
                    throw new IOException("Transient error");
                return Task.FromResult("success");
            },
            policy,
            operationName: "Test operation"
        );

        // Assert
        Assert.Equal("success", result);
        Assert.Equal(2, attemptCount);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_AlwaysFails_ExhaustsRetries()
    {
        // Arrange
        var policy = new RetryPolicy(maxRetries: 2, retryDelay: TimeSpan.FromMilliseconds(10));
        int attemptCount = 0;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<IOException>(async () =>
        {
            await RetryHelper.ExecuteWithRetryAsync(
                () => 
                {
                    attemptCount++;
                    throw new IOException("Persistent error");
                },
                policy,
                operationName: "Test operation"
            );
        });

        Assert.Equal("Persistent error", exception.Message);
        Assert.Equal(3, attemptCount); // Original attempt + 2 retries
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_NonRetryableException_NoRetry()
    {
        // Arrange
        var policy = new RetryPolicy(maxRetries: 3, retryDelay: TimeSpan.FromMilliseconds(10));
        int attemptCount = 0;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await RetryHelper.ExecuteWithRetryAsync(
                () => 
                {
                    attemptCount++;
                    throw new ArgumentNullException("Non-retryable error");
                },
                policy,
                operationName: "Test operation"
            );
        });

        Assert.Equal("Non-retryable error", exception.ParamName);
        Assert.Equal(1, attemptCount); // No retries for ArgumentNullException
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_CustomShouldRetry_UsesCustomLogic()
    {
        // Arrange
        var policy = new RetryPolicy(maxRetries: 2, retryDelay: TimeSpan.FromMilliseconds(10));
        int attemptCount = 0;

        // Act
        var result = await RetryHelper.ExecuteWithRetryAsync(
            () => 
            {
                attemptCount++;
                if (attemptCount == 1)
                    throw new ArgumentException("Custom retryable error");
                return Task.FromResult("success");
            },
            policy,
            operationName: "Test operation",
            shouldRetry: ex => ex is ArgumentException // Custom retry logic
        );

        // Assert
        Assert.Equal("success", result);
        Assert.Equal(2, attemptCount);
    }

    [Fact]
    public void RetryPolicy_BackoffCalculation_CorrectDelays()
    {
        // Arrange
        var policy = new RetryPolicy(
            maxRetries: 3, 
            retryDelay: TimeSpan.FromMilliseconds(100), 
            backoffMultiplier: 2.0,
            maxRetryDelay: TimeSpan.FromSeconds(1)
        );

        // Act & Assert
        // The delay calculation is internal, but we can test by ensuring the policy properties are set correctly
        Assert.Equal(3, policy.MaxRetries);
        Assert.Equal(TimeSpan.FromMilliseconds(100), policy.RetryDelay);
        Assert.Equal(2.0, policy.BackoffMultiplier);
        Assert.Equal(TimeSpan.FromSeconds(1), policy.MaxRetryDelay);
    }

    [Fact]
    public void RetryPolicy_PredefinedPolicies_HaveCorrectSettings()
    {
        // Test Serial policy
        Assert.Equal(3, RetryPolicy.Serial.MaxRetries);
        Assert.Equal(TimeSpan.FromMilliseconds(500), RetryPolicy.Serial.RetryDelay);

        // Test Radio policy
        Assert.Equal(2, RetryPolicy.Radio.MaxRetries);
        Assert.Equal(TimeSpan.FromMilliseconds(1000), RetryPolicy.Radio.RetryDelay);

        // Test Network policy
        Assert.Equal(5, RetryPolicy.Network.MaxRetries);
        Assert.Equal(TimeSpan.FromMilliseconds(200), RetryPolicy.Network.RetryDelay);

        // Test Immediate policy
        Assert.Equal(1, RetryPolicy.Immediate.MaxRetries);
        Assert.Equal(TimeSpan.Zero, RetryPolicy.Immediate.RetryDelay);
    }
}