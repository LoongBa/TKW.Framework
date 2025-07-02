using System;
using System.Globalization;
using System.Text.Json.Serialization;

namespace TKW.Framework.Common.DataType.Location;

public class GeoCoordinate : IEquatable<GeoCoordinate>
{
    private double _MLatitude = double.NaN;
    private double _MLongitude = double.NaN;
    private double _MVerticalAccuracy = double.NaN;
    private double _MHorizontalAccuracy = double.NaN;
    private double _MSpeed = double.NaN;
    private double _MCourse = double.NaN;

    public static readonly GeoCoordinate Unknown = new();

    internal CivicAddress MAddress = CivicAddress.Unknown;

    #region Constructors

    public GeoCoordinate() { }

    public GeoCoordinate(double latitude, double longitude)
        : this(latitude, longitude, double.NaN) { }

    public GeoCoordinate(double latitude, double longitude, double altitude)
        : this(latitude, longitude, altitude, double.NaN, double.NaN, double.NaN, double.NaN) { }

    public GeoCoordinate(double latitude, double longitude, double altitude,
        double horizontalAccuracy, double verticalAccuracy, double speed, double course)
    {
        Latitude = latitude;
        Longitude = longitude;
        Altitude = altitude;
        HorizontalAccuracy = horizontalAccuracy;
        VerticalAccuracy = verticalAccuracy;
        Speed = speed;
        Course = course;
    }

    // 兼容 GeoPoint 的构造
    public GeoCoordinate(int latitudeE6, int longitudeE6)
        : this(latitudeE6 / 1_000_000.0, longitudeE6 / 1_000_000.0) { }

    public GeoCoordinate(string latitude, string longitude)
        : this(double.Parse(latitude), double.Parse(longitude)) { }

    public GeoCoordinate(GeoCoordinate other)
        : this(other.Latitude, other.Longitude, other.Altitude, other.HorizontalAccuracy, other.VerticalAccuracy, other.Speed, other.Course) { }

    #endregion

    #region Properties

    public double Latitude
    {
        get => _MLatitude;
        set
        {
            if (value > 90.0 || value < -90.0)
                throw new ArgumentOutOfRangeException(nameof(value), "Latitude must be in [-90, 90]");
            _MLatitude = value;
        }
    }

    public double Longitude
    {
        get => _MLongitude;
        set
        {
            if (value > 180.0 || value < -180.0)
                throw new ArgumentOutOfRangeException(nameof(value), "Longitude must be in [-180, 180]");
            _MLongitude = value;
        }
    }

    public double Altitude { get; set; } = double.NaN;

    public double HorizontalAccuracy
    {
        get => _MHorizontalAccuracy;
        set
        {
            if (value < 0.0)
                throw new ArgumentOutOfRangeException(nameof(value), "HorizontalAccuracy must be non-negative");
            _MHorizontalAccuracy = (value == 0.0) ? double.NaN : value;
        }
    }

    public double VerticalAccuracy
    {
        get => _MVerticalAccuracy;
        set
        {
            if (value < 0.0)
                throw new ArgumentOutOfRangeException(nameof(value), "VerticalAccuracy must be non-negative");
            _MVerticalAccuracy = (value == 0.0) ? double.NaN : value;
        }
    }

    public double Speed
    {
        get => _MSpeed;
        set
        {
            if (value < 0.0)
                throw new ArgumentOutOfRangeException(nameof(value), "Speed must be non-negative");
            _MSpeed = value;
        }
    }

    public double Course
    {
        get => _MCourse;
        set
        {
            if (value < 0.0 || value > 360.0)
                throw new ArgumentOutOfRangeException(nameof(value), "Course must be in [0, 360]");
            _MCourse = value;
        }
    }

    public bool IsUnknown => this.Equals(GeoCoordinate.Unknown);

