using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Preferences.Managers;
using Content.Shared.CCVar;
using Robust.Server.Player;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Database;

public sealed partial class UserIdAutoMigrationManager
{
    private const string AuthUnavailableMessage = "Authentication service is temporarily unavailable. Please try again later.";
    private const string MigrationBusyMessage = "Account data migration is already in progress. Please try again later.";

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IHttpClientHolder _http = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private PlayTimeTrackingManager _playTimeTracking = default!;

    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _migrationLocks = new();
    private ISawmill _sawmill = default!;
    private bool _warnedMissingEndpoint;

    public void Initialize()
    {
        _sawmill = _logManager.GetSawmill("user_id_migration");
    }

    public Task<string?> TryMigrateOnConnecting(NetConnectingArgs args, CancellationToken cancel = default)
    {
        if (!ServerPreferencesManager.ShouldStorePrefs(args.AuthType))
            return Task.FromResult<string?>(null);

        return TryMigrate(args.UserId, true, cancel);
    }

    private async Task<string?> TryMigrate(NetUserId mkUserId, bool denyOnFailure, CancellationToken cancel)
    {
        if (!_cfg.GetCVar(CCVars.UserIdMigrationAutoEnabled))
            return null;

        var migrationLock = _migrationLocks.GetOrAdd(mkUserId.UserId, _ => new SemaphoreSlim(1, 1));
        await migrationLock.WaitAsync(cancel);

        try
        {
            if (!TryBuildRequestUri(mkUserId.UserId, out var requestUri))
                return denyOnFailure ? AuthUnavailableMessage : null;

            var oldUserId = await QueryLinkedWizDenUserId(mkUserId.UserId, requestUri, cancel);
            if (oldUserId == null)
                return null;

            if (oldUserId == mkUserId.UserId)
            {
                _sawmill.Error("Auth returned the same linked WizDen UUID and MK UUID for {UserId}; refusing automatic migration.", mkUserId);
                return denyOnFailure ? AuthUnavailableMessage : null;
            }

            if (TryFindBlockingOnlineSession(oldUserId.Value, mkUserId.UserId, out var blockingSession))
            {
                _sawmill.Warning(
                    "Skipping user ID migration {OldUserId} -> {NewUserId} because {PlayerName} ({BlockingUserId}) is online.",
                    oldUserId,
                    mkUserId.UserId,
                    blockingSession.Name,
                    blockingSession.UserId);
                return denyOnFailure ? MigrationBusyMessage : null;
            }

            await _playTimeTracking.WaitPendingSaves(new NetUserId(oldUserId.Value), cancel);
            await _playTimeTracking.WaitPendingSaves(mkUserId, cancel);

            var report = await _db.ApplyUserIdLoginMigrationAsync(oldUserId.Value, mkUserId.UserId, cancel);
            LogReport(report);
            if (!report.CanApply)
                return denyOnFailure ? AuthUnavailableMessage : null;

            return null;
        }
        catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
        {
            _sawmill.Warning("Auth lookup timed out for MK UUID {UserId}; cannot run user ID migration.", mkUserId);
            return denyOnFailure ? AuthUnavailableMessage : null;
        }
        catch (HttpRequestException e)
        {
            _sawmill.Warning("Auth lookup failed for MK UUID {UserId}; cannot run user ID migration. Error: {Error}", mkUserId, e.Message);
            return denyOnFailure ? AuthUnavailableMessage : null;
        }
        catch (JsonException e)
        {
            _sawmill.Warning("Auth returned invalid user ID migration JSON for MK UUID {UserId}; cannot run user ID migration. Error: {Error}", mkUserId, e.Message);
            return denyOnFailure ? AuthUnavailableMessage : null;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _sawmill.Error("User ID migration failed for MK UUID {UserId}; cannot complete migration. Error: {Error}", mkUserId, e);
            return denyOnFailure ? AuthUnavailableMessage : null;
        }
        finally
        {
            migrationLock.Release();
        }
    }

