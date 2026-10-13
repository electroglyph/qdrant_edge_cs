namespace Qdrant.Edge;

/// <summary>Geographic point (longitude, latitude).</summary>
public sealed record GeoPoint
{
    /// <summary>Longitude in [-180, 180].</summary>
    public required double Lon { get; init; }

    /// <summary>Latitude in [-90, 90].</summary>
    public required double Lat { get; init; }

    /// <summary>Validates ranges and finiteness.</summary>
    public void Validate() => EdgeValidation.CheckGeoPoint(Lon, Lat);
}

/// <summary>Geographic bounding box.</summary>
public sealed record GeoBoundingBox
{
    /// <summary>Top-left corner.</summary>
    public required GeoPoint TopLeft { get; init; }

    /// <summary>Bottom-right corner.</summary>
    public required GeoPoint BottomRight { get; init; }
}

/// <summary>Geographic radius.</summary>
public sealed record GeoRadius
{
    /// <summary>Circle center.</summary>
    public required GeoPoint Center { get; init; }

    /// <summary>Radius in meters; must be finite and non-negative.</summary>
    public required double Radius { get; init; }

    /// <summary>Validates center and radius.</summary>
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Center, nameof(Center));
        Center.Validate();
        EdgeValidation.CheckFinite(nameof(Radius), Radius);
        if (Radius < 0.0)
        {
            throw new EdgeInvalidArgumentException($"geo radius {Radius} must be >= 0");
        }
    }
}

/// <summary>Geographic line string (polygon ring).</summary>
public sealed record GeoLineString
{
    /// <summary>Ring points.</summary>
    public required IReadOnlyList<GeoPoint> Points { get; init; }

    /// <summary>Validates each point.</summary>
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Points, nameof(Points));
        foreach (GeoPoint point in Points)
        {
            ArgumentNullException.ThrowIfNull(point, nameof(point));
            point.Validate();
        }
    }
}

/// <summary>Geographic polygon with exterior ring and optional interior holes.</summary>
public sealed record GeoPolygon
{
    /// <summary>Exterior ring.</summary>
    public required GeoLineString Exterior { get; init; }

    /// <summary>Interior holes.</summary>
    public IReadOnlyList<GeoLineString>? Interiors { get; init; }

    /// <summary>Validates closed rings of at least 4 points.</summary>
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Exterior, nameof(Exterior));
        CheckRing(Exterior, nameof(Exterior));
        if (Interiors is not null)
        {
            foreach (GeoLineString interior in Interiors)
            {
                ArgumentNullException.ThrowIfNull(interior, nameof(interior));
                CheckRing(interior, "interior");
            }
        }

        static void CheckRing(GeoLineString ring, string name)
        {
            ring.Validate();
            if (ring.Points.Count < 4)
            {
                throw new EdgeInvalidArgumentException($"geo polygon {name} ring needs at least 4 points, got {ring.Points.Count}");
            }

            GeoPoint first = ring.Points[0];
            GeoPoint last = ring.Points[^1];
            if (first.Lon != last.Lon || first.Lat != last.Lat)
            {
                throw new EdgeInvalidArgumentException($"geo polygon {name} ring must be closed");
            }
        }
    }
}
