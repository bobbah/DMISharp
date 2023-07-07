﻿using System.Collections.Generic;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace DMISharp.Tests;

public class DMICreationTests
{
    [Fact]
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

    [Fact]
    public void CanCreateDMIFromImages()
    {
        using var newDMI = new DMIFile(32, 32);
        var sourceData = new List<string>() { "sord", "sordvert", "steve32" };

        foreach (var source in sourceData)
        {
            var img = Image.Load<Rgba32>($@"Data/Input/SourceImages/{source}.png");
            var newState = new DMIState(source, DirectionDepth.One, 1, 32, 32);
            newState.SetFrame(img, 0);
            newDMI.AddState(newState);
        }

        newDMI.Save(@"Data/Output/minecraft.dmi");
    }

    [Fact]
    public void CanChangeDMIDepths()
    {
        using var newDMI = new DMIFile(32, 32);

        // Create state
        var img = Image.Load<Rgba32>($@"Data/Input/SourceImages/steve32.png");
        var newState = new DMIState("steve32", DirectionDepth.One, 1, 32, 32);
        newState.SetFrame(img, 0);
        newDMI.AddState(newState);

        // Modifying state
        newDMI.States.First().SetDirectionDepth(DirectionDepth.Four);
        newDMI.States.First().SetFrameCount(10);

        // Check new states
        Assert.Equal(DirectionDepth.Four, newDMI.States.First().DirectionDepth);
        Assert.Equal(10, newDMI.States.First().Frames);

        // Cannot save
        Assert.False(newDMI.CanSave());
    }
}