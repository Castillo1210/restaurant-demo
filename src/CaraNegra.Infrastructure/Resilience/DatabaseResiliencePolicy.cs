using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace CaraNegra.Infrastructure.Resilience;

public static class DatabaseResiliencePolicy
{
    public static AsyncRetryPolicy CreateRetryPolicy(int maxRetries = 3)
    {
        return Policy
            .Handle<DbUpdateException>()
            .Or<TimeoutException>()
            .WaitAndRetryAsync(
                retryCount: maxRetries,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    // Log retry attempt here if needed
                });
    }

    public static AsyncCircuitBreakerPolicy CreateCircuitBreakerPolicy(
        int exceptionsAllowedBeforeBreaking = 5,
        TimeSpan durationOfBreak = default)
    {
        return Policy
            .Handle<DbUpdateException>()
            .Or<TimeoutException>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: exceptionsAllowedBeforeBreaking,
                durationOfBreak: durationOfBreak == default ? TimeSpan.FromSeconds(30) : durationOfBreak,
                onBreak: (outcome, breakDuration) =>
                {
                    // Log circuit breaker opened
                },
                onReset: () =>
                {
                    // Log circuit breaker closed
                },
                onHalfOpen: () =>
                {
                    // Log circuit breaker half-open
                });
    }

    public static IAsyncPolicy CreateCombinedPolicy(int maxRetries = 3)
    {
        var retryPolicy = CreateRetryPolicy(maxRetries);
        var circuitBreakerPolicy = CreateCircuitBreakerPolicy();
        
        return Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);
    }
}