using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLite.Net.Attributes;
using SQLiteNetExtensions.Attributes;

namespace ClassLibrary
{
    public class WeekHistory
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public myDayWeek myDayWeek { get; set; }
        public int NoOfDays { get; set; }

        [OneToMany(CascadeOperations = CascadeOperation.All)]
        public List<DayHistory> DayHistorys { get; set; }
    }

    public enum myDayWeek
    {
        monday = 1,
        tuesday,
        wednesday,
        thursday,
        friday,
        saturday,
        sunday
    }
}