    /// <summary>
    /// 维度的 1E6 表示
    /// </summary>
    [JsonIgnore]
    public int LatitudeE6
    {
        get => (int)(Latitude * 1_000_000);
        set => Latitude = value / 1_000_000.0;
    }

    /// <summary>
    /// 经度的 1E6 表示
    /// </summary>
    [JsonIgnore]
    public int LongitudeE6
    {
        get => (int)(Longitude * 1_000_000);
        set => Longitude = value / 1_000_000.0;
    }

    #endregion

    #region String Conversion

    public static GeoCoordinate FromLatLngString(string latLngString)
    {
        var data = latLngString.Split(",", StringSplitOptions.RemoveEmptyEntries);
        return new GeoCoordinate(data[0], data[1]);
    }

    public static GeoCoordinate FromLngLatString(string lngLatString)
    {
        var data = lngLatString.Split(",", StringSplitOptions.RemoveEmptyEntries);
        return new GeoCoordinate(data[1], data[0]);
    }

    public string ToLatLngString() => $"{Latitude}, {Longitude}";
    public string ToLngLatString() => $"{Longitude}, {Latitude}";

    #endregion

    #region 类型转换

    // 如需兼容 GeoPoint，可实现如下方法
    // public static GeoCoordinate FromGeoPoint(GeoPoint point) => new(point.LatitudeE6, point.LongitudeE6);
    // public GeoPoint ToGeoPoint() => new GeoPoint(LatitudeE6, LongitudeE6);

    #endregion

    #region Methods

    public double GetDistanceTo(GeoCoordinate other)
    {
        if (double.IsNaN(this.Latitude) || double.IsNaN(this.Longitude) ||
            double.IsNaN(other.Latitude) || double.IsNaN(other.Longitude))
        {
            throw new ArgumentException("Latitude or Longitude is not a number.");
        }

        var dLat1 = this.Latitude * (Math.PI / 180.0);
        var dLon1 = this.Longitude * (Math.PI / 180.0);
        var dLat2 = other.Latitude * (Math.PI / 180.0);
        var dLon2 = other.Longitude * (Math.PI / 180.0);

        var dLon = dLon2 - dLon1;
        var dLat = dLat2 - dLat1;

        var a = Math.Pow(Math.Sin(dLat / 2.0), 2.0) +
                Math.Cos(dLat1) * Math.Cos(dLat2) *
                Math.Pow(Math.Sin(dLon / 2.0), 2.0);

        var c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        const double kEarthRadiusMs = 6376500;
        var dDistance = kEarthRadiusMs * c;

        return dDistance;
    }

    #endregion

    #region Object overrides

    public override int GetHashCode() => Latitude.GetHashCode() ^ Longitude.GetHashCode();

    public override bool Equals(object obj)
    {
        return obj is not GeoCoordinate coordinate ? base.Equals(obj) : Equals(coordinate);
    }

    public override string ToString()
    {
        if (this == GeoCoordinate.Unknown)
        {
            return "Unknown";
        }
        else
        {
            return Latitude.ToString("G", CultureInfo.InvariantCulture) + ", " +
                   Longitude.ToString("G", CultureInfo.InvariantCulture);
        }
    }

    #endregion

    #region IEquatable
    public bool Equals(GeoCoordinate other)
    {
        return other is not null && Latitude.Equals(other.Latitude) && Longitude.Equals(other.Longitude);
    }
    #endregion

    #region Public static operators
    public static bool operator ==(GeoCoordinate left, GeoCoordinate right)
    {
        return left?.Equals(right) ?? right is null;
    }

    public static bool operator !=(GeoCoordinate left, GeoCoordinate right)
    {
        return !(left == right);
    }
    #endregion

    #region 静态工厂方法

    public static GeoCoordinate FromE6(int latitudeE6, int longitudeE6) => new(latitudeE6, longitudeE6);

    public static GeoCoordinate FromString(string lat, string lng) => new(lat, lng);
    
    #endregion
}