    private async Task<Guid?> QueryLinkedWizDenUserId(Guid mkUserId, string requestUri, CancellationToken cancel)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_cfg.GetCVar(CCVars.UserIdMigrationAuthTimeoutSeconds), 1, 60)));

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        using var response = await _http.Client.SendAsync(request, timeout.Token);
        if (response.StatusCode is HttpStatusCode.NoContent)
            return null;

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Auth returned {response.StatusCode} for MK UUID {mkUserId}; refusing to treat this as an unlinked account.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException($"Auth user ID migration response for MK UUID {mkUserId} was not a JSON object.");
        }

        if (!TryReadGuid(document.RootElement, out var responseMkUserId, out var invalidMkUserId, "mkUserId", "mk_user_id", "mkUuid", "mk_uuid", "uuid"))
        {
            throw new JsonException($"Auth response did not include an MK UUID for requested MK UUID {mkUserId}.");
        }

        if (invalidMkUserId || responseMkUserId == null)
        {
            throw new JsonException($"Auth returned an invalid MK UUID for requested MK UUID {mkUserId}.");
        }

        if (responseMkUserId != mkUserId)
        {
            throw new InvalidOperationException(
                $"Auth returned mismatched MK UUID {responseMkUserId} for requested MK UUID {mkUserId}; skipping migration.");
        }

        if (!TryReadGuid(
                document.RootElement,
                out var wizDenUserId,
                out var invalidWizDenUserId,
                "wizdenUserId",
                "wizDenUserId",
                "wizden_user_id",
                "wizdenUuid",
                "wizDenUuid",
                "wizden_uuid",
                "linked"))
        {
            throw new JsonException($"Auth response did not include a linked WizDen UUID field for MK UUID {mkUserId}.");
        }

        if (invalidWizDenUserId)
        {
            throw new JsonException($"Auth returned an invalid linked WizDen UUID for MK UUID {mkUserId}.");
        }

        return wizDenUserId;
    }

    private bool TryBuildRequestUri(Guid mkUserId, out string requestUri)
    {
        requestUri = string.Empty;

        var endpoint = _cfg.GetCVar(CCVars.UserIdMigrationAuthEndpoint).Trim();
        if (endpoint.Length == 0)
        {
            if (!_warnedMissingEndpoint)
            {
                _sawmill.Warning(
                    "User ID auto migration is enabled, but {CVar} is empty. Automatic migration will be skipped.",
                    CCVars.UserIdMigrationAuthEndpoint.Name);
                _warnedMissingEndpoint = true;
            }

            return false;
        }

        var encodedUserId = WebUtility.UrlEncode(mkUserId.ToString());
        var hasPlaceholder = endpoint.Contains("{mkUserId}", StringComparison.Ordinal) ||
                             endpoint.Contains("{0}", StringComparison.Ordinal);

        endpoint = endpoint
            .Replace("{mkUserId}", encodedUserId, StringComparison.Ordinal)
            .Replace("{0}", encodedUserId, StringComparison.Ordinal);

        if (!hasPlaceholder)
        {
            var separator = endpoint.Contains('?') ? '&' : '?';
            endpoint = $"{endpoint}{separator}mkUserId={encodedUserId}";
        }

        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var absoluteUri) && IsHttpUri(absoluteUri))
        {
            requestUri = absoluteUri.ToString();
            return true;
        }

        if (endpoint.Contains(Uri.SchemeDelimiter, StringComparison.Ordinal))
        {
            _sawmill.Warning(
                "Cannot build user ID migration auth URL because {CVar} uses unsupported URI scheme: {Endpoint}",
                CCVars.UserIdMigrationAuthEndpoint.Name,
                endpoint);
            return false;
        }

        var authServer = _cfg.GetCVar(CVars.AuthServer).Trim();
        if (!Uri.TryCreate(authServer, UriKind.Absolute, out var baseUri) || !IsHttpUri(baseUri))
        {
            _sawmill.Warning("Cannot build user ID migration auth URL because auth.server is not an absolute HTTP(S) URL: {AuthServer}", authServer);
            return false;
        }

        requestUri = new Uri(baseUri, endpoint.TrimStart('/')).ToString();
        return true;
    }

    private static bool IsHttpUri(Uri uri)
    {
        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
    }

    private bool TryFindBlockingOnlineSession(
        Guid oldUserId,
        Guid newUserId,
        [NotNullWhen(true)] out ICommonSession? blockingSession)
    {
        foreach (var session in _players.Sessions)
        {
            var sessionUserId = session.UserId.UserId;
            if (sessionUserId != oldUserId && sessionUserId != newUserId)
                continue;

            blockingSession = session;
            return true;
        }

        blockingSession = null;
        return false;
    }

    private void LogReport(UserIdMigrationReport report)
    {
        foreach (var warning in report.Warnings)
        {
            if (!report.HasOldData)
                continue;

            _sawmill.Warning(
                "User ID migration {OldUserId} -> {NewUserId}: {Warning}",
                report.OldUserId,
                report.NewUserId,
                warning);
        }

        foreach (var error in report.Errors)
        {
            _sawmill.Error(
                "User ID migration {OldUserId} -> {NewUserId}: {Error}",
                report.OldUserId,
                report.NewUserId,
                error);
        }

        if (!report.CanApply)
            return;

        if (report.Applied)
        {
            _sawmill.Info(
                "Applied user ID migration {OldUserId} -> {NewUserId}.",
                report.OldUserId,
                report.NewUserId);
        }
        else if (report.AlreadyProcessed)
        {
            _sawmill.Verbose(
                "User ID migration {OldUserId} -> {NewUserId} was already processed.",
                report.OldUserId,
                report.NewUserId);
        }
        else
        {
            _sawmill.Verbose(
                "User ID migration {OldUserId} -> {NewUserId} had no local rows to apply.",
                report.OldUserId,
                report.NewUserId);
        }
    }

    private static bool TryReadGuid(JsonElement element, out Guid? value, out bool invalid, params string[] names)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!names.Any(name => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)))
                continue;

            if (property.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                value = null;
                invalid = false;
                return true;
            }

            if (property.Value.ValueKind == JsonValueKind.String &&
                Guid.TryParse(property.Value.GetString(), out var parsed))
            {
                value = parsed;
                invalid = false;
                return true;
            }

            value = null;
            invalid = true;
            return true;
        }

        value = null;
        invalid = false;
        return false;
    }
}
