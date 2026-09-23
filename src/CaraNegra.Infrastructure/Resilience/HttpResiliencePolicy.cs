using Polly;
using Polly.Extensions.Http;

namespace CaraNegra.Infrastructure.Resilience;

public static class HttpResiliencePolicy
{
    public static IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy(int maxRetries = 3)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: maxRetries,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timeSpan, retryCount, context) =>
                {
                    // Log retry attempt
                });
    }

    public static IAsyncPolicy<HttpResponseMessage> CreateTimeoutPolicy(TimeSpan timeout)
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(timeout);
    }

    public static IAsyncPolicy<HttpResponseMessage> CreateCombinedPolicy(
        int maxRetries = 3,
        TimeSpan? timeout = null)
    {
        var policies = new List<IAsyncPolicy<HttpResponseMessage>>
        {
            CreateRetryPolicy(maxRetries)
        };

        if (timeout.HasValue)
        {
            policies.Add(CreateTimeoutPolicy(timeout.Value));
        }

        return policies.Count > 1 
            ? Policy.WrapAsync(policies.ToArray())
            : policies[0];
    }
}