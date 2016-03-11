using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ActivityBackground.DataModel
{
    internal class ToastContent
    {
        public string Id { get; set; }

        [JsonProperty(PropertyName = "userid")]
        public string UserId { get; set; }

        [JsonProperty(PropertyName = "toasttext")]
        public string toasttext { get; set; }

        [JsonProperty(PropertyName = "patient_status")]
        public string patient_status { get; set; }

        [JsonProperty(PropertyName = "longitude")]
        public double Longitude { get; set; }

        [JsonProperty(PropertyName = "latitude")]
        public double Latitude { get; set; }

        [JsonProperty(PropertyName = "alert_time")]
        public string alert_time { get; set; }
    }
}
