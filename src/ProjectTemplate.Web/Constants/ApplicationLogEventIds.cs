namespace ProjectTemplate.Web.Constants;

/// <summary>
/// Defines application log event identifiers.
/// </summary>
internal static class ApplicationLogEventIds
{
    internal const int UnhandledExceptionRoutedToErrorPage = 6000;
    internal const int StatusCodePageRoutedToErrorPage = 6001;

    internal const int RateLimitClientPartitionFallback = 6002;

    internal const int RateLimitRejectedRequest = 6100;
}
