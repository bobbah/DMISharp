using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SixLabors.ImageSharp.PixelFormats;

namespace DMISharp.Tests;

internal sealed class DMIReadTests
{
    [Test]
    public async Task AnimalDMIStateCount()
    {
        using var file = new DMIFile(@"Data/Input/animal.dmi");
        await Assert.That(file.States.Count).IsEqualTo(154);
    }

    [Test]
    public async Task AirMeterDMIStateCount()
    {
        using var file = new DMIFile(@"Data/Input/air_meter.dmi");
        await Assert.That(file.States.Count).IsEqualTo(16);
    }

    [Test]
    public async Task AtmosTestingDMIStateCount()
    {
        using var file = new DMIFile(@"Data/Input/atmos_testing.dmi");
        await Assert.That(file.States.Count).IsEqualTo(5);
    }

    [Test]
    public async Task LightingDMIStateCount()
    {
        using var file = new DMIFile(@"Data/Input/lighting.dmi");
        await Assert.That(file.States.Count).IsEqualTo(3);
    }

    [Test]
    public async Task TurfAnalysisDMIStateCount()
    {
        using var file = new DMIFile(@"Data/Input/turf_analysis.dmi");
        await Assert.That(file.States.Count).IsEqualTo(16);
    }

    [Test]
    public async Task SpaceDragonDMIStateCount()
    {
        using var file = new DMIFile(@"Data/Input/spacedragon.dmi");
        await Assert.That(file.States.Count).IsEqualTo(2);
    }

    [Test]
    public async Task CanReadDirectionDepth()
    {
        using var file = new DMIFile(@"Data/Input/animal.dmi");

        // Arrange
        var bat = file.States.First(x => x.Name == "bat");
        var carp = file.States.First(x => x.Name == "parrot_fly");
        var rareFrogDead = file.States.First(x => x.Name == "rare_frog_dead");

        // Assert
        await Assert.That(bat.DirectionDepth).IsEqualTo(DirectionDepth.Four);
        await Assert.That(carp.DirectionDepth).IsEqualTo(DirectionDepth.Four);
        await Assert.That(rareFrogDead.DirectionDepth).IsEqualTo(DirectionDepth.One);
    }

    [Test]
    public async Task GoonTurfAnalysisDMIStateCount()
    {
        using var file = new DMIFile(@"Data/Input/turf_analysis_goon.dmi");
        await Assert.That(file.States.Count).IsEqualTo(16);
    }

    [Test]
    public async Task CanReadFromNonSeekableStream()
    {
        var stream = new NonSeekableReadStream(File.OpenRead(@"Data/Input/animal.dmi"));

        using var file = new DMIFile(stream);

        await Assert.That(file.States.Count).IsEqualTo(154);
        await Assert.That(stream.IsDisposed).IsTrue();
        await Assert.That(file.States.First().GetFrame(0)).IsNotNull();
    }

    [Test]
    public async Task LoadedFramesRemainIndependentlyMutable()
    {
        using var file = new DMIFile(@"Data/Input/animal.dmi");
        var state = file.States.First(x => x.Name == "mushroom");
        var first = state.GetFrame(StateDirection.South, 0)!;
        var second = state.GetFrame(StateDirection.North, 0)!;
        var secondPixel = second[0, 0];
        var replacement = new Rgba32(1, 2, 3, 4);

        first[0, 0] = replacement;

        await Assert.That(first[0, 0]).IsEqualTo(replacement);
        await Assert.That(second[0, 0]).IsEqualTo(secondPixel);
        await Assert.That(state.GetFrame(StateDirection.South, 0)).IsSameReferenceAs(first);
    }

    [Test]
    public async Task RemovedStateRetainsUnmaterializedFrames()
    {
        DMIState state;
        using (var file = new DMIFile(@"Data/Input/turf_analysis.dmi"))
        {
            state = file.States.Last();
            await Assert.That(file.RemoveState(state)).IsTrue();
        }

        using (state)
        {
            await Assert.That(state.GetFrame(0)).IsNotNull();
        }
    }

    [Test]
    public async Task ConcurrentFrameReadsReturnSameOwnedImage()
    {
        using var file = new DMIFile(@"Data/Input/animal.dmi");
        var state = file.States.First(x => x.Name == "mushroom");
        var frames = new object[64];

        Parallel.For(0, frames.Length, i => frames[i] = state.GetFrame(StateDirection.South, 0)!);

        foreach (var frame in frames)
            await Assert.That(frame).IsSameReferenceAs(frames[0]);
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