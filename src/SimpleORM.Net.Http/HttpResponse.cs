namespace SimpleORM.Net.Http;


/// <summary>Represents a typed outbound HTTP response.</summary>
/// <typeparam name="T">The expected response type.</typeparam>
public sealed class HttpResponse<T>
{

    /// <summary>Gets a value indicating whether the HTTP status code represents success.</summary>
    public bool Success
    {
        get;

        init;


    }

    /// <summary>Gets the numeric HTTP status code, or zero when no response was received.</summary>
    public int StatusCode
    {
        get;

        init;


    }

    /// <summary>Gets the deserialized response.</summary>
    public T? Data
    {
        get;

        init;


    }

    /// <summary>Gets the raw response body.</summary>
    public string? RawResponse
    {
        get;

        init;


    }

    /// <summary>Gets an error description when the request was unsuccessful.</summary>
    public string? Error
    {
        get;

        init;


    }

    /// <summary>Gets the response and content headers.</summary>
    public IReadOnlyDictionary<string, IEnumerable<string>> Headers
    {
        get;

        init;


    }
    =
    new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase);


}
