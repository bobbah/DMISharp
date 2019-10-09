using DMISharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace DMISharpTests
{
    public class DMIWriteTests
    {
        [Fact]
        public void CanWriteDMIFile()
        {
            using (var file = new DMIFile(@"Data/Input/air_meter.dmi"))
            {
                file.Save(@"Data/Output/air_meter_temp.dmi");
            }
        }

        [Fact]
        public void CanSortDMIFile()
        {
            using (var file = new DMIFile(@"Data/Input/animal.dmi"))
            {
                file.SortStates();
                file.Save(@"Data/Output/animal_sorted_alphabetically.dmi");
            }
        }

        [Fact]
        public void CanWriteAnimations()
        {
            using (var file = new DMIFile(@"Data/Input/animal.dmi"))
            {
                var toTest = file.States.First(x => x.Name == "mushroom");

                for (StateDirection dir = StateDirection.South; dir != StateDirection.SouthEast; dir++)
                {
                    using (var fs = System.IO.File.OpenWrite($@"Data/Output/mushroom_gif_{dir}.gif"))
                    {
                        toTest.SaveAnimatedGIF(fs, dir);
                    }
                }
            }
        }

        /// <summary>
        /// Tests if gif frames are accidentally disposed after creating animation
        /// </summary>
        [Fact]
        public void AnimationConstructDoesNotDisposeFrames()
        {
            using (var file = new DMIFile(@"Data/Input/animal.dmi"))
            {
                var toTest = file.States.First(x => x.Name == "mushroom");

                for (StateDirection dir = StateDirection.South; dir != StateDirection.SouthEast; dir++)
                {
                    using (var fs = System.IO.File.OpenWrite($@"Data/Output/mushroom_A_{dir}.gif"))
                    {
                        toTest.SaveAnimatedGIF(fs, dir);
                    }
                }

                for (StateDirection dir = StateDirection.South; dir != StateDirection.SouthEast; dir++)
                {
                    using (var fs = System.IO.File.OpenWrite($@"Data/Output/mushroom_B_{dir}.gif"))
                    {
                        toTest.SaveAnimatedGIF(fs, dir);
                    }
                }
            }
        }
    }
}
