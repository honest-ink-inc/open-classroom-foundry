// SPDX-License-Identifier: GPL-3.0-or-later
using Azure.Core;
using Azure.Identity;
using Foundry.Domain;

namespace Foundry.Infrastructure.Windows;

/// <summary>
/// Keyless district authentication, ported from Writer's Kiosk baseline
/// c2b670b (EntraAuth.cs). One interactive browser sign-in on first use; the
/// MSAL cache lands DPAPI-encrypted for this Windows user, plus a small
/// non-secret account record, so later launches are silent. No long-lived
/// secret ever exists on disk; IT revokes by disabling the account or role.
/// <see cref="GetTokenAsync"/> is the bearer factory the Azure provider takes.
/// </summary>
public sealed class EntraTokenProvider
{
    private static readonly string[] Scopes = ["https://cognitiveservices.azure.com/.default"];

    private readonly InteractiveBrowserCredential _credential;
    private readonly string _recordPath;
    private AccessToken _token;

    public EntraTokenProvider(string? tenantId, string? clientId, string? cacheDirectory = null)
    {
        _recordPath = Path.Combine(
            cacheDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                EngineIdentity.InternalId),
            "entra-account.json");

        var options = new InteractiveBrowserCredentialOptions
        {
            TenantId = tenantId,
            ClientId = clientId,
            // Sign in interactively only when explicitly asked, so a token
            // refresh can never surprise a classroom with a browser window.
            DisableAutomaticAuthentication = true,
            TokenCachePersistenceOptions = new TokenCachePersistenceOptions
            {
                Name = EngineIdentity.InternalId, // DPAPI-encrypted on Windows
            },
        };

        try
        {
            if (File.Exists(_recordPath))
            {
                using var stream = File.OpenRead(_recordPath);
                options.AuthenticationRecord = AuthenticationRecord.Deserialize(stream);
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            // An unreadable record falls through to a fresh sign-in.
        }

        _credential = new InteractiveBrowserCredential(options);
    }

    /// <summary>
    /// Returns a valid bearer token, signing in interactively only when the
    /// silent cache cannot supply one (first run, revoked session, expired
    /// refresh token). Suitable as the Azure provider's token factory.
    /// </summary>
    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_token.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            return _token.Token;
        }

        var context = new TokenRequestContext(Scopes);
        try
        {
            _token = await _credential.GetTokenAsync(context, cancellationToken).ConfigureAwait(false);
        }
        catch (AuthenticationRequiredException)
        {
            var record = await _credential.AuthenticateAsync(context, cancellationToken).ConfigureAwait(false);
            Directory.CreateDirectory(Path.GetDirectoryName(_recordPath)!);
            using (var stream = File.Create(_recordPath))
            {
                record.Serialize(stream, cancellationToken);
            }

            _token = await _credential.GetTokenAsync(context, cancellationToken).ConfigureAwait(false);
        }

        return _token.Token;
    }
}
