using System.Runtime.InteropServices;

namespace PersonalAppsHub.Services;

public sealed record RecycleBinItem(string Name, string OriginalLocation, long SizeBytes, DateTime? DeletedAt)
{
    public string SizeText => TemporaryFileCleanupService.FormatSize(SizeBytes);
    public string DeletedText => DeletedAt?.ToString("dd/MM/yyyy HH:mm") ?? "Inconnue";
}

public sealed record RecycleBinSnapshot(IReadOnlyList<RecycleBinItem> Items, long TotalSizeBytes, long ItemCount)
{
    public DateTime? OldestDeletion => Items.Where(item => item.DeletedAt.HasValue)
        .Select(item => item.DeletedAt).Min();
}

public sealed class RecycleBinService
{
    private const uint NoConfirmation = 0x00000001;
    private const uint NoProgressUi = 0x00000002;
    private const uint NoSound = 0x00000004;

    public Task<RecycleBinSnapshot> ScanAsync() => Task.Run(Scan);

    private static RecycleBinSnapshot Scan()
    {
        var info = new QueryRecycleBinInfo { Size = Marshal.SizeOf<QueryRecycleBinInfo>() };
        var result = SHQueryRecycleBin(null, ref info);
        if (result != 0) Marshal.ThrowExceptionForHR(result);

        var items = new List<RecycleBinItem>();
        object? shell = null;
        object? folder = null;
        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application")
                ?? throw new InvalidOperationException("L’interface de la Corbeille Windows est indisponible.");
            shell = Activator.CreateInstance(shellType);
            dynamic dynamicShell = shell!;
            folder = dynamicShell.Namespace(10);
            if (folder is null) return new RecycleBinSnapshot(items, info.TotalSize, info.ItemCount);
            dynamic dynamicFolder = folder;
            foreach (var itemObject in dynamicFolder.Items())
            {
                try
                {
                    dynamic item = itemObject;
                    var name = Convert.ToString(item.Name) ?? "Élément supprimé";
                    var originalLocation = Convert.ToString(item.ExtendedProperty("System.Recycle.DeletedFrom")) ?? "Inconnu";
                    var size = Math.Max(0, Convert.ToInt64(item.Size));
                    var deletedValue = item.ExtendedProperty("System.Recycle.DateDeleted");
                    DateTime? deletedAt = deletedValue is DateTime date ? date :
                        DateTime.TryParse(Convert.ToString(deletedValue), out DateTime parsed) ? parsed : null;
                    items.Add(new RecycleBinItem(name, originalLocation, size, deletedAt));
                }
                catch { }
                finally { if (Marshal.IsComObject(itemObject)) Marshal.FinalReleaseComObject(itemObject); }
            }
        }
        finally
        {
            if (folder is not null && Marshal.IsComObject(folder)) Marshal.FinalReleaseComObject(folder);
            if (shell is not null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
        }
        return new RecycleBinSnapshot(items.OrderBy(item => item.DeletedAt ?? DateTime.MaxValue).ToArray(),
            info.TotalSize, info.ItemCount);
    }

    public void Empty()
    {
        var result = SHEmptyRecycleBin(IntPtr.Zero, null, NoConfirmation | NoProgressUi | NoSound);
        if (result != 0) Marshal.ThrowExceptionForHR(result);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    private struct QueryRecycleBinInfo
    {
        public int Size;
        public long TotalSize;
        public long ItemCount;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? rootPath, ref QueryRecycleBinInfo info);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr window, string? rootPath, uint flags);
}
