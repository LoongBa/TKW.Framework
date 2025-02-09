using System.Linq;
using Castle.Core.Logging;
using DomainTest.Managers;
using DomainTest.Models;
using Microsoft.Extensions.Logging;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;

namespace DomainTest.Services;

public class VStaffService : DomainService
{
    private readonly DomainTestDataAccessHelper _DaHelper;

    public VStaffService(DomainUser domainUser, DomainTestDataAccessHelper daHelper) : base(domainUser)
    {
        _DaHelper = daHelper.AssertNotNull(nameof(daHelper));
    }

    [AllowAnonymous]
    public IQueryable<VStaff> VStaffsQueryable()
    {
        return _DaHelper.CreateDbContextInstance<DomainTestContext>().VStaffs;// .VStaffsQueryable;
    }
}