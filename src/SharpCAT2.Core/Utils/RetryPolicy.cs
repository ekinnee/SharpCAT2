using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace SharpCAT2.Core.Utils;

/// <summary>
/// Retry policy configuration for handling transient failures
/// </summary>
public class RetryPolicy
{
    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    public int MaxRetries { get; }
    
    /// <summary>
    /// Delay between retry attempts
    /// </summary>
    public TimeSpan RetryDelay { get; }
    
    /// <summary>
    /// Exponential backoff multiplier (1.0 = no backoff, 2.0 = double each time)
    /// </summary>
    public double BackoffMultiplier { get; }
    
    /// <summary>
    /// Maximum delay between retries (prevents infinite growth with backoff)
    /// </summary>
    public TimeSpan MaxRetryDelay { get; }

    /// <summary>
    /// Default retry policy for serial operations
    /// </summary>
    public static readonly RetryPolicy Serial = new(
        maxRetries: 3, 
        retryDelay: TimeSpan.FromMilliseconds(500), 
        backoffMultiplier: 1.5,
        maxRetryDelay: TimeSpan.FromSeconds(5)
    );

    /// <summary>
    /// Default retry policy for radio operations
    /// </summary>
    public static readonly RetryPolicy Radio = new(
        maxRetries: 2, 
        retryDelay: TimeSpan.FromMilliseconds(1000), 
        backoffMultiplier: 2.0,
        maxRetryDelay: TimeSpan.FromSeconds(10)
    );

    /// <summary>
    /// Default retry policy for network operations
    /// </summary>
    public static readonly RetryPolicy Network = new(
        maxRetries: 5, 
        retryDelay: TimeSpan.FromMilliseconds(200), 
        backoffMultiplier: 1.5,
        maxRetryDelay: TimeSpan.FromSeconds(3)
    );

    /// <summary>
    /// Immediate retry policy (no delay)
    /// </summary>
    public static readonly RetryPolicy Immediate = new(
        maxRetries: 1, 
        retryDelay: TimeSpan.Zero, 
        backoffMultiplier: 1.0,
        maxRetryDelay: TimeSpan.Zero
    );

    public RetryPolicy(int maxRetries, TimeSpan retryDelay, double backoffMultiplier = 1.0, TimeSpan? maxRetryDelay = null)
    {
        MaxRetries = Math.Max(0, maxRetries);
        RetryDelay = retryDelay;
        BackoffMultiplier = Math.Max(1.0, backoffMultiplier);
        MaxRetryDelay = maxRetryDelay ?? TimeSpan.FromMinutes(1);
    }
}

/// <summary>
/// Utility class for executing operations with retry logic and error recovery
/// </summary>
public static class RetryHelper
{
    /// <summary>
    /// Executes an operation with retry logic
    /// </summary>
    /// <typeparam name="T">Return type of the operation</typeparam>
    /// <param name="operation">Operation to execute</param>
    /// <param name="policy">Retry policy to use</param>
    /// <param name="logger">Logger for recording retry attempts</param>
    /// <param name="operationName">Name of the operation for logging</param>
    /// <param name="shouldRetry">Function to determine if exception should trigger retry</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the operation</returns>
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        RetryPolicy policy,
        ILogger? logger = null,
        string operationName = "Operation",
        Func<Exception, bool>? shouldRetry = null,
        CancellationToken cancellationToken = default)
    {
        shouldRetry ??= DefaultShouldRetry;
        var sw = Stopwatch.StartNew();
        Exception? lastException = null;

        for (int attempt = 0; attempt <= policy.MaxRetries; attempt++)
        {
            try
            {
                var result = await operation();
                
                if (attempt > 0)
                {
                    logger?.LogInformation("{OperationName} succeeded after {Attempts} attempts in {ElapsedMs}ms", 
                        operationName, attempt + 1, sw.ElapsedMilliseconds);
                }
                
                return result;
            }
            catch (Exception ex) when (attempt < policy.MaxRetries && shouldRetry(ex) && !cancellationToken.IsCancellationRequested)
            {
                lastException = ex;
                var delay = CalculateDelay(policy, attempt);
                
                logger?.LogWarning(ex, "{OperationName} failed on attempt {Attempt}/{MaxAttempts}, retrying in {DelayMs}ms: {Error}", 
                    operationName, attempt + 1, policy.MaxRetries + 1, delay.TotalMilliseconds, ex.Message);
                
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }

        logger?.LogError(lastException, "{OperationName} failed after {MaxAttempts} attempts in {ElapsedMs}ms", 
            operationName, policy.MaxRetries + 1, sw.ElapsedMilliseconds);
        
        throw lastException ?? new InvalidOperationException($"{operationName} failed without exception");
    }

    /// <summary>
    /// Executes a void operation with retry logic
    /// </summary>
    public static async Task ExecuteWithRetryAsync(
        Func<Task> operation,
        RetryPolicy policy,
        ILogger? logger = null,
        string operationName = "Operation",
        Func<Exception, bool>? shouldRetry = null,
        CancellationToken cancellationToken = default)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            await operation();
            return true;
        }, policy, logger, operationName, shouldRetry, cancellationToken);
    }

    /// <summary>
    /// Executes a synchronous operation with retry logic
    /// </summary>
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<T> operation,
        RetryPolicy policy,
        ILogger? logger = null,
        string operationName = "Operation",
        Func<Exception, bool>? shouldRetry = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(() => Task.FromResult(operation()), policy, logger, operationName, shouldRetry, cancellationToken);
    }

    /// <summary>
    /// Calculates the delay for a given attempt based on the retry policy
    /// </summary>
    private static TimeSpan CalculateDelay(RetryPolicy policy, int attempt)
    {
        if (policy.RetryDelay == TimeSpan.Zero)
            return TimeSpan.Zero;

        var delay = TimeSpan.FromMilliseconds(
            policy.RetryDelay.TotalMilliseconds * Math.Pow(policy.BackoffMultiplier, attempt)
        );

        return delay > policy.MaxRetryDelay ? policy.MaxRetryDelay : delay;
    }

    /// <summary>
    /// Default logic for determining if an exception should trigger a retry.
    /// Handles both network and serial communication errors with appropriate retry logic.
    /// </summary>
    private static bool DefaultShouldRetry(Exception ex)
    {
        return ex switch
        {
            // Network-related exceptions that are typically transient
            System.Net.Sockets.SocketException => true,
            System.IO.IOException => true,
            TimeoutException => true,
            
            // Serial port specific transient issues
            InvalidOperationException when ex.Message.Contains("port is closed") => true,
            InvalidOperationException when ex.Message.Contains("port is not open") => true,
            InvalidOperationException => true, // Other InvalidOperation exceptions might be transient
            
            // Permanent issues - don't retry these
            UnauthorizedAccessException => false, // Don't retry permission errors
            
            // Task-related exceptions
            TaskCanceledException => false,
            OperationCanceledException => false,
            
            // Argument exceptions shouldn't be retried
            ArgumentNullException => false,
            ArgumentException => false,
            
            _ => false // Conservative approach - don't retry unknown exceptions
        };
    }
}