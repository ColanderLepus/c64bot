namespace C64Bot.Libretro.Tests;

// Tests that load the VICE core but don't run a game, except for the one shared running core.
[Collection(CoreCollection.Name)]
public class LibretroCoreLifecycleTests(LodeRunnerAtTheTrainerMenu lodeRunner)
{
    [CoreFact]
    public void GetSystemInfo_ViceCore_DescribesViceX64sc()
    {
        // Arrange
        using var core = LibretroCore.Load(CoreFiles.CorePath);

        // Act
        var info = core.GetSystemInfo();

        // Assert
        Assert.Equal("VICE x64sc", info.LibraryName);
        Assert.StartsWith("3.10", info.LibraryVersion);
        Assert.Contains("d64", info.ValidExtensions.Split('|'));
        Assert.True(info.NeedFullPath);
    }

    [CoreFact]
    public void ApiVersion_ViceCore_IsOne()
    {
        // Arrange
        using var core = LibretroCore.Load(CoreFiles.CorePath);

        // Act
        var version = core.ApiVersion;

        // Assert
        Assert.Equal(1u, version);
    }

    [CoreFact]
    public void LoadGame_BeforeInitialize_ThrowsInvalidOperationException()
    {
        // Arrange
        using var core = LibretroCore.Load(CoreFiles.CorePath);

        // Act
        var exception = Record.Exception(() => core.LoadGame(CoreFiles.DiskPath));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    [CoreFact]
    public void RunFrame_BeforeLoadGame_ThrowsInvalidOperationException()
    {
        // Arrange
        using var core = LibretroCore.Load(CoreFiles.CorePath);

        // Act
        var exception = Record.Exception(core.RunFrame);

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    [CoreFact]
    public void SystemRam_BeforeLoadGame_ThrowsInvalidOperationException()
    {
        // Arrange
        using var core = LibretroCore.Load(CoreFiles.CorePath);

        // Act
        var exception = Record.Exception(() => core.SystemRam.Length);

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    [CoreFact]
    public void WriteSystemRam_BeforeLoadGame_ThrowsInvalidOperationException()
    {
        // Arrange
        using var core = LibretroCore.Load(CoreFiles.CorePath);

        // Act
        var exception = Record.Exception(() => core.WriteSystemRam(0xC000, [1]));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    [CoreFact]
    public void SaveState_BeforeLoadGame_ThrowsInvalidOperationException()
    {
        // Arrange
        using var core = LibretroCore.Load(CoreFiles.CorePath);

        // Act
        var exception = Record.Exception(() => core.SaveState());

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    [CoreFact]
    public void LoadState_BeforeLoadGame_ThrowsInvalidOperationException()
    {
        // Arrange
        using var core = LibretroCore.Load(CoreFiles.CorePath);

        // Act
        var exception = Record.Exception(() => core.LoadState([1, 2, 3]));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    [CoreFact]
    public void Initialize_WhileAnotherCoreIsRunning_ThrowsInvalidOperationException()
    {
        // Arrange
        _ = lodeRunner.Core;
        using var second = LibretroCore.Load(CoreFiles.CorePath);

        // Act
        var exception = Record.Exception(() => second.Initialize(Path.GetTempPath(), Path.GetTempPath()));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    [CoreFact]
    public void Dispose_Twice_DoesNotThrow()
    {
        // Arrange
        var core = LibretroCore.Load(CoreFiles.CorePath);
        core.Dispose();

        // Act
        var exception = Record.Exception(core.Dispose);

        // Assert
        Assert.Null(exception);
    }

    [CoreFact]
    public void GetSystemInfo_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var core = LibretroCore.Load(CoreFiles.CorePath);
        core.Dispose();

        // Act
        var exception = Record.Exception(core.GetSystemInfo);

        // Assert
        Assert.IsType<ObjectDisposedException>(exception);
    }
}
