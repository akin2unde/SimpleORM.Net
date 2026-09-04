using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace SimpleORM.Net.AspNetCore;

/// <summary>
/// Provides extension methods and utility operations for files, streams, strings,
/// serialization, cryptography, dynamic objects, MongoDB filters, and object mapping.
/// </summary>
public static class ExtensionUtil
{
    /// <summary>
    /// Reads the complete contents of an uploaded form file into a byte array.
    /// </summary>
    /// <param name="i">The uploaded form file to read.</param>
    /// <returns>A byte array containing the file contents.</returns>
    public static byte[] ToByteArray(this IFormFile i)
    {
        using (BinaryReader sr = new BinaryReader(i.OpenReadStream()))
        {
            return sr.ReadBytes((int)i.Length);
        }
    }
    /// <summary>
    /// Reads all remaining data from a stream into a byte array.
    /// </summary>
    /// <param name="i">The stream to read from its current position.</param>
    /// <returns>A byte array containing the remaining stream data.</returns>
    public static byte[] ToByteArray(this Stream i)
    {
        byte[] bytes;
        List<byte> totalStream = new();
        byte[] buffer = new byte[32];
        int read;
        while ((read = i.Read(buffer, 0, buffer.Length)) > 0)
        {
            totalStream.AddRange(buffer.Take(read));
        }
        bytes = totalStream.ToArray();
        return bytes;
    }

    /// <summary>
    /// Determines whether a string is <see langword="null"/>, empty, or consists only of white-space characters.
    /// </summary>
    /// <param name="i">The string to evaluate.</param>
    /// <returns><see langword="true"/> when the string has no meaningful content; otherwise, <see langword="false"/>.</returns>
    public static bool IsEmpty(this string i)
    {
        return string.IsNullOrWhiteSpace(i);
    }
    /// <summary>
    /// Determines whether a string has a syntactically valid email-address format.
    /// </summary>
    /// <param name="i">The email address to validate.</param>
    /// <returns><see langword="true"/> when the value matches the supported email pattern; otherwise, <see langword="false"/>.</returns>
    public static bool IsValidMail(this string i)
    {
        var pattern = @"^[a-zA-Z0-9.!#$%&'*+-/=?^_`{|}~]+@[a-zA-Z0-9-]+(?:\.[a-zA-Z0-9-]+)*$";
        var regex = new Regex(pattern);
        return regex.IsMatch(i);
    }
    /// <summary>
    /// Compares two strings using ordinal, case-insensitive comparison rules.
    /// </summary>
    /// <param name="i">The first string.</param>
    /// <param name="anotherStr">The second string.</param>
    /// <returns><see langword="true"/> when both strings are equal ignoring case; otherwise, <see langword="false"/>.</returns>
    public static bool IsEqualIgnoreCase(this string i, string anotherStr)
    {
        return string.Equals(i, anotherStr, StringComparison.OrdinalIgnoreCase);
    }
    /// <summary>
    /// Converts URL escape sequences in a string to their unescaped representation.
    /// </summary>
    /// <param name="i">The escaped URL value.</param>
    /// <returns>The unescaped string.</returns>
    public static string URLUnescape(this string i)
    {
        var res = Uri.UnescapeDataString(i);
        return res;
    }

    /// <summary>
    /// Encrypts text using the utility's configured AES algorithm and returns Base64-encoded ciphertext.
    /// </summary>
    /// <param name="text">The plain text to encrypt.</param>
    /// <returns>The encrypted value encoded as Base64.</returns>
    public static string Encrypt(this string text)
    {
        string secret = "unitiSC01USP#@_5";
        byte[] key = Encoding.UTF8.GetBytes(secret);

        byte[] plainBytes = Encoding.UTF8.GetBytes(text);
        byte[]? encryptedBytes = null;
        // Set up the encryption objects
        using (Aes aes = Aes.Create())
        {
            aes.Key = key;
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.PKCS7;

            // Encrypt the input plaintext using the AES algorithm
            using (ICryptoTransform encryptor = aes.CreateEncryptor())
            {
                encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            }
        }
        var res = Convert.ToBase64String(encryptedBytes);
        return res;
    }

    /// <summary>
    /// Decrypts a Base64-encoded AES ciphertext value.
    /// </summary>
    /// <param name="text">The Base64-encoded ciphertext to decrypt.</param>
    /// <returns>The decrypted plain text.</returns>
    public static string Decrypt(this string text)
    {
        string secret = "hawkeEY01USP#@_5";
        byte[] key = Encoding.UTF8.GetBytes(secret);

        byte[] plainBytes = Convert.FromBase64String(text);
        byte[]? decryptedBytes = null;

        // Set up the encryption objects
        using (Aes aes = Aes.Create())
        {
            aes.Key = key;
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.PKCS7;

            // Decrypt the input ciphertext using the AES algorithm
            using (ICryptoTransform decryptor = aes.CreateDecryptor())
            {
                decryptedBytes = decryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            }
        }
        var res = Encoding.UTF8.GetString(decryptedBytes);
        return res;
    }

