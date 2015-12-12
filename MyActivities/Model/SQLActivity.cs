using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lumia.Sense;
using SQLite;

namespace MyActivities.Model
{
    class SQLActivity
    {
        [PrimaryKey, AutoIncrement]
        public int ID { get; set; }
        public Activity Mode { get; set; }
        public DateTime DateTime { get; set; }
    }
}
