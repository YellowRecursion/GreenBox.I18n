using GreenBox.I18n;

namespace GreenBox.I18n.Core.Tests;

public sealed class I18nCatalogBatchMoveTests
{
    [Fact]
    public void MoveEntries_ValidMoves_AppliesAllPaths()
    {
        I18nCatalog catalog = CreateCatalog(
            CreateEntry("3857333080842830204", "Units.Tank.Title"),
            CreateEntry("3857333080842830453", "Units.Turret.Title"));

        I18nBatchEditResult result = catalog.MoveEntries(new[]
        {
            new I18nEntryMove(3857333080842830204, "Gameplay.Title"),
            new I18nEntryMove(3857333080842830453, "Gameplay.TurretTitle"),
        });

        Assert.True(result.IsSuccess);
        Assert.True(result.HasChanges);
        Assert.Equal("Gameplay.Title", catalog.FindById(3857333080842830204)!.Path);
        Assert.Equal("Gameplay.TurretTitle", catalog.FindById(3857333080842830453)!.Path);
    }

    [Fact]
    public void MoveEntries_PathCollision_DoesNotChangeCatalog()
    {
        I18nCatalog catalog = CreateCatalog(
            CreateEntry("3857333080842830204", "Units.Tank.Title"),
            CreateEntry("3857333080842830453", "Gameplay.Title"));

        I18nBatchEditResult result = catalog.MoveEntries(new[]
        {
            new I18nEntryMove(3857333080842830204, "Gameplay.Title"),
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(I18nEditCodes.DuplicatePath, result.Error!.Code);
        Assert.Equal("Units.Tank.Title", catalog.FindById(3857333080842830204)!.Path);
        Assert.Equal("Gameplay.Title", catalog.FindById(3857333080842830453)!.Path);
    }

    [Fact]
    public void MoveEntries_SwappedPaths_AppliesAtomically()
    {
        I18nCatalog catalog = CreateCatalog(
            CreateEntry("3857333080842830204", "First.Title"),
            CreateEntry("3857333080842830453", "Second.Title"));

        I18nBatchEditResult result = catalog.MoveEntries(new[]
        {
            new I18nEntryMove(3857333080842830204, "Second.Title"),
            new I18nEntryMove(3857333080842830453, "First.Title"),
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("Second.Title", catalog.FindById(3857333080842830204)!.Path);
        Assert.Equal("First.Title", catalog.FindById(3857333080842830453)!.Path);
    }

    [Fact]
    public void MoveEntries_MissingEntry_DoesNotApplyOtherMoves()
    {
        I18nCatalog catalog = CreateCatalog(CreateEntry("3857333080842830204", "Units.Title"));

        I18nBatchEditResult result = catalog.MoveEntries(new[]
        {
            new I18nEntryMove(3857333080842830204, "Gameplay.Title"),
            new I18nEntryMove(3857333080842830453, "Missing.Title"),
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(I18nEditCodes.EntryNotFound, result.Error!.Code);
        Assert.Equal("Units.Title", catalog.FindById(3857333080842830204)!.Path);
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
