using System.IO;

namespace DMISharp.Interfaces;

public interface IExportable
{
    void Save(Stream dataStream);
    bool CanSave();
}