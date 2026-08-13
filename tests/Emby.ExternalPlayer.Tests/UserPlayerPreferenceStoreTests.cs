using Emby.ExternalPlayer.Domain;
using Emby.ExternalPlayer.Services;

namespace Emby.ExternalPlayer.Tests;

[TestClass]
public sealed class UserPlayerPreferenceStoreTests
{
    [TestMethod]
    public void Set_PersistsPreferencesByUserAndPlatform()
    {
        WithTemporaryDirectory(directory =>
        {
            var firstUser = Guid.NewGuid();
            var secondUser = Guid.NewGuid();
            var store = new UserPlayerPreferenceStore(directory);

            store.Set(firstUser, ClientPlatform.MacOS, "Iina");
            store.Set(firstUser, ClientPlatform.Windows, "Vlc");
            store.Set(secondUser, ClientPlatform.MacOS, "custom-0123456789abcdef0123456789abcdef");

            var restored = new UserPlayerPreferenceStore(directory);
            Assert.AreEqual("Iina", restored.Get(firstUser, ClientPlatform.MacOS));
            Assert.AreEqual("Vlc", restored.Get(firstUser, ClientPlatform.Windows));
            Assert.AreEqual(
                "custom-0123456789abcdef0123456789abcdef",
                restored.Get(secondUser, ClientPlatform.MacOS));
            Assert.IsNull(restored.Get(secondUser, ClientPlatform.Windows));
        });
    }

