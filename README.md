<h1 align="center">
<img src="https://melonmesa.com/storage/dmisharp.png" alt="DMISharp" width="256"/>
<br/>
DMISharp
</h1>

### **DMISharp** is a .NET 5.0 library for easily interacting with the BYOND DMI file format.

Developed with ease-of-use in mind, this library provides a simple means for importing, modifying, and exporting DMI files. Internally, images are handled using [ImageSharp](https://github.com/SixLabors/ImageSharp), a fantastic open-source library from SixLabors.

### Installation

Install the current stable release from Nuget.

[![NuGet](https://img.shields.io/nuget/v/DMISharp.svg)](https://www.nuget.org/packages/DMISharp/)

### Getting Started

**Important things to note:**
- **Always** dispose DMIFile / DMIState objects. They use unmanaged memory for their sprites.

Ingesting a DMI file from a path:
```csharp
using var file = new DMIFile("test.dmi")
// do cool things
```

Ingesting a DMI file from a stream:
```csharp
// Getting a memory stream from a database with the DMI file's data in the response
using var dmiData = database.GetFile("test.dmi")
using var file = new DMIFile(dmiData)
// do more cool things
```

Sorting a DMI file's states alphabetically, and saving the resulting DMI file:
```csharp
using var file = new DMIFile("unsorted_states.dmi")
file.SortStates();
file.Save("sorted_states.dmi");
```

Sorting a DMI file's states using a provided comparer, and saving the resulting DMI file to a memory stream:
```csharp
using var stream = new MemoryStream()
using var file = new DMIFile("unsorted_states.dmi")
file.SortStates(new CoolCustomComparer());
file.Save(stream);
```

Creating a new DMI file with 32x32 pixel states, and creating 3 states from 3 source images:
```csharp
using var newDMI = new DMIFile(32, 32)
var sourceData = new List<string>() { "sord", "sordvert", "steve32" };

foreach (var source in sourceData)
{
    var img = Image.Load<Rgba32>($@"Data/Input/SourceImages/{source}.png");
    var newState = new DMIState(source, DirectionDepth.One, 1, 32, 32);
    newState.SetFrame(img, 0);
    newDMI.AddState(newState);
}

newDMI.Save(@"Data/Output/minecraft.dmi");
```

Creating a new DMI file with 32x32 pixel states, creating a state, and then modifying that state's frame and direction depth:
```csharp
using var newDMI = new DMIFile(32, 32)

// Create state
var img = Image.Load<Rgba32>($@"Data/Input/SourceImages/steve32.png");
var newState = new DMIState("steve32", DirectionDepth.One, 1, 32, 32);
newState.SetFrame(img, 0);
newDMI.AddState(newState);

// Modifying state
newDMI.States.First().SetDirectionDepth(DirectionDepth.Four);
newDMI.States.First().SetFrameCount(10);

// At this point you would add the new frames for each direction, otherwise
// you cannot save the file.

// Saving
newDMI.Save(@"Data/Output/minecraft.dmi");
```


### Questions?

Feel free to contact me on Discord at `bobbahbrown#0001` with any questions or concerns. Additionally, I can be found in the [/tg/station discord server](https://tgstation13.org/phpBB/viewforum.php?f=60).