    // public static string key = "@bena_03";
    // public static string Encode(this string text)
    // {
    //     DESCryptoServiceProvider des = new DESCryptoServiceProvider();
    //     byte[] inputByteArray = Encoding.GetEncoding("UTF-8").GetBytes(text);
    //     des.Key = ASCIIEncoding.ASCII.GetBytes(key);
    //     des.IV = ASCIIEncoding.ASCII.GetBytes(key);
    //     MemoryStream ms = new MemoryStream();
    //     CryptoStream cs = new CryptoStream(ms, des.CreateEncryptor(), CryptoStreamMode.Write);

    //     cs.Write(inputByteArray, 0, inputByteArray.Length);
    //     cs.FlushFinalBlock();

    //     StringBuilder ret = new StringBuilder();
    //     foreach (byte b in ms.ToArray())
    //     {
    //         ret.AppendFormat("{0:X2}", b);
    //     }
    //     return ret.ToString();
    // }
    // public static string Decode(this string text)
    // {
    //     var des = new DESCryptoServiceProvider();
    //     byte[] inputByteArray = new byte[text.Length / 2];
    //     for (int x = 0; x < text.Length / 2; x++)
    //     {
    //         int i = (Convert.ToInt32(text.Substring(x * 2, 2), 16));
    //         inputByteArray[x] = (byte)i;
    //     }
    //     des.Key = ASCIIEncoding.ASCII.GetBytes(key);
    //     des.IV = ASCIIEncoding.ASCII.GetBytes(key);
    //     MemoryStream ms = new();
    //     CryptoStream cs = new CryptoStream(ms, des.CreateDecryptor(), CryptoStreamMode.Write);
    //     cs.Write(inputByteArray, 0, inputByteArray.Length);
    //     cs.FlushFinalBlock();
    //     StringBuilder ret = new();
    //     var res = System.Text.Encoding.Default.GetString(ms.ToArray());
    //     return res;
    // }
    /// <summary>
    /// Encodes a string using <c>Secure.Encode</c>.
    /// </summary>
    /// <param name="input">The value to encode.</param>
    /// <returns>The encoded value.</returns>
    public static string Encode(this string input)
    {
        return Secure.Encode(input);
    }
    /// <summary>
    /// Decodes a value previously encoded by <see cref="Encode(string)"/>.
    /// </summary>
    /// <param name="input">The encoded value.</param>
    /// <returns>The decoded string.</returns>
    public static string Decode(this string input)
    {
        return Secure.Decode(input);
    }
    /// <summary>
    /// Determines whether an HTTP request appears to originate from a mobile device based on its user-agent header.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns><see langword="true"/> when a known mobile-device identifier is found; otherwise, <see langword="false"/>.</returns>
    public static bool IsMobileDevice(this HttpContext context)
    {
        string userAgent = context.Request.Headers.UserAgent.ToString();
        if (string.IsNullOrEmpty(userAgent))
        {
            return false;
        }
        userAgent = userAgent.ToLower();
        // Check for common mobile device keywords
        if (userAgent.Contains("android") ||
            userAgent.Contains("iphone") ||
            userAgent.Contains("ipad") ||
            userAgent.Contains("windows phone") ||
            userAgent.Contains("mobile") ||
            userAgent.Contains("opera mini") ||
            userAgent.Contains("blackberry"))
        {
            return true;
        }

        // More advanced checks might involve screen size or other headers
        // For example, checking for specific `HTTP_X_WAP_PROFILE` or `HTTP_ACCEPT` headers, 
        // though these are less common in modern mobile browsers.

        return false;
    }
    /// <summary>
    /// Produces a secure hashed representation of a string.
    /// </summary>
    /// <param name="original">The value to hash.</param>
    /// <returns>The hashed value produced by <c>Secure.GetHashedValue</c>.</returns>
    public static string Hash(string original)
    {
        return Secure.GetHashedValue(original);
    }

    /// <summary>
    /// Creates a deep copy of an object by serializing and deserializing it as JSON.
    /// </summary>
    /// <typeparam name="T">The object type.</typeparam>
    /// <param name="source">The object to clone.</param>
    /// <returns>A deserialized copy of the source object.</returns>
    public static T? Clone<T>(this T source)
    {
        var serialized = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<T>(serialized);
    }

