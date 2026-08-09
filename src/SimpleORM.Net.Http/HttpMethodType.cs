using System.Dynamic;


using System.Net.Http.Headers;


using System.Text;


using System.Text.Json;


using Microsoft.Extensions.DependencyInjection;


namespace SimpleORM.Net.Http;


/// <summary>HTTP method.</summary>
public enum HttpMethodType
{

    /// <summary>GET.</summary>
    Get,
    /// <summary>POST.</summary>
    Post,
    /// <summary>PUT.</summary>
    Put,
    /// <summary>PATCH.</summary>
    Patch,
    /// <summary>DELETE.</summary>
    Delete,
    /// <summary>HEAD.</summary>
    Head,
    /// <summary>OPTIONS.</summary>
    Options
}
