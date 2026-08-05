using GreenBox.I18n;

namespace GreenBox.I18n.Core.Tests;

public sealed class I18nCatalogBatchMoveTests
{
    [Fact]
    public void MoveEntries_ValidMoves_AppliesAllPaths()
    {
        I18nCatalog catalog = CreateCatalog(
            CreateEntry("1", "Units.Tank.Title"),
            CreateEntry("2", "Units.Turret.Title"));

        I18nBatchEditResult result = catalog.MoveEntries(new[]
        {
            new I18nEntryMove(1, "Gameplay.Title"),
            new I18nEntryMove(2, "Gameplay.TurretTitle"),
        });

        Assert.True(result.IsSuccess);
        Assert.True(result.HasChanges);
        Assert.Equal("Gameplay.Title", catalog.FindById(1)!.Path);
        Assert.Equal("Gameplay.TurretTitle", catalog.FindById(2)!.Path);
    }

    [Fact]
    public void MoveEntries_PathCollision_DoesNotChangeCatalog()
    {
        I18nCatalog catalog = CreateCatalog(
            CreateEntry("1", "Units.Tank.Title"),
            CreateEntry("2", "Gameplay.Title"));

        I18nBatchEditResult result = catalog.MoveEntries(new[]
        {
            new I18nEntryMove(1, "Gameplay.Title"),
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(I18nEditCodes.DuplicatePath, result.Error!.Code);
        Assert.Equal("Units.Tank.Title", catalog.FindById(1)!.Path);
        Assert.Equal("Gameplay.Title", catalog.FindById(2)!.Path);
    }

    [Fact]
    public void MoveEntries_SwappedPaths_AppliesAtomically()
    {
        I18nCatalog catalog = CreateCatalog(
            CreateEntry("1", "First.Title"),
            CreateEntry("2", "Second.Title"));

        I18nBatchEditResult result = catalog.MoveEntries(new[]
        {
            new I18nEntryMove(1, "Second.Title"),
            new I18nEntryMove(2, "First.Title"),
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("Second.Title", catalog.FindById(1)!.Path);
        Assert.Equal("First.Title", catalog.FindById(2)!.Path);
    }

    [Fact]
    public void MoveEntries_MissingEntry_DoesNotApplyOtherMoves()
    {
        I18nCatalog catalog = CreateCatalog(CreateEntry("1", "Units.Title"));

        I18nBatchEditResult result = catalog.MoveEntries(new[]
        {
            new I18nEntryMove(1, "Gameplay.Title"),
            new I18nEntryMove(2, "Missing.Title"),
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(I18nEditCodes.EntryNotFound, result.Error!.Code);
        Assert.Equal("Units.Title", catalog.FindById(1)!.Path);
    }

    private static I18nCatalog CreateCatalog(params I18nEntry[] entries)
    {
        return new I18nCatalog { Entries = entries.ToList() };
    }

    private static I18nEntry CreateEntry(string id, string path)
    {
        return new I18nEntry { Id = id, Path = path };
    }
}