    /// <summary>
    /// Converts a value to a specified runtime type.
    /// </summary>
    /// <param name="obj">The value to convert.</param>
    /// <param name="castTo">The destination type.</param>
    /// <returns>The converted value.</returns>
    public static dynamic CastType(dynamic obj, Type castTo)
    {
        return Convert.ChangeType(obj, castTo);
    }
    /// <summary>
    /// Serializes an object to JSON while ignoring reference cycles.
    /// </summary>
    /// <param name="obj">The object to serialize.</param>
    /// <returns>The JSON representation of the object.</returns>
    public static string SerializeObject(this object obj)
    {
        // var json = Newtonsoft.Json.JsonConvert.SerializeObject(obj, Formatting.Indented,
        //      new JsonSerializerSettings()
        //      {
        //          ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore,
        //          NullValueHandling = NullValueHandling.Include
        //      }
        //  );
        var json = System.Text.Json.JsonSerializer.Serialize(obj, new JsonSerializerOptions { ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles });
        return json;
    }

    /// <summary>
    /// Adds a property to an <see cref="ExpandoObject"/> or replaces its existing value.
    /// </summary>
    /// <param name="expando">The dynamic object to modify.</param>
    /// <param name="propertyName">The property name.</param>
    /// <param name="value">The property value.</param>
    public static void AddProperty(this ExpandoObject expando, string propertyName, object value)
    {
        // ExpandoObject supports IDictionary so we can extend it like this
        var expandoDict = expando as IDictionary<string, object>;
        if (expandoDict.ContainsKey(propertyName))
            expandoDict[propertyName] = value;
        else
            expandoDict.Add(propertyName, value);
    }

    /// <summary>
    /// Calculates a deterministic non-cryptographic hash code for a string.
    /// </summary>
    /// <param name="text">The string to hash.</param>
    /// <returns>The calculated integer hash represented as a string.</returns>
    public static string GetHashString(this string text)
    {
        unchecked
        {
            int hash = 23;
            foreach (char c in text)
            {
                hash = hash * 31 + c;
            }
            return hash.ToString();
        }
    }
    /// <summary>
    /// Returns the conventional default SQL date value, 1 January 1900 at midnight.
    /// </summary>
    /// <param name="sqlDateTime">The date value on which the extension is invoked. The value is not used.</param>
    /// <returns>A <see cref="DateTime"/> representing 1 January 1900 at 00:00:00.</returns>
    public static DateTime Default(this DateTime sqlDateTime)
    {

        return new DateTime(1900, 01, 01, 00, 00, 00);
    }

    /// <summary>
    /// Returns the final tick of the specified calendar day.
    /// </summary>
    /// <param name="date">The date whose end-of-day value is required.</param>
    /// <returns>The last possible <see cref="DateTime"/> value within the specified day.</returns>
    public static DateTime EndOfDay(this DateTime date)
    {
        // date = date.Hour > 0 && date.Hour <= 23 ? date.AddHours(23 - date.Hour).AddMinutes(60 - date.Minute).AddSeconds(60 - date.Second) : date.Date.AddDays(1).AddTicks(-1);
        // return date;
        return date.Date.AddDays(1).AddTicks(-1);
    }

    /// <summary>
    /// Encrypts text with AES using a Base64-encoded key.
    /// </summary>
    /// <param name="input">The plain text to encrypt.</param>
    /// <param name="privateKey">The AES key encoded as Base64.</param>
    /// <returns>The encrypted value encoded as Base64.</returns>
    public static string AESEncrypt(this string input, string privateKey)
    {
        byte[] key = Convert.FromBase64String(privateKey);
        byte[] plainBytes = Encoding.UTF8.GetBytes(input);
        byte[]? encryptedBytes = null;
        // Set up the encryption objects
        using (Aes aes = Aes.Create())
        {
            aes.Key = key;
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.PKCS7;

            // Encrypt the input plaintext using the AES algorithm
            using (ICryptoTransform encryptor = aes.CreateEncryptor())
            {
                encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            }
        }
        var resStr = Convert.ToBase64String(encryptedBytes);
        return resStr;
    }

