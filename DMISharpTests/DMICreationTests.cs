using DMISharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace DMISharpTests
{
    public class DMICreationTests
    {
        [Fact]
        public void CanMergeDMIs()
        {
            using (var newDMI = new DMIFile(32, 32))
            {
                using (var firstFile = new DMIFile(@"Data/Input/centcom.dmi"))
                using (var secondFile = new DMIFile(@"Data/Input/misc.dmi"))
                {
                    newDMI.ImportStates(firstFile);
                    newDMI.ImportStates(secondFile);
                }

                Assert.True(newDMI.Save(@"Data/Output/merged.dmi"));
            }
        }
    }
}
