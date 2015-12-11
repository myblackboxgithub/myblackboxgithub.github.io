using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using MyActivities.Model;
using SQLite;

namespace MyActivities.Helpers
{
    public class DatabaseHelperClass
    {
        SQLiteConnection dbConn;

        public bool CreateDB(string Dbpath)
        {
            try
            {
                if (!CheckFileExists(Dbpath).Result)
                {
                    using (dbConn = new SQLiteConnection(Dbpath))
                    {
                        dbConn.CreateTable<SQLActivity>();
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> CheckFileExists(string path)
        {
            try
            {
                await ApplicationData.Current.LocalFolder.GetFileAsync(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

    }
}
