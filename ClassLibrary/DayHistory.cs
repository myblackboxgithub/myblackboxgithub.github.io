using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lumia.Sense;
using SQLite.Net.Attributes;
using SQLiteNetExtensions.Attributes;

namespace ClassLibrary
{
    public class DayHistory
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int Hour { get; set; }
        public Activity ActivityName { get; set; }
        public TimeSpan ActivityTime { get; set; }

        [ForeignKey(typeof(WeekHistory))]
        public int WeekId { get; set; }
        [ManyToOne]
        public WeekHistory WeekHistory { get; set; }

        public DayHistory()
        { }

        public DayHistory(int h, Activity s, TimeSpan i)
        {
            Hour = h;
            ActivityName = s;
            ActivityTime = i;
        }
    }
}
