using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WClouds_WPF.Logic
{
    public record Info
    {
        public DateTime? ChangedDate { get; }
        public TimeOnly? ChangedTime { get; }
        public double Size { get; }
        public int ChangedUser { get; }
        public int Owner { get; }
        public string Name { get; }

        public Info(DateTime? changedDate, TimeOnly? changedTime, double size, int changedUser, int owner, string name)
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
