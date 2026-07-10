using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using System.Threading.Tasks;

namespace DMISharp.Tests;

internal sealed class DMIAnimationTests
{
    [Test]
    public async Task GetAnimatedCopiesFramesAndMetadataWithoutMutatingSources()
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

        await Assert.That(animation.Frames.Count).IsEqualTo(2);
        await Assert.That(animation.Frames[0][0, 0]).IsEqualTo(default);
        await Assert.That(animation.Frames[0][1, 0]).IsEqualTo(new Rgba32(40, 50, 60, 128));
        await Assert.That(animation.Frames[0][2, 0]).IsEqualTo(new Rgba32(70, 80, 90, 255));

        var gifMetadata = animation.Metadata.GetFormatMetadata(GifFormat.Instance);
        await Assert.That(gifMetadata.ColorTableMode).IsEqualTo(GifColorTableMode.Local);
        await Assert.That(gifMetadata.RepeatCount).IsEqualTo((ushort)3);
        await Assert.That(animation.Frames[0].Metadata.GetFormatMetadata(GifFormat.Instance).FrameDelay).IsEqualTo(15);
        await Assert.That(animation.Frames[1].Metadata.GetFormatMetadata(GifFormat.Instance).FrameDelay).IsEqualTo(25);
        foreach (var frame in animation.Frames)
        {
            await Assert.That(frame.Metadata.GetFormatMetadata(GifFormat.Instance).DisposalMethod).IsEqualTo(GifDisposalMethod.RestoreToBackground);
        }

        await Assert.That(first[0, 0]).IsEqualTo(new Rgba32(10, 20, 30, 0));
        await Assert.That(firstSourceMetadata.FrameDelay).IsEqualTo(99);
        await Assert.That(firstSourceMetadata.DisposalMethod).IsEqualTo(GifDisposalMethod.NotDispose);
        await Assert.That(secondSourceMetadata.FrameDelay).IsEqualTo(98);
        await Assert.That(secondSourceMetadata.DisposalMethod).IsEqualTo(GifDisposalMethod.RestoreToPrevious);
    }

    [Test]
    public async Task GetAnimatedMissingFrameDoesNotMutateEarlierFrames()
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
        await Assert.That(first[0, 0]).IsEqualTo(new Rgba32(10, 20, 30, 0));
        await Assert.That(sourceMetadata.FrameDelay).IsEqualTo(99);
        await Assert.That(sourceMetadata.DisposalMethod).IsEqualTo(GifDisposalMethod.NotDispose);
    }
}