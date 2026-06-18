using System;

namespace WClouds_WPF.Logic
{
    public record Info
    {
        public string? ChangedDate { get; }
        public string? ChangedTime { get; }
        public double Size { get; }
        public string ChangedUser { get; }
        public string Owner { get; }
        public string Name { get; }

        public Info(string? changedDate, string? changedTime, double size, string changedUser, string owner, string name)
        {
            ChangedDate = changedDate;
            ChangedTime = changedTime;
            Size = size;
            ChangedUser = changedUser;
            Owner = owner;
            Name = name;
        }
    }
}
