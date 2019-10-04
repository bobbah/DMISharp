using DMISharp;
using System;
using System.Collections.Generic;
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
    }
}
