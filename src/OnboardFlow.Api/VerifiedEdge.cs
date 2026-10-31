using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.Features;

namespace OnboardFlow.Api;

public sealed record VerifiedCaller(string Network);

public static class VerifiedEdge
{
    // Internal origin query metadata is covered by the existing method/target/body signature.
    // The Worker overwrites it, so callers cannot choose their own bucket. Old app images ignore it.
    internal const string NetworkQuery = "__of_network";
    private const string TimeHeader = "X-OnboardFlow-Edge-Time";
    private const string SignatureHeader = "X-OnboardFlow-Edge-Signature";
    private const int MaxBodyBytes = 16384;

    public static IApplicationBuilder UseVerifiedEdge(this IApplicationBuilder app)
    {
        var settings = app.ApplicationServices.GetRequiredService<IConfiguration>().GetSection("VerifiedEdge");
        if (!settings.GetValue<bool>("Enabled")) return app;
        var origin = settings["PublicOrigin"] ?? "";
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var address) || address.Scheme != "https"
            || address.UserInfo.Length != 0 || address.AbsolutePath != "/" || address.Query.Length != 0
            || address.Fragment.Length != 0 || address.GetLeftPart(UriPartial.Authority) != origin)
            throw new InvalidOperationException("Verified edge requires a canonical HTTPS origin.");
        var publicKey = Convert.FromBase64String(settings["PublicKey"] ?? "");
        using (var check = ECDsa.Create()) check.ImportSubjectPublicKeyInfo(publicKey, out _);

        return app.Use(async (http, next) =>
        {
            var timestamp = http.Request.Headers[TimeHeader];
            var signature = http.Request.Headers[SignatureHeader];
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var valid = false;
            if (timestamp.Count == 1 && signature.Count == 1 && signature[0]?.Length == 88
                && long.TryParse(timestamp[0], NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
                && seconds >= now - 90 && seconds <= now + 90)
            {
                http.Request.EnableBuffering(bufferThreshold: MaxBodyBytes, bufferLimit: MaxBodyBytes + 1);
                var body = new byte[MaxBodyBytes + 1];
                var length = 0;
                while (length < body.Length)
                {
                    var read = await http.Request.Body.ReadAsync(body.AsMemory(length), http.RequestAborted);
                    if (read == 0) break;
                    length += read;
                }
                http.Request.Body.Position = 0;
                if (length > MaxBodyBytes)
                {
                    http.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                    return;
                }
                var target = http.Features.Get<IHttpRequestFeature>()?.RawTarget;
                if (string.IsNullOrEmpty(target)) target = http.Request.GetEncodedPathAndQuery();
                var digest = Convert.ToHexStringLower(SHA256.HashData(body.AsSpan(0, length)));
                var canonical = string.Join('\n', "v1", timestamp[0], origin, http.Request.Method, target, digest);
                try
                {
                    using var verifier = ECDsa.Create();
                    verifier.ImportSubjectPublicKeyInfo(publicKey, out _);
                    valid = verifier.VerifyData(Encoding.UTF8.GetBytes(canonical), Convert.FromBase64String(signature[0]!),
                        HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
                }
                catch (Exception error) when (error is FormatException or CryptographicException) { }
            }
            http.Request.Headers.Remove(TimeHeader);
            http.Request.Headers.Remove(SignatureHeader);
            if (!valid)
            {
                http.Response.Headers.CacheControl = "no-store";
                if (HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method))
                    http.Response.Redirect(origin + http.Request.GetEncodedPathAndQuery(), permanent: true, preserveMethod: true);
                else http.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            // HTTPS is attested by the edge signature, bound to this method, target and body.
            // It does not depend on a shared host's rotating private ingress addresses.
            if (ProtectedRequests.Includes(http.Request.Path))
            {
                var network = http.Request.Query[NetworkQuery];
                if (network.Count != 1 || network[0] is not { Length: 64 } value || !value.All(Uri.IsHexDigit))
                {
                    http.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }
                http.Features.Set(new VerifiedCaller(value.ToLowerInvariant()));
            }
            http.Request.Scheme = "https";
            await next(http);
        });
    }
}
