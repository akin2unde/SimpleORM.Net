using System.Dynamic;

using System.Net.Http.Headers;

using System.Text;

using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;

namespace SimpleORM.Net.Http;

/// <summary>HTTP registration.</summary>
public static class HttpRegistration
{

    /// <summary>Registers wrapper.</summary>
    public static IServiceCollection AddSimpleOrmHttp(this IServiceCollection s,Action<SimpleOrmHttpOptions>? configure=null)
    {
        var o=new SimpleOrmHttpOptions();

        configure?.Invoke(o);

        s.AddSingleton(o);

        s.AddHttpClient("SimpleORM.Net.Http");

        s.AddTransient<IHttpService,HttpService>();

        return s;

    }

}
