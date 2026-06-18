namespace WClouds_WPF.Logic
{
    public record HistoryEntry
    {
        public int HistoryId { get; init; }
        public string? Date { get; init; }
        public string? Time { get; init; }
        public double SizeMb { get; init; }
        public string ChangedUser { get; init; } = "";
        public bool HasBackup { get; init; }

        public string DateTimeDisplay => Date != null ? $"{Date} {Time}" : "—";
        public string SizeDisplay => $"{SizeMb:N3} MB";
    }
}
