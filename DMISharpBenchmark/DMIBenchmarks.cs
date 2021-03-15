using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using DMISharp;

namespace DMISharpBenchmark
{
    [MemoryDiagnoser]
    public class DMIBenchmarks
    {
        [Benchmark]
        public DMIFile ReadSmallDMIFile()
        {
            return new DMIFile("Data/Input/small.dmi");
        }

        [Benchmark]
        public DMIFile ReadLargeDMIFile()
        {
            return new DMIFile("Data/Input/large.dmi");
        }

        [Benchmark]
        public void WriteDMIFile()
        {
            using var file = new DMIFile(@"Data/Input/air_meter.dmi");
            file.Save(@"Data/Output/air_meter_temp.dmi");
        }

        [Benchmark]
        public void SortDMIFile()
        {
            using var file = new DMIFile(@"Data/Input/animal.dmi");
            file.SortStates();
            file.Save(@"Data/Output/animal_sorted_alphabetically.dmi");
        }

        [Benchmark]
        public void WriteAnimations()
        {
            using var file = new DMIFile(@"Data/Input/animal.dmi");
            var toTest = file.States.First(x => x.Name == "mushroom");

            for (var dir = StateDirection.South; dir != StateDirection.SouthEast; dir++)
            {
                using var fs = File.OpenWrite($@"Data/Output/mushroom_gif_{dir}.gif");
                toTest.SaveAnimatedGIF(fs, dir);
            }
        }

        [Benchmark]
        public void AnimationConstructDoesNotDisposeFrames()
        {
            using var file = new DMIFile(@"Data/Input/animal.dmi");
            var toTest = file.States.First(x => x.Name == "mushroom");

            for (var dir = StateDirection.South; dir != StateDirection.SouthEast; dir++)
            {
                using var fs = File.OpenWrite($@"Data/Output/mushroom_A_{dir}.gif");
                toTest.SaveAnimatedGIF(fs, dir);
            }

            for (var dir = StateDirection.South; dir != StateDirection.SouthEast; dir++)
            {
                using var fs = File.OpenWrite($@"Data/Output/mushroom_B_{dir}.gif");
                toTest.SaveAnimatedGIF(fs, dir);
            }
        }

        [Benchmark]
        public void AnimationOfBarsignsConstructsCorrectly()
        {
            using var fs = File.OpenWrite(@"Data/Output/thegreytide.gif");
            using var file = new DMIFile(@"Data/Input/barsigns.dmi");
            var toTest = file.States.First(x => x.Name == "thegreytide");
            toTest.SaveAnimatedGIF(fs, StateDirection.South);
        }

        [Benchmark]
        public void AnimationOfSingularityConstructsCorrectly()
        {
            using var fs = File.OpenWrite(@"Data/Output/singularity_s11.gif");
            using var file = new DMIFile(@"Data/Input/352x352.dmi");
            var toTest = file.States.First(x => x.Name == "singularity_s11");
            toTest.SaveAnimatedGIF(fs, StateDirection.South);
        }
    }
}
