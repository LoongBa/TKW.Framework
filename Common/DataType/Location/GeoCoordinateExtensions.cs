using System;
using System.Collections.Generic;
using System.Linq;

namespace TKW.Framework.Common.DataType.Location;

/// <summary>
/// GeoCoordinate 常用地理扩展方法（距离、格式化、有效性、多边形/矩形判断、坐标系转换、面积、路径等）
/// </summary>
public static class GeoCoordinateExtensions
{
    extension(GeoCoordinate coord)
    {
        /// <summary>
        /// 校验经纬度是否合法
        /// </summary>
        /// <param name="coord">地理坐标</param>
        /// <returns>经纬度均为合法数值时返回 true</returns>
        public bool IsValid()
            => coord is not null
               && !double.IsNaN(coord.Latitude)
               && !double.IsNaN(coord.Longitude)
               && coord.Latitude is >= -90 and <= 90
               && coord.Longitude is >= -180 and <= 180;

        /// <summary>
        /// 计算与另一点的球面距离（单位：米）
        /// </summary>
        /// <param name="coord">当前坐标</param>
        /// <param name="other">目标坐标</param>
        /// <returns>距离（米）</returns>
        public double DistanceTo(GeoCoordinate other)
            => coord.GetDistanceTo(other);

        /// <summary>
        /// 计算与一组点的球面距离（单位：米）
        /// </summary>
        /// <param name="coord">当前坐标</param>
        /// <param name="others">目标坐标集合</param>
        /// <returns>距离集合（米）</returns>
        public IEnumerable<double> DistancesTo(IEnumerable<GeoCoordinate> others)
            => others.Select(coord.GetDistanceTo);

        /// <summary>
        /// 计算一组点中距离当前点最近的点
        /// </summary>
        /// <param name="coord">当前坐标</param>
        /// <param name="points">目标坐标集合</param>
        /// <returns>最近的点，若集合为空则返回 null</returns>
        public GeoCoordinate? NearestTo(IEnumerable<GeoCoordinate> points)
        {
            GeoCoordinate? nearest = null;
            var minDist = double.MaxValue;
            foreach (var p in points)
            {
                var dist = coord.GetDistanceTo(p);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = p;
                }
            }
            return nearest;
        }

        /// <summary>
        /// 判断当前点是否在指定半径（米）范围内
        /// </summary>
        /// <param name="coord">当前坐标</param>
        /// <param name="center">圆心坐标</param>
        /// <param name="radiusMeters">半径（米）</param>
        /// <returns>在范围内返回 true</returns>
        public bool IsWithin(GeoCoordinate center, double radiusMeters)
            => coord.GetDistanceTo(center) <= radiusMeters;

        /// <summary>
        /// 输出度分秒格式字符串（如 31°12'45.123"N, 121°30'15.456"E）
        /// </summary>
        /// <param name="coord">地理坐标</param>
        /// <returns>度分秒格式字符串</returns>
        public string ToDmsString()
        {
            // 将经纬度转换为度分秒字符串
            return $"{Dms(coord.Latitude, "N", "S")}, {Dms(coord.Longitude, "E", "W")}";
        }

        /// <summary>
        /// 判断当前点是否在指定矩形区域内
        /// </summary>
        /// <param name="coord">当前坐标</param>
        /// <param name="leftTop">矩形左上角</param>
        /// <param name="rightBottom">矩形右下角</param>
        /// <returns>在区域内返回 true</returns>
        public bool IsInRectangle(GeoCoordinate leftTop, GeoCoordinate rightBottom)
        {
            if (!coord.IsValid() || !leftTop.IsValid() || !rightBottom.IsValid()) return false;
            return coord.Latitude <= leftTop.Latitude && coord.Latitude >= rightBottom.Latitude
                && coord.Longitude >= leftTop.Longitude && coord.Longitude <= rightBottom.Longitude;
        }

