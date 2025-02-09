using System.Linq;
using DomainTest.Models;
using HotChocolate;
using HotChocolate.Data;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain;

namespace DomainTest.Services;

public class VStaffQuery
{
    private readonly ProjectDomainUser _User;

    public VStaffQuery(SessionHelper sessionHelper)
    {
        _User = sessionHelper.AssertNotNull(nameof(sessionHelper)).NewGuestSession().User;//游客
    }

    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<VStaff> Staffs()
    {
        return _User.Use<VStaffService>().VStaffsQueryable();
    }
}