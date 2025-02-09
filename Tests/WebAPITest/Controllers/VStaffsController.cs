using DomainTest;
using DomainTest.Models;
using DomainTest.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain;

namespace WebAPITest.Controllers;

[Route("api/[controller]")]
[ApiController]
public class VStaffsController : ODataController
{
    private readonly ProjectDomainUser _User;
    private readonly VStaffService _VStaffService;

    public VStaffsController(SessionHelper sessionHelper)
    {
        //游客
        var session = sessionHelper.AssertNotNull(nameof(sessionHelper)).NewGuestSession();

        //登录
        //var session = sessionHelper.AssertNotNull(nameof(sessionHelper)).UserLogin("", "", UserAuthenticationType.WechatApp);

        //会话
        //var sessionKey = "xxxxxxxxxxx"; //来自 Cookie/localStorage/Header/QueryString 等等
        //session = sessionHelper.AssertNotNull(nameof(sessionHelper)).RetrieveAndActiveUserSession(sessionKey);

        //获得用户实例
        _User = session.User;
        _VStaffService = _User.Use<VStaffService>();
    }

    [HttpGet]
    [EnableQuery(MaxTop = 100)]
    public IQueryable<DomainTest.Models.VStaff> Get()
    {
        return _VStaffService.VStaffsQueryable();
    }

    /*[HttpGet("/all")]
    [EnableQuery(MaxTop = 100)]
    public IQueryable<DomainTest.Models.VStaff> GetVStaffs(ODataQueryOptions<DomainTest.Models.VStaff> queryOptions)
    {
        queryOptions.Filter.Validator = new RestrictiveFilterByQueryValidator(new[] { "Name" });
        queryOptions.Validate(new ODataValidationSettings()
        {
            //其它限制

        });
        var result = (IQueryable<DomainTest.Models.VStaff>)queryOptions.ApplyTo(_VStaffService.VStaffsQueryable());
        return result;
    }*/
}
