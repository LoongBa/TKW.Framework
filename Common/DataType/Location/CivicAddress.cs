using System;

namespace TKW.Framework.Common.DataType.Location;

public class CivicAddress
{
    public static readonly CivicAddress Unknown = new();

    //
    // private construcotr for creating single instance of CivicAddress.Unknown
    //
    private CivicAddress()
    {
        AddressLine1 = string.Empty;
        AddressLine2 = string.Empty;
        Building = string.Empty;
        City = string.Empty;
        CountryRegion = string.Empty;
        FloorLevel = string.Empty;
        PostalCode = string.Empty;
        StateProvince = string.Empty;
    }

    public CivicAddress(string addressLine1, string addressLine2, string building, string city, string countryRegion, string floorLevel, string postalCode, string stateProvince)
        : this()
    {
        var hasField = false;

        if (!string.IsNullOrEmpty(addressLine1))
        {
            hasField = true;
            AddressLine1 = addressLine1;
        }
        if (!string.IsNullOrEmpty(addressLine2))
        {
            hasField = true;
            AddressLine2 = addressLine2;
        }
        if (!string.IsNullOrEmpty(building))
        {
            hasField = true;
            Building = building;
        }
        if (!string.IsNullOrEmpty(city))
        {
            hasField = true;
            City = city;
        }
        if (!string.IsNullOrEmpty(countryRegion))
        {
            hasField = true;
            CountryRegion = countryRegion;
        }
        if (!string.IsNullOrEmpty(floorLevel))
        {
            hasField = true;
            FloorLevel = floorLevel;
        }
        if (!string.IsNullOrEmpty(postalCode))
        {
            hasField = true;
            PostalCode = postalCode;
        }

        if (!string.IsNullOrEmpty(stateProvince))
        {
            hasField = true;
            StateProvince = stateProvince;
        }

        if (!hasField)
        {
            throw new ArgumentException(Sr.ArgumentRequiresAtLeastOneNonEmptyStringParameter);
        }
    }

    public string AddressLine1 { get; private set; }
    public string AddressLine2 { get; private set; }
    public string Building { get; private set; }
    public string City { get; private set; }
    public string CountryRegion { get; private set; }
    public string FloorLevel { get; private set; }
    public string PostalCode { get; private set; }
    public string StateProvince { get; private set; }
}