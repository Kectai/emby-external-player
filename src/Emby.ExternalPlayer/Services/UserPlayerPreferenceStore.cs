using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using Emby.ExternalPlayer.Domain;
using MediaBrowser.Model.Logging;

namespace Emby.ExternalPlayer.Services;

public sealed class UserPlayerPreferenceStore
{
    private static readonly XmlSerializer Serializer = new(typeof(UserPlayerPreferenceDocument));
    private readonly object sync = new();
    private readonly string filePath;
    private readonly string backupPath;
    private readonly ILogger? logger;
    private UserPlayerPreferenceOptionsCollection preferences;
    private bool preserveRecoveryCopy;

    public UserPlayerPreferenceStore(string configurationDirectoryPath, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(configurationDirectoryPath))
        {
            throw new ArgumentException("A configuration directory is required.", nameof(configurationDirectoryPath));
        }
        filePath = Path.Combine(configurationDirectoryPath, "external-player-user-preferences.xml");
        backupPath = filePath + ".bak";
        this.logger = logger;
        preferences = LoadWithRecovery();
        RestrictExistingFile(filePath);
        RestrictExistingFile(backupPath);
    }

    public string? Get(Guid userId, ClientPlatform platform)
    {
        lock (sync)
        {
            return GetValue(preferences, userId, platform);
        }
    }

    public string Set(Guid userId, ClientPlatform platform, string playerId)
    {
        lock (sync)
        {
            var current = GetValue(preferences, userId, platform);
            if (string.Equals(current, playerId, StringComparison.OrdinalIgnoreCase))
            {
                return current!;
            }

            var updated = Clone(preferences);
            var options = new PluginOptions { UserPlayerPreferences = updated };
            options.SetUserDefaultPlayer(userId, platform, playerId);
            Save(updated);
            preferences = updated;
            return options.GetUserDefaultPlayer(userId, platform)!;
        }
    }

    public bool RemovePlayer(string playerId)
    {
        lock (sync)
        {
            var updated = Clone(preferences);
            if (updated.RemoveAll(preference => string.Equals(
                    preference.PlayerId,
                    playerId,
                    StringComparison.OrdinalIgnoreCase)) == 0)
            {
                return false;
            }
            Save(updated);
            preferences = updated;
            return true;
        }
    }

    public bool Import(UserPlayerPreferenceOptionsCollection? legacyPreferences)
    {
        if (legacyPreferences is null || legacyPreferences.Count == 0)
        {
            return false;
        }
        lock (sync)
        {
            var updated = Clone(preferences);
            foreach (var legacy in legacyPreferences)
            {
                if (legacy is null || !Guid.TryParseExact(legacy.UserId, "N", out var userId) ||
                    GetValue(updated, userId, legacy.Platform) is not null)
                {
                    continue;
                }
                var options = new PluginOptions { UserPlayerPreferences = updated };
                try
                {
                    options.SetUserDefaultPlayer(userId, legacy.Platform, legacy.PlayerId);
                }
                catch (ArgumentException)
                {
                    continue;
                }
            }
            if (CollectionsEqual(preferences, updated))
            {
                return false;
            }
            Save(updated);
            preferences = updated;
            return true;
        }
    }

    public bool RemoveMissingCustomPlayers(IEnumerable<string> availablePlayerIds)
    {
        if (availablePlayerIds is null)
        {
            throw new ArgumentNullException(nameof(availablePlayerIds));
        }
        var available = availablePlayerIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        lock (sync)
        {
            var updated = Clone(preferences);
            if (updated.RemoveAll(preference =>
                    preference.PlayerId.StartsWith("custom-", StringComparison.OrdinalIgnoreCase) &&
                    !available.Contains(preference.PlayerId)) == 0)
            {
                return false;
            }
            Save(updated);
            preferences = updated;
            return true;
        }
    }

    public void DeleteFiles()
    {
        lock (sync)
        {
            DeleteFiles(Path.GetDirectoryName(filePath)!);
            preferences = new UserPlayerPreferenceOptionsCollection();
        }
    }

    public static void DeleteFiles(string configurationDirectoryPath)
    {
        if (string.IsNullOrWhiteSpace(configurationDirectoryPath))
        {
            throw new ArgumentException("A configuration directory is required.", nameof(configurationDirectoryPath));
        }
        var path = Path.Combine(configurationDirectoryPath, "external-player-user-preferences.xml");
        DeleteIfPresent(path);
        DeleteIfPresent(path + ".bak");
        if (!Directory.Exists(configurationDirectoryPath))
        {
            return;
        }
        var prefix = Path.GetFileName(path) + ".";
        foreach (var candidate in Directory.GetFiles(configurationDirectoryPath, prefix + "*.tmp"))
        {
            var name = Path.GetFileName(candidate);
            var tokenStart = prefix.Length;
            const int tokenLength = 32;
            if (name.Length == prefix.Length + tokenLength + 4 &&
                name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) &&
                name.Substring(tokenStart, tokenLength).All(IsLowerHexCharacter))
            {
                DeleteIfPresent(candidate);
            }
        }
    }

    private void Save(UserPlayerPreferenceOptionsCollection values)
    {
        var directory = Path.GetDirectoryName(filePath)
            ?? throw new InvalidOperationException("The preference path has no directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.WriteThrough))
            {
                Serializer.Serialize(stream, new UserPlayerPreferenceDocument { Preferences = values });
                stream.Flush();
            }
            RestrictFile(temporaryPath);
            if (File.Exists(filePath))
            {
                File.Replace(
                    temporaryPath,
                    filePath,
                    preserveRecoveryCopy ? null : backupPath);
                preserveRecoveryCopy = false;
            }
            else
            {
                File.Move(temporaryPath, filePath);
                try
                {
                    File.Copy(filePath, backupPath, overwrite: true);
                    RestrictFile(backupPath);
                }
                catch (Exception exception)
                {
                    logger?.ErrorException(
                        "External Player saved user preferences but could not create the recovery copy.",
                        exception);
                }
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private UserPlayerPreferenceOptionsCollection LoadWithRecovery()
    {
        if (!File.Exists(filePath))
        {
            if (!File.Exists(backupPath))
            {
                return new UserPlayerPreferenceOptionsCollection();
            }
            preserveRecoveryCopy = true;
            return Load(backupPath);
        }
        try
        {
            return Load(filePath);
        }
        catch (Exception primaryException)
        {
            if (!File.Exists(backupPath))
            {
                throw new InvalidDataException(
                    "The External Player preference file is unreadable and no recovery copy exists.",
                    primaryException);
            }
            try
            {
                var recovered = Load(backupPath);
                preserveRecoveryCopy = true;
                logger?.ErrorException(
                    "External Player recovered user default-player preferences from the last good copy.",
                    primaryException);
                return recovered;
            }
            catch (Exception backupException)
            {
                throw new InvalidDataException(
                    "The External Player preference file and its recovery copy are unreadable.",
                    new AggregateException(primaryException, backupException));
            }
        }
    }

    private static UserPlayerPreferenceOptionsCollection Load(string path)
    {
        if (!File.Exists(path))
        {
            return new UserPlayerPreferenceOptionsCollection();
        }
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var document = Serializer.Deserialize(stream) as UserPlayerPreferenceDocument;
        var options = new PluginOptions
        {
            UserPlayerPreferences = document?.Preferences ?? new UserPlayerPreferenceOptionsCollection(),
        };
        options.NormalizeUserPlayerPreferences();
        return Clone(options.UserPlayerPreferences);
    }

    private static string? GetValue(
        UserPlayerPreferenceOptionsCollection values,
        Guid userId,
        ClientPlatform platform)
    {
        var userIdValue = userId.ToString("N");
        return values.FirstOrDefault(preference =>
            string.Equals(preference.UserId, userIdValue, StringComparison.OrdinalIgnoreCase) &&
            preference.Platform == platform)?.PlayerId;
    }

    private static UserPlayerPreferenceOptionsCollection Clone(
        UserPlayerPreferenceOptionsCollection values) =>
        new(values.Select(preference => new UserPlayerPreferenceOptions
        {
            UserId = preference.UserId,
            Platform = preference.Platform,
            PlayerId = preference.PlayerId,
        }));

    private static bool CollectionsEqual(
        UserPlayerPreferenceOptionsCollection first,
        UserPlayerPreferenceOptionsCollection second) =>
        first.Count == second.Count && first.All(value => second.Any(candidate =>
            string.Equals(value.UserId, candidate.UserId, StringComparison.OrdinalIgnoreCase) &&
            value.Platform == candidate.Platform &&
            string.Equals(value.PlayerId, candidate.PlayerId, StringComparison.OrdinalIgnoreCase)));

    private static void DeleteIfPresent(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static bool IsLowerHexCharacter(char value) =>
        (value >= '0' && value <= '9') || (value >= 'a' && value <= 'f');

    private void RestrictExistingFile(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }
        try
        {
            RestrictFile(path);
        }
        catch (Exception exception)
        {
            logger?.ErrorException(
                "External Player could not restrict user preference file permissions.",
                exception);
        }
    }

    private static void RestrictFile(string path)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Chmod(path, 0x180) != 0)
        {
            throw new IOException(
                "Could not set owner-only permissions on the External Player preference file.");
        }
    }

    [DllImport("libc", EntryPoint = "chmod", SetLastError = true)]
    private static extern int Chmod(string path, uint mode);
}

public sealed class UserPlayerPreferenceDocument
{
    public UserPlayerPreferenceOptionsCollection Preferences { get; set; } = new();
}
