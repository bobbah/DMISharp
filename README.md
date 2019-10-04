<h1 align="center">
<img src="https://melonmesa.com/storage/dmisharp.png" alt="DMISharp" width="256"/>
<br/>
DMISharp
</h1>

<div align="center">
    [![Build Status](https://travis-ci.com/bobbahbrown/DMISharp.svg?branch=master)](https://travis-ci.com/bobbahbrown/DMISharp)
</div>

### **DMISharp** is a .NET Standard 2.0 library for easily interacting with the BYOND DMI file format.

Developed with ease-of-use in mind, this library provides a simple means for importing, modifying, and exporting DMI files. Internally, images are handled using [ImageSharp](https://github.com/SixLabors/ImageSharp), a fantastic open-source library from SixLabors.

### Installation

Install the current stable release from Nuget.
[![NuGet](https://img.shields.io/nuget/v/DMISharp.svg)](https://www.nuget.org/packages/DMISharp/)

### Getting Started

**Important things to note:**
- **Always** dispose DMIFile / DMIState objects. They use unmanaged memory for their sprites.

Ingesting a DMI file from a path:
```csharp
using (var file = new DMIFile("test.dmi"))
{
    // do cool things
}
```

Ingesting a DMI file from a stream:
```csharp
// Getting a memory stream from a database with the DMI file's data in the response
using (var dmiData = database.GetFile("test.dmi"))
using (var file = new DMIFile(dmiData))
{
    // do more cool things
}
```

Sorting a DMI file's states alphabetically, and saving the resulting DMI file:
```csharp
using (var file = new DMIFile("unsorted_states.dmi")) 
{
    file.SortStates();
    file.Save("sorted_states.dmi");
}
```

Sorting a DMI file's states using a provided comparer, and saving the resulting DMI file to a memory stream:
```csharp
using (var stream = new MemoryStream())
using (var file = new DMIFile("unsorted_states.dmi"))
{
    file.SortStates(new CoolCustomComparer());
    file.Save(stream);
}
```

### Questions?

Feel free to contact me on Discord at `bobbahbrown#0001` with any questions or concerns. Additionally, I can be found in the [/tg/station discord server](https://discord.gg/ss13).