using System.Diagnostics.CodeAnalysis;
using System.Linq;
using DMISharp.Metadata;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace DMISharp.Tests;

public static class DMIModifyTests
{
    [Fact]
    public static void ShouldRemoveStateMetadata()
    {
        using var file = new DMIFile(@"Data/Input/turf_analysis.dmi");

        // Arrange
        var mdCount = file.Metadata.States.Count;
        var stateToRemove = file.States.Last();

        // Act
        var result = file.RemoveState(stateToRemove);
        stateToRemove.Dispose();

        // Assert
        Assert.True(result);
        Assert.Equal(mdCount - 1, file.Metadata.States.Count);
    }

    [Fact]
    public static void ShouldDetectValidUnmodifiedDMIState()
    {
        // Arrange
        using var file = new DMIFile(@"Data/Input/turf_analysis.dmi");

        // Act
        var arrowState = file.States.First(x => x.Name == "arrow");
            
        // Assert
        Assert.True(arrowState.IsReadyForSave());
    }

    [Fact]
    public static void ShouldDetectInvalidDMIStateMissingFrame()
    {
        // Arrange
        using var file = new DMIFile(@"Data/Input/turf_analysis.dmi");

        // Act
        var arrowState = file.States.First(x => x.Name == "arrow");
        arrowState.DeleteFrame(0);
            
        // Assert
        Assert.False(arrowState.IsReadyForSave());
        Assert.False(file.CanSave());
    }

    [Fact]
    public static void ShouldDetectInvalidDMIStateMissingDirection()
    {
        // Arrange
        using var file = new DMIFile(@"Data/Input/turf_analysis.dmi");

        // Act
        var arrowState = file.States.First(x => x.Name == "arrow");
        arrowState.SetDirectionDepth(DirectionDepth.Eight);
            
        // Assert
        Assert.False(arrowState.IsReadyForSave());
        Assert.False(file.CanSave());
    }

    [Fact]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
    public static void StatesShouldReuseReadOnlyView()
    {
        using var file = new DMIFile(1, 1);
        var view = file.States;

        file.AddState(new DMIState("state", DirectionDepth.One, 1, 1, 1));

        Assert.Same(view, file.States);
        Assert.Single(view);
    }

    [Fact]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
    public static void StatesViewShouldRemainLiveAfterSorting()
    {
        using var file = new DMIFile(1, 1);
        file.AddState(new DMIState("second", DirectionDepth.One, 1, 1, 1));
        file.AddState(new DMIState("first", DirectionDepth.One, 1, 1, 1));
        var view = file.States;

        file.SortStates();

        Assert.Same(view, file.States);
        Assert.Equal(new[] { "first", "second" }, view.Select(state => state.Name));
    }

    [Fact]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
    public static void ImportStatesShouldPreserveOrderMetadataAndOwnership()
    {
        using var destination = new DMIFile(1, 1);
        using var source = new DMIFile(1, 1);
        var existing = new DMIState("existing", DirectionDepth.One, 1, 1, 1);
        var first = new DMIState("first", DirectionDepth.One, 1, 1, 1);
        var second = new DMIState("second", DirectionDepth.One, 1, 1, 1);
        first.SetFrame(new Image<Rgba32>(1, 1), 0);
        destination.AddState(existing);
        source.AddState(first);
        source.AddState(second);

        var added = destination.ImportStates(source);
        source.Dispose();

        Assert.Equal(2, added);
        Assert.Equal(new[] { existing, first, second }, destination.States);
        Assert.Equal(destination.States.Select(state => state.Data), destination.Metadata.States);
        Assert.Empty(source.States);
        Assert.Empty(source.Metadata.States);
        Assert.Equal(1, first.TotalFrames);
    }

    [Fact]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
    public static void ImportStatesShouldPreserveUnrelatedMetadata()
    {
        using var destination = new DMIFile(1, 1);
        using var source = new DMIFile(1, 1);
        var first = new DMIState("first", DirectionDepth.One, 1, 1, 1);
        var second = new DMIState("second", DirectionDepth.One, 1, 1, 1);
        var unrelated = new StateMetadata("unrelated");
        source.AddState(first);
        source.AddState(second);
        source.Metadata.States.Insert(0, unrelated);
        source.Metadata.States.Remove(first.Data);

        destination.ImportStates(source);

        Assert.Equal(new[] { unrelated }, source.Metadata.States);
        Assert.Equal(new[] { first.Data, second.Data }, destination.Metadata.States);
    }
}