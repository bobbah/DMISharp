using System.Collections.Generic;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Threading.Tasks;

namespace DMISharp.Tests;

internal sealed class DMICreationTests
{
    [Test]
    public void CanMergeDMI()
    {
        using var newDMI = new DMIFile(32, 32);
        using (var firstFile = new DMIFile(@"Data/Input/centcom.dmi"))
        using (var secondFile = new DMIFile(@"Data/Input/misc.dmi"))
        {
            newDMI.ImportStates(firstFile);
            newDMI.ImportStates(secondFile);
        }

        newDMI.Save(@"Data/Output/merged.dmi");
    }

    [Test]
    public void CanCreateDMIFromImages()
    {
        using var newDMI = new DMIFile(32, 32);
        var sourceData = new List<string>() { "sord", "sordvert", "steve32" };

        foreach (var source in sourceData)
        {
            var img = Image.Load<Rgba32>($@"Data/Input/SourceImages/{source}.png");
#pragma warning disable CA2000
            var newState = new DMIState(source, DirectionDepth.One, 1, 32, 32);
#pragma warning restore CA2000
            newState.SetFrame(img, 0);
            newDMI.AddState(newState);
        }

        newDMI.Save(@"Data/Output/minecraft.dmi");
    }

    [Test]
    public async Task CanChangeDMIDepths()
    {
        using var newDMI = new DMIFile(32, 32);

        // Create state
        var img = await Image.LoadAsync<Rgba32>($@"Data/Input/SourceImages/steve32.png").ConfigureAwait(false);
#pragma warning disable CA2000
        var newState = new DMIState("steve32", DirectionDepth.One, 1, 32, 32);
#pragma warning restore CA2000
        newState.SetFrame(img, 0);
        newDMI.AddState(newState);

        // Modifying state
        newDMI.States.First().SetDirectionDepth(DirectionDepth.Four);
        newDMI.States.First().SetFrameCount(10);

        // Check new states
        await Assert.That(newDMI.States.First().DirectionDepth).IsEqualTo(DirectionDepth.Four);
        await Assert.That(newDMI.States.First().Frames).IsEqualTo(10);

        // Cannot save
        await Assert.That(newDMI.CanSave()).IsFalse();
    }
}