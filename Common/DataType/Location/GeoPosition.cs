// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*=============================================================================
**
** Class: GeoPosition
**
** Purpose: Represents a GeoPosition object
**
=============================================================================*/

using System;

namespace TKW.Framework.Common.DataType.Location;

public class GeoPosition<T>(DateTimeOffset timestamp, T position)
{
    #region Constructors

    public GeoPosition() :
        this(DateTimeOffset.MinValue, default)
    {
    }

    #endregion

    #region Properties

    public T Location { get; set; } = position;

    public DateTimeOffset Timestamp { get; set; } = timestamp;

    #endregion
}