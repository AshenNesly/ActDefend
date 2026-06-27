using System;
using System.IO;
using System.Threading.Tasks;
using ActDefend.Core.Configuration;
using ActDefend.Core.Models;
using ActDefend.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ActDefend.UnitTests.Storage;

public sealed class TrustedProcessRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly TrustedProcessRepository _repo;

    public TrustedProcessRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"actdefend_test_{Guid.NewGuid():N}.db");

        var options = Options.Create(new ActDefendOptions
        {
            Storage = new StorageOptions { DatabasePath = _dbPath },
            TrustedProcesses = new TrustedProcessOptions
            {
                DefaultExclusions = ["svchost.exe", "MsMpEng.exe"]
            }
        });

        _repo = new TrustedProcessRepository(NullLogger<TrustedProcessRepository>.Instance, options);
    }

    // ── Existing tests (unchanged) ────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_IncludesDefaultExclusions()
    {
        var entries = await _repo.GetAllAsync();
        entries.Should().Contain(e => e.ProcessName == "svchost.exe" && e.IsDefault);
        entries.Should().Contain(e => e.ProcessName == "MsMpEng.exe" && e.IsDefault);
    }

    [Fact]
    public async Task AddAsync_PersistsToDatabase_AndReturnsInGetAll()
    {
        var newEntry = new TrustedProcessEntry
        {
            EntryId     = Guid.NewGuid(),
            ProcessName = "custom.exe",
            CreatedAt   = DateTimeOffset.UtcNow,
            Reason      = "Test manual add",
            Source      = "UserAdded",
            IsDefault   = false
        };

        await _repo.AddAsync(newEntry);

        var entries = await _repo.GetAllAsync();
        entries.Should().ContainSingle(e => e.ProcessName == "custom.exe" && e.Source == "UserAdded");

        // Verify IsTrusted works
        _repo.IsTrusted(1234, "custom.exe", null).Should().BeTrue();
    }

    [Fact]
    public async Task RemoveAsync_RemovesUserAddedEntry()
    {
        var newEntry = new TrustedProcessEntry
        {
            EntryId     = Guid.NewGuid(),
            ProcessName = "custom2.exe",
            CreatedAt   = DateTimeOffset.UtcNow,
            Reason      = "To be removed",
            Source      = "UserAdded",
            IsDefault   = false
        };

        await _repo.AddAsync(newEntry);
        await _repo.RemoveAsync(newEntry.EntryId);

        var entries = await _repo.GetAllAsync();
        entries.Should().NotContain(e => e.ProcessName == "custom2.exe");
        _repo.IsTrusted(1234, "custom2.exe", null).Should().BeFalse();
    }

    [Fact]
    public async Task RemoveAsync_DoesNotRemoveDefaultEntry()
    {
        var entries = await _repo.GetAllAsync();
        var defaultEntry = entries[0];
        defaultEntry.IsDefault.Should().BeTrue();

        await _repo.RemoveAsync(defaultEntry.EntryId);

        var entriesAfter = await _repo.GetAllAsync();
        entriesAfter.Should().Contain(e => e.EntryId == defaultEntry.EntryId);
    }

    [Fact]
    public async Task RestartSimulation_LoadsPersistedEntries()
    {
        var newEntry = new TrustedProcessEntry
        {
            EntryId     = Guid.NewGuid(),
            ProcessName = "persistent.exe",
            CreatedAt   = DateTimeOffset.UtcNow,
            Reason      = "Survives restart",
            Source      = "UserAdded",
            IsDefault   = false
        };

        await _repo.AddAsync(newEntry);

        // Simulate restart by creating a new repository instance pointing to the same DB file
        var options = Options.Create(new ActDefendOptions
        {
            Storage          = new StorageOptions { DatabasePath = _dbPath },
            TrustedProcesses = new TrustedProcessOptions { DefaultExclusions = ["svchost.exe"] }
        });

        var newRepo = new TrustedProcessRepository(NullLogger<TrustedProcessRepository>.Instance, options);

        var entries = await newRepo.GetAllAsync();
        entries.Should().ContainSingle(e => e.ProcessName == "persistent.exe");
        entries.Should().ContainSingle(e => e.ProcessName == "svchost.exe");
    }

    // ── New tests ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that removing a user-added entry and then simulating an application restart
    /// does NOT reload that entry — i.e., removal is truly persisted to SQLite.
    /// </summary>
    [Fact]
    public async Task RemoveAsync_AfterRestart_RemovedEntryDoesNotReload()
    {
        // Arrange: add an entry
        var entry = new TrustedProcessEntry
        {
            EntryId     = Guid.NewGuid(),
            ProcessName = "removed-after-restart.exe",
            CreatedAt   = DateTimeOffset.UtcNow,
            Reason      = "Will be removed before restart",
            Source      = "UserAdded",
            IsDefault   = false
        };
        await _repo.AddAsync(entry);
        _repo.IsTrusted(0, "removed-after-restart.exe", null).Should().BeTrue("entry was just added");

        // Act: remove the entry
        await _repo.RemoveAsync(entry.EntryId);
        _repo.IsTrusted(0, "removed-after-restart.exe", null).Should().BeFalse("entry was just removed");

        // Simulate app restart with a fresh repository pointing at the same DB file
        var restartOptions = Options.Create(new ActDefendOptions
        {
            Storage          = new StorageOptions { DatabasePath = _dbPath },
            TrustedProcesses = new TrustedProcessOptions { DefaultExclusions = ["svchost.exe"] }
        });
        var restarted = new TrustedProcessRepository(NullLogger<TrustedProcessRepository>.Instance, restartOptions);

        // Assert: the removed entry must NOT be present after restart
        var entriesAfterRestart = await restarted.GetAllAsync();
        entriesAfterRestart.Should().NotContain(e => e.ProcessName == "removed-after-restart.exe",
            "the entry was removed before restart and must not reload from SQLite");
        restarted.IsTrusted(0, "removed-after-restart.exe", null).Should().BeFalse();
    }

    /// <summary>
    /// Verifies that default (IsDefault=true) entries survive a RemoveAsync call —
    /// they are protected in-memory only and were never written to SQLite,
    /// so they always reload from configuration.
    /// </summary>
    [Fact]
    public async Task DefaultEntries_AreProtected_CannotBeRemovedViaRemoveAsync()
    {
        // Try removing both defaults
        var all = await _repo.GetAllAsync();
        var defaults = all.Where(e => e.IsDefault).ToList();
        defaults.Should().HaveCount(2, "two defaults were configured");

        foreach (var d in defaults)
            await _repo.RemoveAsync(d.EntryId);

        // In-memory: defaults should still be present (RemoveAll guard: !e.IsDefault)
        var after = await _repo.GetAllAsync();
        after.Should().Contain(e => e.ProcessName == "svchost.exe" && e.IsDefault);
        after.Should().Contain(e => e.ProcessName == "MsMpEng.exe" && e.IsDefault);
    }

    /// <summary>
    /// AddAsync with a user-supplied reason stores that reason and exposes it via GetAllAsync.
    /// </summary>
    [Fact]
    public async Task AddAsync_StoresReason_AndReturnsItInGetAll()
    {
        const string reason = "My backup agent writes many files — trusted";
        var entry = new TrustedProcessEntry
        {
            EntryId     = Guid.NewGuid(),
            ProcessName = "backupagent.exe",
            CreatedAt   = DateTimeOffset.UtcNow,
            Reason      = reason,
            Source      = "UserAdded",
            IsDefault   = false
        };

        await _repo.AddAsync(entry);

        var all = await _repo.GetAllAsync();
        all.Should().ContainSingle(e => e.ProcessName == "backupagent.exe" && e.Reason == reason);
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (!File.Exists(_dbPath)) return;
        try
        {
            File.Delete(_dbPath);
            foreach (var ext in new[] { "-wal", "-shm" })
            {
                var aux = _dbPath + ext;
                if (File.Exists(aux)) File.Delete(aux);
            }
        }
        catch { /* ignore test cleanup errors */ }
    }
}
