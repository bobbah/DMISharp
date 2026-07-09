using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace DMISharp.Tests;

public sealed class DMIAnimationTests
{
    [Fact]
    public void GetAnimatedCopiesFramesAndMetadataWithoutMutatingSources()
    {
        using var state = new DMIState("animated", DirectionDepth.One, 2, 3, 1);
#pragma warning disable CA2000
        var first = new Image<Rgba32>(3, 1);
#pragma warning restore CA2000
        first[0, 0] = new Rgba32(10, 20, 30, 0);
        first[1, 0] = new Rgba32(40, 50, 60, 128);
        first[2, 0] = new Rgba32(70, 80, 90, 255);
        var second = first.Clone();

        var firstSourceMetadata = first.Frames.RootFrame.Metadata.GetFormatMetadata(GifFormat.Instance);
        firstSourceMetadata.FrameDelay = 99;
        firstSourceMetadata.DisposalMethod = GifDisposalMethod.NotDispose;
        var secondSourceMetadata = second.Frames.RootFrame.Metadata.GetFormatMetadata(GifFormat.Instance);
        secondSourceMetadata.FrameDelay = 98;
        secondSourceMetadata.DisposalMethod = GifDisposalMethod.RestoreToPrevious;

        state.SetFrame(first, 0);
        state.SetFrame(second, 1);
        state.SetDelay(new[] { 1.5, 2.5 });
        state.SetLoop(3);

        using var animation = state.GetAnimated(StateDirection.South);

        Assert.Equal(2, animation.Frames.Count);
        Assert.Equal(default, animation.Frames[0][0, 0]);
        Assert.Equal(new Rgba32(40, 50, 60, 128), animation.Frames[0][1, 0]);
        Assert.Equal(new Rgba32(70, 80, 90, 255), animation.Frames[0][2, 0]);

        var gifMetadata = animation.Metadata.GetFormatMetadata(GifFormat.Instance);
        Assert.Equal(GifColorTableMode.Local, gifMetadata.ColorTableMode);
        Assert.Equal(3, gifMetadata.RepeatCount);
        Assert.Equal(15, animation.Frames[0].Metadata.GetFormatMetadata(GifFormat.Instance).FrameDelay);
        Assert.Equal(25, animation.Frames[1].Metadata.GetFormatMetadata(GifFormat.Instance).FrameDelay);
        foreach (var frame in animation.Frames)
        {
            Assert.Equal(GifDisposalMethod.RestoreToBackground,
                frame.Metadata.GetFormatMetadata(GifFormat.Instance).DisposalMethod);
        }

        Assert.Equal(new Rgba32(10, 20, 30, 0), first[0, 0]);
        Assert.Equal(99, firstSourceMetadata.FrameDelay);
        Assert.Equal(GifDisposalMethod.NotDispose, firstSourceMetadata.DisposalMethod);
        Assert.Equal(98, secondSourceMetadata.FrameDelay);
        Assert.Equal(GifDisposalMethod.RestoreToPrevious, secondSourceMetadata.DisposalMethod);
    }

    [Fact]
    public void GetAnimatedMissingFrameDoesNotMutateEarlierFrames()
    {
        using var state = new DMIState("incomplete", DirectionDepth.One, 2, 1, 1);
#pragma warning disable CA2000
        var first = new Image<Rgba32>(1, 1, new Rgba32(10, 20, 30, 0));
#pragma warning restore CA2000
        var sourceMetadata = first.Frames.RootFrame.Metadata.GetFormatMetadata(GifFormat.Instance);
        sourceMetadata.FrameDelay = 99;
        sourceMetadata.DisposalMethod = GifDisposalMethod.NotDispose;
        state.SetFrame(first, 0);
        state.InitializeDelay();

        Assert.Throws<InvalidOperationException>(() => state.GetAnimated(StateDirection.South));
        Assert.Equal(new Rgba32(10, 20, 30, 0), first[0, 0]);
        Assert.Equal(99, sourceMetadata.FrameDelay);
        Assert.Equal(GifDisposalMethod.NotDispose, sourceMetadata.DisposalMethod);
    }
}
