using System;
using System.Collections.Generic;
using System.Text;

namespace WClouds_WPF.Logic
{
    public record FileExplorerEntry : Info
    {
        public int Id { get; }
        public bool IsFolder { get; }

        public FileExplorerEntry(Info info, int id, bool isFolder)
            : base(info.ChangedDate, info.ChangedTime, info.Size, info.ChangedUser, info.Owner, info.Name)
        {
            Id = id;
            IsFolder = isFolder;
        }

        public string Icon => IsFolder ? "📁" : DataPage.GetFileIcon(Name);
        public string ChangedDisplay =>
            ChangedDate.HasValue ? $"{ChangedDate:dd.MM.yyyy} {ChangedTime}" : "—";
    }
}