    [TestMethod]
    public void Set_RestrictsPreferenceFilesToTheServerAccountOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }
        WithTemporaryDirectory(directory =>
        {
            var store = new UserPlayerPreferenceStore(directory);
            store.Set(Guid.NewGuid(), ClientPlatform.MacOS, "Iina");

            foreach (var fileName in new[]
                     {
                         "external-player-user-preferences.xml",
                         "external-player-user-preferences.xml.bak",
                     })
            {
#pragma warning disable CA1416 // The test returns before this block on Windows.
                var mode = File.GetUnixFileMode(Path.Combine(directory, fileName));
#pragma warning restore CA1416
                Assert.AreEqual(
                    UnixFileMode.UserRead | UnixFileMode.UserWrite,
                    mode,
                    fileName + " must not be readable by other local accounts");
            }
        });
    }

    [TestMethod]
    public void Set_DoesNotChangeMemoryWhenPersistenceFails()
    {
        WithTemporaryDirectory(directory =>
        {
            var blockingPath = Path.Combine(directory, "not-a-directory");
            File.WriteAllText(blockingPath, "block");
            var store = new UserPlayerPreferenceStore(blockingPath);
            var userId = Guid.NewGuid();

            Assert.ThrowsExactly<IOException>(() =>
                store.Set(userId, ClientPlatform.MacOS, "Iina"));
            Assert.IsNull(store.Get(userId, ClientPlatform.MacOS));
        });
    }

    [TestMethod]
    public void Import_PreservesExistingPreferencesAndAddsMissingKeys()
    {
        WithTemporaryDirectory(directory =>
        {
            var userId = Guid.NewGuid();
            var store = new UserPlayerPreferenceStore(directory);
            store.Set(userId, ClientPlatform.MacOS, "Vlc");

            store.Import(new UserPlayerPreferenceOptionsCollection
            {
                new() { UserId = userId.ToString("N"), Platform = ClientPlatform.MacOS, PlayerId = "Iina" },
                new() { UserId = userId.ToString("N"), Platform = ClientPlatform.Windows, PlayerId = "Vlc" },
            });

            Assert.AreEqual("Vlc", store.Get(userId, ClientPlatform.MacOS));
            Assert.AreEqual("Vlc", store.Get(userId, ClientPlatform.Windows));
        });
    }

    [TestMethod]
    public void RemovePlayer_CleansEveryReferenceWithoutTouchingOtherDefaults()
    {
        WithTemporaryDirectory(directory =>
        {
            var firstUser = Guid.NewGuid();
            var secondUser = Guid.NewGuid();
            const string customId = "custom-0123456789abcdef0123456789abcdef";
            var store = new UserPlayerPreferenceStore(directory);
            store.Set(firstUser, ClientPlatform.MacOS, customId);
            store.Set(secondUser, ClientPlatform.Windows, customId);
            store.Set(secondUser, ClientPlatform.MacOS, "Iina");

            Assert.IsTrue(store.RemovePlayer(customId));
            Assert.IsNull(store.Get(firstUser, ClientPlatform.MacOS));
            Assert.IsNull(store.Get(secondUser, ClientPlatform.Windows));
            Assert.AreEqual("Iina", store.Get(secondUser, ClientPlatform.MacOS));
            Assert.IsFalse(store.RemovePlayer(customId));
        });
    }

    [TestMethod]
    public void Load_RecoversTheLastGoodCopyWithoutOverwritingCorruptData()
    {
        WithTemporaryDirectory(directory =>
        {
            var userId = Guid.NewGuid();
            var store = new UserPlayerPreferenceStore(directory);
            store.Set(userId, ClientPlatform.MacOS, "Iina");
            store.Set(userId, ClientPlatform.MacOS, "Vlc");
            var primary = Path.Combine(directory, "external-player-user-preferences.xml");
            File.WriteAllText(primary, "not xml");

            var recovered = new UserPlayerPreferenceStore(directory);

            Assert.AreEqual("Iina", recovered.Get(userId, ClientPlatform.MacOS));
            Assert.AreEqual("not xml", File.ReadAllText(primary),
                "loading must not silently replace evidence needed for manual recovery");
        });
    }

    [TestMethod]
    public void Load_RejectsCorruptPrimaryAndRecoveryCopy()
    {
        WithTemporaryDirectory(directory =>
        {
            var store = new UserPlayerPreferenceStore(directory);
            store.Set(Guid.NewGuid(), ClientPlatform.MacOS, "Iina");
            File.WriteAllText(Path.Combine(directory, "external-player-user-preferences.xml"), "bad primary");
            File.WriteAllText(Path.Combine(directory, "external-player-user-preferences.xml.bak"), "bad backup");

            Assert.ThrowsExactly<InvalidDataException>(() =>
                new UserPlayerPreferenceStore(directory));
        });
    }

    [TestMethod]
    public void Load_UsesRecoveryCopyWhenThePrimaryFileIsMissing()
    {
        WithTemporaryDirectory(directory =>
        {
            var userId = Guid.NewGuid();
            var store = new UserPlayerPreferenceStore(directory);
            store.Set(userId, ClientPlatform.MacOS, "Iina");
            File.Delete(Path.Combine(directory, "external-player-user-preferences.xml"));

            var recovered = new UserPlayerPreferenceStore(directory);

            Assert.AreEqual("Iina", recovered.Get(userId, ClientPlatform.MacOS));
        });
    }

    [TestMethod]
    public void Save_AfterRecoveryDoesNotReplaceTheGoodCopyWithCorruptPrimaryData()
    {
        WithTemporaryDirectory(directory =>
        {
            var userId = Guid.NewGuid();
            var store = new UserPlayerPreferenceStore(directory);
            store.Set(userId, ClientPlatform.MacOS, "Iina");
            store.Set(userId, ClientPlatform.Windows, "Vlc");
            var primary = Path.Combine(directory, "external-player-user-preferences.xml");
            File.WriteAllText(primary, "not xml");
            var recovered = new UserPlayerPreferenceStore(directory);

            recovered.Set(userId, ClientPlatform.Android, "Vlc");
            File.WriteAllText(primary, "corrupt the new primary too");
            var recoveredAgain = new UserPlayerPreferenceStore(directory);

            Assert.AreEqual("Iina", recoveredAgain.Get(userId, ClientPlatform.MacOS));
            Assert.IsNull(recoveredAgain.Get(userId, ClientPlatform.Android),
                "the recovery copy intentionally remains the last independently verified generation");
        });
    }

    [TestMethod]
    public void RemoveMissingCustomPlayers_RetriesSafelyAfterAPersistenceFailure()
    {
        WithTemporaryDirectory(directory =>
        {
            var userId = Guid.NewGuid();
            const string stalePlayer = "custom-0123456789abcdef0123456789abcdef";
            var store = new UserPlayerPreferenceStore(directory);
            store.Set(userId, ClientPlatform.MacOS, stalePlayer);
            File.Delete(Path.Combine(directory, "external-player-user-preferences.xml"));
            File.Delete(Path.Combine(directory, "external-player-user-preferences.xml.bak"));
            Directory.Delete(directory);
            File.WriteAllText(directory, "block writes");
            try
            {
                Assert.ThrowsExactly<IOException>(() =>
                    store.RemoveMissingCustomPlayers(Array.Empty<string>()));
                Assert.AreEqual(stalePlayer, store.Get(userId, ClientPlatform.MacOS));
            }
            finally
            {
                File.Delete(directory);
                Directory.CreateDirectory(directory);
            }

            Assert.IsTrue(store.RemoveMissingCustomPlayers(Array.Empty<string>()));
            Assert.IsNull(store.Get(userId, ClientPlatform.MacOS));
        });
    }

    [TestMethod]
    public void DeleteFiles_RemovesPrimaryAndRecoveryCopy()
    {
        WithTemporaryDirectory(directory =>
        {
            var store = new UserPlayerPreferenceStore(directory);
            store.Set(Guid.NewGuid(), ClientPlatform.MacOS, "Iina");
            var orphanedTemporary = Path.Combine(
                directory,
                "external-player-user-preferences.xml.0123456789abcdef0123456789abcdef.tmp");
            var unrelatedTemporary = Path.Combine(directory, "external-player-user-preferences.xml.keep.tmp");
            File.WriteAllText(orphanedTemporary, "preference data");
            File.WriteAllText(unrelatedTemporary, "unrelated");

            store.DeleteFiles();

            Assert.IsFalse(File.Exists(Path.Combine(directory, "external-player-user-preferences.xml")));
            Assert.IsFalse(File.Exists(Path.Combine(directory, "external-player-user-preferences.xml.bak")));
            Assert.IsFalse(File.Exists(orphanedTemporary));
            Assert.IsTrue(File.Exists(unrelatedTemporary), "cleanup must not delete loosely matching files");
        });
    }

    private static void WithTemporaryDirectory(Action<string> action)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "emby-external-player-preference-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            action(directory);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
