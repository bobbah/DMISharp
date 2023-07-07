using System.IO;

namespace DMISharp.Interfaces;

/// <summary>
/// Provides a mechanism for saving an object to a stream
/// </summary>
public interface IExportable
{
    /// <summary>
    /// Saves the <see cref="IExportable"/> to the provided stream.
    /// </summary>
    /// <param name="dataStream">The stream to write the data to</param>
    void Save(Stream dataStream);
    
    /// <summary>
    /// Determines if the <see cref="IExportable"/> can be saved.
    /// </summary>
    /// <returns>True if savable, false otherwise</returns>
    bool CanSave();
}