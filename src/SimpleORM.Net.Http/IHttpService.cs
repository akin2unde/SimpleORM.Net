using System.Dynamic;


using System.Net.Http.Headers;


using System.Text;


using System.Text.Json;


using Microsoft.Extensions.DependencyInjection;


namespace SimpleORM.Net.Http;


/// <summary>HTTP wrapper.</summary>
public interface IHttpService
{

    /// <summary>Send typed.</summary>
    Task<HttpResponse<T>> Send<T>(HttpRequestOptions request,CancellationToken cancellationToken=default);


    /// <summary>Send dynamic.</summary>
    Task<HttpResponse<dynamic>> SendDynamic(HttpRequestOptions request,CancellationToken cancellationToken=default);


}
