namespace Stalksville.Application;

/// <summary>Thrown when the Wolvesville API returns a non-success response.</summary>
public sealed class WolvesvilleApiException : Exception
{
    public int StatusCode { get; }

    public bool IsNotFound => StatusCode == 404;

    public bool IsUnauthorized => StatusCode is 401 or 403;

    public bool IsRateLimited => StatusCode == 429;

    public WolvesvilleApiException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}
