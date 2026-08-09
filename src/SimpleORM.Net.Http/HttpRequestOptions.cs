using System.Dynamic;


using System.Net.Http.Headers;


using System.Text;


using System.Text.Json;


using Microsoft.Extensions.DependencyInjection;


namespace SimpleORM.Net.Http;


/// <summary>HTTP request options.</summary>
public sealed class HttpRequestOptions
{

    /// <summary>URL.</summary>
    public string Url
    {
        get;

        set;

    }
    =string.Empty;


    /// <summary>Method.</summary>
    public HttpMethodType Method
    {
        get;

        set;

    }
    =HttpMethodType.Get;


    /// <summary>Body type.</summary>
    public HttpBodyType BodyType
    {
        get;

        set;

    }
    =HttpBodyType.Json;


    /// <summary>Payload.</summary>
    public object? Payload
    {
        get;

        set;

    }

    /// <summary>Headers.</summary>
    public IDictionary<string,string> Headers
    {
        get;

    }
    =new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);


    /// <summary>Query values.</summary>
    public IDictionary<string,string?> Query
    {
        get;

    }
    =new Dictionary<string,string?>();


    /// <summary>Bearer token.</summary>
    public string? BearerToken
    {
        get;

        set;

    }

    /// <summary>Timeout.</summary>
    public TimeSpan? Timeout
    {
        get;

        set;

    }

}