        /// <summary>
        /// 判断当前点是否在多边形内（射线法，适用于经纬度简单多边形）
        /// </summary>
        /// <param name="coord">当前坐标</param>
        /// <param name="polygon">多边形顶点集合（顺序闭合）</param>
        /// <returns>在多边形内返回 true</returns>
        public bool IsInPolygon(IEnumerable<GeoCoordinate> polygon)
        {
            if (!coord.IsValid() || polygon is null) return false;
            var points = polygon.ToList();
            var n = points.Count;
            if (n < 3) return false;
            var inside = false;
            double x = coord.Longitude, y = coord.Latitude;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double xi = points[i].Longitude, yi = points[i].Latitude;
                double xj = points[j].Longitude, yj = points[j].Latitude;
                // 射线法判断
                if (((yi > y) != (yj > y)) &&
                    (x < (xj - xi) * (y - yi) / (yj - yi + double.Epsilon) + xi))
                    inside = !inside;
            }
            return inside;
        }
    }

    /// <summary>
    /// 将单个经纬度值转换为度分秒字符串
    /// </summary>
    private static string Dms(double value, string pos, string neg)
    {
        var abs = Math.Abs(value);
        var deg = (int)abs;
        var min = (int)((abs - deg) * 60);
        var sec = (abs - deg - min / 60.0) * 3600;
        var dir = value >= 0 ? pos : neg;
        return $"{deg}°{min}'{sec:0.###}\"{dir}";
    }

    /// <summary>
    /// 计算多边形面积（单位：平方米，适用于小范围近似平面）
    /// </summary>
    /// <param name="polygon">多边形顶点集合（顺序闭合）</param>
    /// <returns>面积（平方米）</returns>
    public static double PolygonArea(this IEnumerable<GeoCoordinate> polygon)
    {
        var points = polygon.ToList();
        if (points.Count < 3) return 0;
        // 投影到平面，采用Shoelace公式
        double area = 0;
        double earthRadius = 6378137; // WGS84赤道半径
        for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
        {
            var xi = points[i].Longitude * Math.PI / 180 * earthRadius * Math.Cos(points[i].Latitude * Math.PI / 180);
            var yi = points[i].Latitude * Math.PI / 180 * earthRadius;
            var xj = points[j].Longitude * Math.PI / 180 * earthRadius * Math.Cos(points[j].Latitude * Math.PI / 180);
            var yj = points[j].Latitude * Math.PI / 180 * earthRadius;
            area += (xj * yi - xi * yj);
        }
        return Math.Abs(area / 2.0);
    }

    /// <summary>
    /// 计算路径总距离（折线距离，单位：米）
    /// </summary>
    /// <param name="points">路径点集合</param>
    /// <returns>总距离（米）</returns>
    public static double PathDistance(this IEnumerable<GeoCoordinate> points)
    {
        var list = points.ToList();
        if (list.Count < 2) return 0;
        double sum = 0;
        for (var i = 1; i < list.Count; i++)
        {
            sum += list[i - 1].GetDistanceTo(list[i]);
        }
        return sum;
    }

    /// <summary>
    /// 判断路径是否闭合（首尾点距离小于阈值，默认1米）
    /// </summary>
    public static bool IsClosedPath(this IEnumerable<GeoCoordinate> points, double threshold = 1.0)
    {
        var list = points.ToList();
        if (list.Count < 3) return false;
        return list.First().GetDistanceTo(list.Last()) <= threshold;
    }

    /// <summary>
    /// 计算路径中心点（所有点的平均值）
    /// </summary>
    public static GeoCoordinate PathCenter(this IEnumerable<GeoCoordinate> points)
    {
        var list = points.ToList();
        if (list.Count == 0) return GeoCoordinate.Unknown;
        var lat = list.Average(p => p.Latitude);
        var lng = list.Average(p => p.Longitude);
        return new GeoCoordinate(lat, lng);
    }

    /// <summary>
    /// 计算多边形重心（质心，适用于平面近似）
    /// </summary>
    public static GeoCoordinate PolygonCentroid(this IEnumerable<GeoCoordinate> polygon)
    {
        var points = polygon.ToList();
        if (points.Count < 3) return GeoCoordinate.Unknown;
        double area = 0, cx = 0, cy = 0;
        double earthRadius = 6378137;
        for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
        {
            var xi = points[i].Longitude * Math.PI / 180 * earthRadius * Math.Cos(points[i].Latitude * Math.PI / 180);
            var yi = points[i].Latitude * Math.PI / 180 * earthRadius;
            var xj = points[j].Longitude * Math.PI / 180 * earthRadius * Math.Cos(points[j].Latitude * Math.PI / 180);
            var yj = points[j].Latitude * Math.PI / 180 * earthRadius;
            var a = xj * yi - xi * yj;
            area += a;
            cx += (xi + xj) * a;
            cy += (yi + yj) * a;
        }
        area *= 0.5;
        if (Math.Abs(area) < 1e-10) return PathCenter(points);
        cx /= (6 * area);
        cy /= (6 * area);
        // 反投影回经纬度
        var lat = cy / earthRadius * 180 / Math.PI;
        var lng = cx / (earthRadius * Math.Cos(lat * Math.PI / 180)) * 180 / Math.PI;
        return new GeoCoordinate(lat, lng);
    }

    /// <summary>
    /// 获取点集合的最大/最小纬度经度点（边界点）
    /// </summary>
    public static (GeoCoordinate min, GeoCoordinate max) GetBounds(this IEnumerable<GeoCoordinate> points)
    {
        var list = points.ToList();
        if (list.Count == 0) return (GeoCoordinate.Unknown, GeoCoordinate.Unknown);
        var minLat = list.Min(p => p.Latitude);
        var maxLat = list.Max(p => p.Latitude);
        var minLng = list.Min(p => p.Longitude);
        var maxLng = list.Max(p => p.Longitude);
        return (new GeoCoordinate(minLat, minLng), new GeoCoordinate(maxLat, maxLng));
    }

    /// <summary>
    /// 获取点集合的外接矩形（左上、右下）
    /// </summary>
    public static (GeoCoordinate leftTop, GeoCoordinate rightBottom) GetBoundingBox(this IEnumerable<GeoCoordinate> points)
    {
        var (min, max) = points.GetBounds();
        return (new GeoCoordinate(max.Latitude, min.Longitude), new GeoCoordinate(min.Latitude, max.Longitude));
    }

    // ================== 坐标系转换（WGS84/GCJ02/BD09） ==================
    // 仅适用于中国大陆区域，常用算法实现，若无需求可移除

    private const double pi = Math.PI;
    private const double a = 6378245.0;
    private const double ee = 0.00669342162296594323;

    /// <summary>
    /// WGS84 转 GCJ02（火星坐标系，中国境内）
    /// </summary>
    public static GeoCoordinate ToGcj02(this GeoCoordinate wgs)
    {
        if (!IsInChina(wgs.Latitude, wgs.Longitude)) return new GeoCoordinate(wgs.Latitude, wgs.Longitude);
        var dLat = TransformLat(wgs.Longitude - 105.0, wgs.Latitude - 35.0);
        var dLon = TransformLon(wgs.Longitude - 105.0, wgs.Latitude - 35.0);
        var radLat = wgs.Latitude / 180.0 * pi;
        var magic = Math.Sin(radLat);
        magic = 1 - ee * magic * magic;
        var sqrtMagic = Math.Sqrt(magic);
        dLat = (dLat * 180.0) / ((a * (1 - ee)) / (magic * sqrtMagic) * pi);
        dLon = (dLon * 180.0) / (a / sqrtMagic * Math.Cos(radLat) * pi);
        var mgLat = wgs.Latitude + dLat;
        var mgLon = wgs.Longitude + dLon;
        return new GeoCoordinate(mgLat, mgLon);
    }

    /// <summary>
    /// GCJ02 转 WGS84（近似反解）
    /// </summary>
    public static GeoCoordinate ToWgs84(this GeoCoordinate gcj)
    {
        if (!IsInChina(gcj.Latitude, gcj.Longitude)) return new GeoCoordinate(gcj.Latitude, gcj.Longitude);
        var g2 = gcj.ToGcj02();
        var dLat = g2.Latitude - gcj.Latitude;
        var dLon = g2.Longitude - gcj.Longitude;
        return new GeoCoordinate(gcj.Latitude - dLat, gcj.Longitude - dLon);
    }

    /// <summary>
    /// GCJ02 转 BD09（百度坐标系）
    /// </summary>
    public static GeoCoordinate ToBd09(this GeoCoordinate gcj)
    {
        double x = gcj.Longitude, y = gcj.Latitude;
        var z = Math.Sqrt(x * x + y * y) + 0.00002 * Math.Sin(y * pi);
        var theta = Math.Atan2(y, x) + 0.000003 * Math.Cos(x * pi);
        var bdLon = z * Math.Cos(theta) + 0.0065;
        var bdLat = z * Math.Sin(theta) + 0.006;
        return new GeoCoordinate(bdLat, bdLon);
    }

    /// <summary>
    /// BD09 转 GCJ02
    /// </summary>
    public static GeoCoordinate Bd09ToGcj02(this GeoCoordinate bd)
    {
        double x = bd.Longitude - 0.0065, y = bd.Latitude - 0.006;
        var z = Math.Sqrt(x * x + y * y) - 0.00002 * Math.Sin(y * pi);
        var theta = Math.Atan2(y, x) - 0.000003 * Math.Cos(x * pi);
        var ggLon = z * Math.Cos(theta);
        var ggLat = z * Math.Sin(theta);
        return new GeoCoordinate(ggLat, ggLon);
    }

    /// <summary>
    /// BD09 转 WGS84
    /// </summary>
    public static GeoCoordinate Bd09ToWgs84(this GeoCoordinate bd)
    {
        var gcj = bd.Bd09ToGcj02();
        return gcj.ToWgs84();
    }

    /// <summary>
    /// 判断是否在中国大陆范围内（用于坐标系转换）
    /// </summary>
    private static bool IsInChina(double lat, double lon)
        => lon is >= 72.004 and <= 137.8347 && lat is >= 0.8293 and <= 55.8271;

    /// <summary>
    /// 坐标系转换辅助：纬度变换
    /// </summary>
    private static double TransformLat(double x, double y)
    {
        var ret = -100.0 + 2.0 * x + 3.0 * y + 0.2 * y * y + 0.1 * x * y + 0.2 * Math.Sqrt(Math.Abs(x));
        ret += (20.0 * Math.Sin(6.0 * x * pi) + 20.0 * Math.Sin(2.0 * x * pi)) * 2.0 / 3.0;
        ret += (20.0 * Math.Sin(y * pi) + 40.0 * Math.Sin(y / 3.0 * pi)) * 2.0 / 3.0;
        ret += (160.0 * Math.Sin(y / 12.0 * pi) + 320 * Math.Sin(y * pi / 30.0)) * 2.0 / 3.0;
        return ret;
    }

    /// <summary>
    /// 坐标系转换辅助：经度变换
    /// </summary>
    private static double TransformLon(double x, double y)
    {
        var ret = 300.0 + x + 2.0 * y + 0.1 * x * x + 0.1 * x * y + 0.1 * Math.Sqrt(Math.Abs(x));
        ret += (20.0 * Math.Sin(6.0 * x * pi) + 20.0 * Math.Sin(2.0 * x * pi)) * 2.0 / 3.0;
        ret += (20.0 * Math.Sin(x * pi) + 40.0 * Math.Sin(x / 3.0 * pi)) * 2.0 / 3.0;
        ret += (150.0 * Math.Sin(x / 12.0 * pi) + 300.0 * Math.Sin(x / 30.0 * pi)) * 2.0 / 3.0;
        return ret;
    }
}