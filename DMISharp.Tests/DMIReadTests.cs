using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace DMISharp.Tests;

public class DMIReadTests
{
    [Fact]
    public void AnimalDMIStateCount()
    {
        using var file = new DMIFile(@"Data/Input/animal.dmi");
        Assert.Equal(154, file.States.Count);
    }

    [Fact]
    public void AirMeterDMIStateCount()
    {
        using var file = new DMIFile(@"Data/Input/air_meter.dmi");
        Assert.Equal(16, file.States.Count);
    }

    [Fact]
    public void AtmosTestingDMIStateCount()
    {
        using var file = new DMIFile(@"Data/Input/atmos_testing.dmi");
        Assert.Equal(5, file.States.Count);
    }

    [Fact]
    public void LightingDMIStateCount()
    {
        using var file = new DMIFile(@"Data/Input/lighting.dmi");
        Assert.Equal(3, file.States.Count);
    }

    [Fact]
    public void TurfAnalysisDMIStateCount()
    {
        using var file = new DMIFile(@"Data/Input/turf_analysis.dmi");
        Assert.Equal(16, file.States.Count);
    }

    [Fact]
    public void SpaceDragonDMIStateCount()
    {
        using var file = new DMIFile(@"Data/Input/spacedragon.dmi");
        Assert.Equal(2, file.States.Count);
    }

    [Fact]
    public void CanReadDirectionDepth()
    {
        using var file = new DMIFile(@"Data/Input/animal.dmi");

        // Arrange
        var bat = file.States.First(x => x.Name == "bat");
        var carp = file.States.First(x => x.Name == "parrot_fly");
        var rareFrogDead = file.States.First(x => x.Name == "rare_frog_dead");

        // Assert
        Assert.Equal(DirectionDepth.Four, bat.DirectionDepth);
        Assert.Equal(DirectionDepth.Four, carp.DirectionDepth);
        Assert.Equal(DirectionDepth.One, rareFrogDead.DirectionDepth);
    }

    [Fact]
    public void GoonTurfAnalysisDMIStateCount()
    {
        using var file = new DMIFile(@"Data/Input/turf_analysis_goon.dmi");
        Assert.Equal(16, file.States.Count);
    }

    [Fact]
    public void CanReadFromNonSeekableStream()
    {
        var stream = new NonSeekableReadStream(File.OpenRead(@"Data/Input/animal.dmi"));

        using var file = new DMIFile(stream);

        Assert.Equal(154, file.States.Count);
        Assert.True(stream.IsDisposed);
        Assert.NotNull(file.States.First().GetFrame(0));
    }

    [Fact]
    public void LoadedFramesRemainIndependentlyMutable()
    {
        using var file = new DMIFile(@"Data/Input/animal.dmi");
        var state = file.States.First(x => x.Name == "mushroom");
        var first = state.GetFrame(StateDirection.South, 0)!;
        var second = state.GetFrame(StateDirection.North, 0)!;
        var secondPixel = second[0, 0];
        var replacement = new Rgba32(1, 2, 3, 4);

        first[0, 0] = replacement;

        Assert.Equal(replacement, first[0, 0]);
        Assert.Equal(secondPixel, second[0, 0]);
        Assert.Same(first, state.GetFrame(StateDirection.South, 0));
    }

    [Fact]
    public void RemovedStateRetainsUnmaterializedFrames()
    {
        DMIState state;
        using (var file = new DMIFile(@"Data/Input/turf_analysis.dmi"))
        {
            state = file.States.Last();
            Assert.True(file.RemoveState(state));
        }

        using (state)
        {
            Assert.NotNull(state.GetFrame(0));
        }
    }

    [Fact]
    public void ConcurrentFrameReadsReturnSameOwnedImage()
    {
        using var file = new DMIFile(@"Data/Input/animal.dmi");
        var state = file.States.First(x => x.Name == "mushroom");
        var frames = new object[64];

        Parallel.For(0, frames.Length, i => frames[i] = state.GetFrame(StateDirection.South, 0)!);

        Assert.All(frames, frame => Assert.Same(frames[0], frame));
    }

    private sealed class NonSeekableReadStream : Stream
    {
        private readonly Stream _inner;

        public NonSeekableReadStream(Stream inner)
        {
            _inner = inner;
        }

        public bool IsDisposed { get; private set; }

        public override bool CanRead => _inner.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
                IsDisposed = true;
            }

            base.Dispose(disposing);
        }
    }
}