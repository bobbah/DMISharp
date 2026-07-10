using System.Diagnostics.CodeAnalysis;
using System.Linq;
using DMISharp.Metadata;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Threading.Tasks;
using TUnit.Assertions.Enums;

namespace DMISharp.Tests;

internal sealed class DMIModifyTests
{
    [Test]
    public async Task ShouldRemoveStateMetadata()
    {
        using var file = new DMIFile(@"Data/Input/turf_analysis.dmi");

        // Arrange
        var mdCount = file.Metadata.States.Count;
        var stateToRemove = file.States.Last();

        // Act
        var result = file.RemoveState(stateToRemove);
        stateToRemove.Dispose();

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(file.Metadata.States.Count).IsEqualTo(mdCount - 1);
    }

    [Test]
    public async Task ShouldDetectValidUnmodifiedDMIState()
    {
        // Arrange
        using var file = new DMIFile(@"Data/Input/turf_analysis.dmi");

        // Act
        var arrowState = file.States.First(x => x.Name == "arrow");

        // Assert
        await Assert.That(arrowState.IsReadyForSave()).IsTrue();
    }

    [Test]
    public async Task ShouldDetectInvalidDMIStateMissingFrame()
    {
        // Arrange
        using var file = new DMIFile(@"Data/Input/turf_analysis.dmi");

        // Act
        var arrowState = file.States.First(x => x.Name == "arrow");
        arrowState.DeleteFrame(0);

        // Assert
        await Assert.That(arrowState.IsReadyForSave()).IsFalse();
        await Assert.That(file.CanSave()).IsFalse();
    }

    [Test]
    public async Task ShouldDetectInvalidDMIStateMissingDirection()
    {
        // Arrange
        using var file = new DMIFile(@"Data/Input/turf_analysis.dmi");

        // Act
        var arrowState = file.States.First(x => x.Name == "arrow");
        arrowState.SetDirectionDepth(DirectionDepth.Eight);

        // Assert
        await Assert.That(arrowState.IsReadyForSave()).IsFalse();
        await Assert.That(file.CanSave()).IsFalse();
    }

    [Test]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
    public async Task StatesShouldReuseReadOnlyView()
    {
        using var file = new DMIFile(1, 1);
        var view = file.States;

        file.AddState(new DMIState("state", DirectionDepth.One, 1, 1, 1));

        await Assert.That(file.States).IsSameReferenceAs(view);
        await Assert.That(view).HasSingleItem();
    }

    [Test]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
    public async Task StatesViewShouldRemainLiveAfterSorting()
    {
        using var file = new DMIFile(1, 1);
        file.AddState(new DMIState("second", DirectionDepth.One, 1, 1, 1));
        file.AddState(new DMIState("first", DirectionDepth.One, 1, 1, 1));
        var view = file.States;

        file.SortStates();

        await Assert.That(file.States).IsSameReferenceAs(view);
        await Assert.That(view.Select(state => state.Name))
            .IsEquivalentTo(new[] { "first", "second" }, CollectionOrdering.Matching);
    }

    [Test]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
    public async Task ImportStatesShouldPreserveOrderMetadataAndOwnership()
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

        await Assert.That(added).IsEqualTo(2);
        await Assert.That(destination.States)
            .IsEquivalentTo(new[] { existing, first, second }, CollectionOrdering.Matching);
        await Assert.That(destination.Metadata.States)
            .IsEquivalentTo(destination.States.Select(state => state.Data), CollectionOrdering.Matching);
        await Assert.That(source.States).IsEmpty();
        await Assert.That(source.Metadata.States).IsEmpty();
        await Assert.That(first.TotalFrames).IsEqualTo(1);
    }

    [Test]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
    public async Task ImportStatesShouldPreserveUnrelatedMetadata()
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

        await Assert.That(source.Metadata.States)
            .IsEquivalentTo(new[] { unrelated }, CollectionOrdering.Matching);
        await Assert.That(destination.Metadata.States)
            .IsEquivalentTo(new[] { first.Data, second.Data }, CollectionOrdering.Matching);
    }
}