using System;
using System.Text.Json.Serialization;

namespace TKW.Framework.Common.DataType
{
    public class GeoPoint
    {
        public GeoPoint(double latitude, double longitude)
        {
            LatitudeE6 = (int)(latitude * 1000000);
            LongitudeE6 = (int)(longitude * 1000000);
        }
        public GeoPoint(int latitude, int longitude)
        {
            LatitudeE6 = latitude;
            LongitudeE6 = longitude;
        }
        public GeoPoint(string latitude, string longitude)
        {
            LatitudeE6 = (int)(double.Parse(latitude) * 1000000);
            LongitudeE6 = (int)(double.Parse(longitude) * 1000000);
        }

        public GeoPoint(GeoPoint centerPoint)
        {
            LatitudeE6 = centerPoint.LatitudeE6;
            LongitudeE6 = centerPoint.LongitudeE6;
        }

        /// <summary>
        /// 维度的 1E6 表示
        /// </summary>
        [JsonIgnore]
        public int LatitudeE6 { get; protected set; }

        /// <summary>
        /// 经度的 1E6 表示
        /// </summary>
        [JsonIgnore]
        public int LongitudeE6 { get; protected set; }

        public double Latitude => (double)LatitudeE6 / 1000000;
        public double Longitude => (double)LongitudeE6 / 1000000;

        public static GeoPoint FromLatLngString(string latLngString)
        {
            var data = latLngString.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            return new GeoPoint(data[0], data[1]);
        }
        public static GeoPoint FromLngLatString(string lngLatString)
        {
            var data = lngLatString.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            return new GeoPoint(data[1], data[0]);
        }

        public string ToLatLngString()
        {
            return $"{Latitude}, {Longitude}";
        }
        public string ToLngLatString()
        {
            return $"{Longitude}, {Latitude}";
        }
    }
}
