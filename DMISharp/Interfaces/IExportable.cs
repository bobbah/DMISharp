using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DMISharp.Interfaces
{
    public interface IExportable
    {
        void Save(Stream dataStream);
        bool CanSave();
    }
}
