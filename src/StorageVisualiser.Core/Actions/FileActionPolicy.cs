using System;
using System.Collections.Generic;

namespace StorageVisualiser.Core.Actions;

public enum DeleteMode : byte
{
    Disabled = 0,
    RecycleBinOnly = 1,
    AllowPermanent = 2
}

public sealed class FileActionPolicy
{
    public DeleteMode Mode { get; set; } = DeleteMode.RecycleBinOnly;

    public List<string> AdditionalProtectedPaths { get; set; } = [];

    public bool AllowPermanentDelete => Mode == DeleteMode.AllowPermanent;

    public bool IsDeleteAllowed => Mode != DeleteMode.Disabled;
}
