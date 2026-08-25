using System.Collections;

using System.Dynamic;

using System.Globalization;

using System.Net.Http.Headers;

using System.Text;

using System.Text.Json;

using System.Xml.Serialization;

namespace SimpleORM.Net.Http;

/// <summary>
/// Sends outbound HTTP requests using <see cref="IHttpClientFactory"/> and converts responses to the requested type.
/// </summary>
public sealed class HttpService : IHttpService
{

    private const string ClientName = "SimpleORM.Net.Http";

    private readonly IHttpClientFactory _httpClientFactory;

    private readonly SimpleOrmHttpOptions _options;

    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="options">SimpleORM.Net HTTP options.</param>
    public HttpService(IHttpClientFactory httpClientFactory, SimpleOrmHttpOptions options)

    {

        _httpClientFactory = httpClientFactory;

        _options = options;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }
        ;

    }

    /// <inheritdoc />
    public async Task<HttpResponse<T>> Send<T>(
    HttpRequestOptions request,
    CancellationToken cancellationToken = default)

    {

        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Url))

        {

            throw new ArgumentException("The request URL is required.", nameof(request));

        }

        try

        {

            using var message = BuildRequestMessage(request);

            using var timeoutSource = CreateTimeoutSource(request.Timeout, cancellationToken);

            var client = _httpClientFactory.CreateClient(ClientName);

            using var response = await client.SendAsync(
            message,
            HttpCompletionOption.ResponseHeadersRead,
            timeoutSource.Token).ConfigureAwait(false);

            var rawResponse = response.Content is null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(timeoutSource.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode && _options.ThrowOnError)

            {

                throw new HttpRequestException(
                $"HTTP request failed with status code {(int)response.StatusCode} ({response.ReasonPhrase}).");

            }

            var data = response.IsSuccessStatusCode
            ? Deserialize<T>(rawResponse, response.Content?.Headers.ContentType?.MediaType)
            : default;

            return new HttpResponse<T>

            {

                Success = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                Data = data,
                RawResponse = rawResponse,
                Error = response.IsSuccessStatusCode ? null : rawResponse,
                Headers = ReadHeaders(response)

            }
            ;

        }

        catch (Exception exception) when (!_options.ThrowOnError && exception is not OperationCanceledException)

        {

            return new HttpResponse<T>

            {

                Success = false,
                StatusCode = 0,
                Error = exception.Message

            }
            ;

        }

        catch (OperationCanceledException exception)
        when (!cancellationToken.IsCancellationRequested && !_options.ThrowOnError)

        {

            return new HttpResponse<T>

            {

                Success = false,
                StatusCode = 0,
                Error = $"HTTP request timed out. {exception.Message}"

            }
            ;

        }

    }

    /// <inheritdoc />
    public async Task<HttpResponse<dynamic>> SendDynamic(
    HttpRequestOptions request,
    CancellationToken cancellationToken = default)

    {

        var response = await Send<ExpandoObject>(request, cancellationToken).ConfigureAwait(false);

        return new HttpResponse<dynamic>

        {

            Success = response.Success,
            StatusCode = response.StatusCode,
            Data = response.Data,
            RawResponse = response.RawResponse,
            Error = response.Error,
            Headers = response.Headers

        }
        ;

    }

    private HttpRequestMessage BuildRequestMessage(HttpRequestOptions request)

    {

        var message = new HttpRequestMessage(MapMethod(request.Method), BuildUri(request.Url, request.Query));

        message.Content = BuildContent(request);

        if (!string.IsNullOrWhiteSpace(request.BearerToken))

        {

            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.BearerToken);

        }

        foreach (var header in _options.DefaultHeaders)

        {

            AddHeader(message, header.Key, header.Value);

        }

        foreach (var header in request.Headers)

        {

            message.Headers.Remove(header.Key);

            message.Content?.Headers.Remove(header.Key);

            AddHeader(message, header.Key, header.Value);

        }

        return message;

    }

    private HttpContent? BuildContent(HttpRequestOptions request)

    {

        if (request.BodyType == HttpBodyType.None || request.Payload is null)

        {

            return null;

        }

        return request.BodyType switch

        {

            HttpBodyType.Json => new StringContent(
            JsonSerializer.Serialize(request.Payload, request.Payload.GetType(), _jsonOptions),
            Encoding.UTF8,
            "application/json"),
            HttpBodyType.Text => new StringContent(
            Convert.ToString(request.Payload, CultureInfo.InvariantCulture) ?? string.Empty,
            Encoding.UTF8,
            "text/plain"),
            HttpBodyType.Xml => new StringContent(
            SerializeXml(request.Payload),
            Encoding.UTF8,
            "application/xml"),
            HttpBodyType.FormUrlEncoded => new FormUrlEncodedContent(ToStringPairs(request.Payload)),
            HttpBodyType.MultipartFormData => BuildMultipartContent(request.Payload),
            _ => throw new NotSupportedException($"HTTP body type '{request.BodyType}' is not supported.")

        }
        ;

    }

    private static MultipartFormDataContent BuildMultipartContent(object payload)

    {

        var content = new MultipartFormDataContent();

        foreach (var pair in ToObjectPairs(payload))

        {

            if (pair.Value is null)

            {

                content.Add(new StringContent(string.Empty), pair.Key);

            }

            else if (pair.Value is HttpContent httpContent)

            {

                content.Add(httpContent, pair.Key);

            }

            else if (pair.Value is byte[] bytes)

            {

                content.Add(new ByteArrayContent(bytes), pair.Key, pair.Key);

            }

            else if (pair.Value is Stream stream)

            {

                content.Add(new StreamContent(stream), pair.Key, pair.Key);

            }

            else

            {

                content.Add(
                new StringContent(Convert.ToString(pair.Value, CultureInfo.InvariantCulture) ?? string.Empty),
                pair.Key);

            }

        }

        return content;

    }

    private static IEnumerable<KeyValuePair<string, string>> ToStringPairs(object payload)

    {

        foreach (var pair in ToObjectPairs(payload))

        {

            yield return new KeyValuePair<string, string>(
            pair.Key,
            Convert.ToString(pair.Value, CultureInfo.InvariantCulture) ?? string.Empty);

        }

    }

    private static IEnumerable<KeyValuePair<string, object?>> ToObjectPairs(object payload)

    {

        if (payload is IDictionary<string, object?> genericDictionary)

        {

            foreach (var pair in genericDictionary)

            {

                yield return pair;

            }

            yield break;

        }

        if (payload is IDictionary dictionary)

        {

            foreach (DictionaryEntry entry in dictionary)

            {

                if (entry.Key is not null)

                {

                    yield return new KeyValuePair<string, object?>(entry.Key.ToString()!, entry.Value);

                }

            }

            yield break;

        }

        foreach (var property in payload.GetType().GetProperties())

        {

            if (property.CanRead)

            {

                yield return new KeyValuePair<string, object?>(property.Name, property.GetValue(payload));

            }

        }

    }

    private static string SerializeXml(object payload)

    {

        var serializer = new XmlSerializer(payload.GetType());

        using var writer = new StringWriter(CultureInfo.InvariantCulture);

        serializer.Serialize(writer, payload);

        return writer.ToString();

    }

    private T? Deserialize<T>(string rawResponse, string? mediaType)

    {

        if (typeof(T) == typeof(string))

        {

            return (T)(object)rawResponse;

        }

        if (string.IsNullOrWhiteSpace(rawResponse))

        {

            return default;

        }

        if (string.Equals(mediaType, "application/xml", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(mediaType, "text/xml", StringComparison.OrdinalIgnoreCase))

        {

            var serializer = new XmlSerializer(typeof(T));

            using var reader = new StringReader(rawResponse);

            return (T?)serializer.Deserialize(reader);

        }

        return JsonSerializer.Deserialize<T>(rawResponse, _jsonOptions);

    }

    private static HttpMethod MapMethod(HttpMethodType method) => method switch

    {

        HttpMethodType.Get => HttpMethod.Get,
        HttpMethodType.Post => HttpMethod.Post,
        HttpMethodType.Put => HttpMethod.Put,
        HttpMethodType.Patch => HttpMethod.Patch,
        HttpMethodType.Delete => HttpMethod.Delete,
        HttpMethodType.Head => HttpMethod.Head,
        HttpMethodType.Options => HttpMethod.Options,
        _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Unknown HTTP method.")

    }
    ;

    private static Uri BuildUri(string url, IDictionary<string, string?> query)

    {

        var builder = new UriBuilder(url);

        var values = new List<string>();

        if (!string.IsNullOrWhiteSpace(builder.Query))

        {

            values.Add(builder.Query.TrimStart('?'));

        }

        values.AddRange(
        query
        .Where(x => x.Value is not null)
        .Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value!)}"));

        builder.Query = string.Join("&", values.Where(x => !string.IsNullOrWhiteSpace(x)));

        return builder.Uri;

    }

    private static void AddHeader(HttpRequestMessage message, string name, string value)

    {

        if (!message.Headers.TryAddWithoutValidation(name, value) && message.Content is not null)

        {

            message.Content.Headers.TryAddWithoutValidation(name, value);

        }

    }

    private CancellationTokenSource CreateTimeoutSource(TimeSpan? requestedTimeout, CancellationToken cancellationToken)

    {

        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var timeout = requestedTimeout ?? _options.DefaultTimeout;

        if (timeout > TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)

        {

            source.CancelAfter(timeout);

        }

        return source;

    }

    private static IReadOnlyDictionary<string, IEnumerable<string>> ReadHeaders(HttpResponseMessage response)

    {

        var headers = new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in response.Headers)

        {

            headers[header.Key] = header.Value;

        }

        if (response.Content is not null)

        {

            foreach (var header in response.Content.Headers)

            {

                headers[header.Key] = header.Value;

            }

        }

        return headers;

    }

}
