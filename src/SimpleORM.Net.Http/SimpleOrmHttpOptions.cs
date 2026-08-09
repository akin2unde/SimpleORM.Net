namespace SimpleORM.Net.Http;


/// <summary>Configures the SimpleORM.Net outbound HTTP wrapper.</summary>
public sealed class SimpleOrmHttpOptions
{

    /// <summary>Gets or sets the default request timeout.</summary>
    public TimeSpan DefaultTimeout
    {
        get;

        set;


    }
    = TimeSpan.FromSeconds(30);


    /// <summary>Gets or sets a value indicating whether HTTP and transport failures should throw exceptions.</summary>
    public bool ThrowOnError
    {
        get;

        set;


    }

    /// <summary>Gets headers applied to every request unless overridden by request-specific headers.</summary>
    public IDictionary<string, string> DefaultHeaders
    {
        get;


    }
    =
    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);


}
