using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace OnboardFlow.Api;

public sealed record OwnerGrant(string Id, DateTimeOffset? ExpiresAt);

public sealed class OwnerAccess(IConfiguration configuration, TimeProvider clock)
{
    public const string CookieName = "__Host-onboardflow.owner";
    // Domain separation prevents an ordinary signed gateway request becoming an owner pass.
    internal const string Purpose = "onboardflow-owner-runs-v1";
    internal const string PermanentPurpose = "onboardflow-owner-runs-v2";

    public OwnerGrant? Read(string? pass)
    {
        if (pass is null || pass.Length > 256) return null;
        var parts = pass.Split('.');
        if (parts.Length != 4 || parts[2].Length != 32 || !parts[2].All(Uri.IsHexDigit)) return null;
        try
        {
            DateTimeOffset? expires = null;
            string purpose;
            if (parts[0] == "v2" && parts[1] == "permanent") purpose = PermanentPurpose;
            else if (parts[0] == "v1" && long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
            {
                expires = DateTimeOffset.FromUnixTimeSeconds(seconds);
                var now = clock.GetUtcNow();
                if (expires <= now || expires > now.AddDays(90).AddMinutes(5)) return null;
                purpose = Purpose;
            }
            else return null;
            var origin = configuration["VerifiedEdge:PublicOrigin"] ?? "";
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var address) || address.Scheme != "https"
                || address.GetLeftPart(UriPartial.Authority) != origin) return null;
            var signature = WebEncoders.Base64UrlDecode(parts[3]);
            if (signature.Length != 64) return null;
            using var verifier = ECDsa.Create();
            verifier.ImportSubjectPublicKeyInfo(Convert.FromBase64String(configuration["OwnerAccess:PublicKey"] ?? ""), out _);
            var canonical = string.Join('\n', purpose, origin, parts[1], parts[2]);
            return verifier.KeySize == 256 && verifier.VerifyData(Encoding.UTF8.GetBytes(canonical), signature,
                HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation) ? new(parts[2].ToLowerInvariant(), expires) : null;
        }
        catch (Exception error) when (error is FormatException or CryptographicException or ArgumentException) { return null; }
    }

}