    /// <summary>
    /// Decrypts Base64-encoded AES ciphertext using a Base64-encoded key.
    /// </summary>
    /// <param name="input">The ciphertext encoded as Base64.</param>
    /// <param name="privateKey">The AES key encoded as Base64.</param>
    /// <returns>The decrypted plain text.</returns>
    public static string AESDecrypt(this string input, string privateKey)
    {
        var cipherBytes = Convert.FromBase64String(input);
        byte[] key = Convert.FromBase64String(privateKey);
        byte[]? decryptedBytes = null;
        // Set up the encryption objects
        using (Aes aes = Aes.Create())
        {
            aes.Key = key;
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.PKCS7;

            // Decrypt the input ciphertext using the AES algorithm
            using (ICryptoTransform decryptor = aes.CreateDecryptor())
            {
                decryptedBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            }
        }
        var resStr = Encoding.UTF8.GetString(decryptedBytes);
        return resStr;
    }
    /// <summary>
    /// Encrypts text with an RSA public key represented in XML format.
    /// </summary>
    /// <param name="text">The plain text to encrypt.</param>
    /// <param name="publicKey">The RSA public key in XML format.</param>
    /// <returns>The RSA-encrypted value encoded as Base64.</returns>
    public static string RSAEncrypt(this string text, string publicKey)
    {
        using RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
        rsa.FromXmlString(publicKey);
        byte[] dataToEncrypt = Encoding.UTF8.GetBytes(text);
        byte[] encryptedData = rsa.Encrypt(dataToEncrypt, false);
        return Convert.ToBase64String(encryptedData);
    }
    /// <summary>
    /// Decrypts Base64-encoded RSA ciphertext with an XML-formatted private key.
    /// </summary>
    /// <param name="encryptedText">The RSA-encrypted value encoded as Base64.</param>
    /// <param name="privateKey">The RSA private key in XML format.</param>
    /// <returns>The decrypted plain text.</returns>
    public static string RSADecrypt(this string encryptedText, string privateKey)
    {
        using RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
        rsa.FromXmlString(privateKey);
        byte[] encryptedData = Convert.FromBase64String(encryptedText);
        byte[] decryptedData = rsa.Decrypt(encryptedData, false);
        return Encoding.UTF8.GetString(decryptedData);
    }
    /// <summary>
    /// Copies the public properties of an object into an <see cref="ExpandoObject"/>.
    /// </summary>
    /// <param name="obj">The source object.</param>
    /// <returns>An expandable object containing the source properties, or <see langword="null"/> when the source is <see langword="null"/>.</returns>
    public static dynamic? ToExpando(this object obj)
    {
        if (obj == null) return null;
        var expando = new ExpandoObject();
        var expandoDict = expando as IDictionary<string, object?>;
        // Fetch properties via reflection/TypeDescriptor
        foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(obj.GetType()))
        {
            var value = property.GetValue(obj);
            expandoDict.Add(property.Name, value);
        }
        return expando;
    }

    /// <summary>
    /// Maps readable source properties to writable destination properties with matching names and assignable types.
    /// </summary>
    /// <typeparam name="TDestination">The destination reference type. It must have a parameterless constructor.</typeparam>
    /// <param name="source">The source object to map.</param>
    /// <returns>A new destination instance populated with compatible source-property values.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    public static TDestination MapTo<TDestination>(
   this object source)
   where TDestination : class, new()
    {
        ArgumentNullException.ThrowIfNull(source);

        var destination = new TDestination();

        var sourceProperties = source
            .GetType()
            .GetProperties()
            .Where(x => x.CanRead)
            .GroupBy(x => x.Name)
            .ToDictionary(
                x => x.Key,
                x => x.First());

        var destinationProperties = typeof(TDestination)
            .GetProperties()
            .Where(x => x.CanWrite)
            .GroupBy(x => x.Name)
            .ToDictionary(
                x => x.Key,
                x => x.First());

        foreach (var sourceProperty in sourceProperties.Values)
        {
            if (!destinationProperties.TryGetValue(
                    sourceProperty.Name,
                    out var destinationProperty))
            {
                continue;
            }

            if (!destinationProperty.PropertyType
                    .IsAssignableFrom(sourceProperty.PropertyType))
            {
                continue;
            }

            var value = sourceProperty.GetValue(source);

            destinationProperty.SetValue(
                destination,
                value);
        }

        return destination;
    }

    /// <summary>
    /// Determines whether an object's runtime type is decorated with a specified attribute.
    /// </summary>
    /// <typeparam name="TAttribute">The attribute type to locate.</typeparam>
    /// <param name="value">The object whose runtime type will be inspected.</param>
    /// <returns><see langword="true"/> when the attribute is defined directly or inherited; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    public static bool HasAttribute<TAttribute>(this object value)
      where TAttribute : Attribute
    {
        ArgumentNullException.ThrowIfNull(value);

        return value.GetType().IsDefined(
            typeof(TAttribute),
            inherit: true);
    }

}