using System.Dynamic;


using System.Net.Http.Headers;


using System.Text;


using System.Text.Json;


using Microsoft.Extensions.DependencyInjection;


namespace SimpleORM.Net.Http;


/// <summary>Body type.</summary>
public enum HttpBodyType
{

    /// <summary>None.</summary>
    None,
    /// <summary>JSON.</summary>
    Json,
    /// <summary>Form.</summary>
    FormUrlEncoded,
    /// <summary>Multipart.</summary>
    MultipartFormData,
    /// <summary>Text.</summary>
    Text,
    /// <summary>XML.</summary>
    Xml
}
