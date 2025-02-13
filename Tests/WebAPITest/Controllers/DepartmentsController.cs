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
public class DepartmentsController : ODataController
{
    private readonly ProjectUser _User;
    private readonly DepartmentService _DepartmentService;

    public DepartmentsController(SessionHelper sessionHelper)
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
        _DepartmentService = _User.Use<DepartmentService>();
    }

    [HttpGet("All")]
    [EnableQuery]
    public IEnumerable<Department> GetAll()
    {
        return _DepartmentService.ListAllDepartments();
    }
}