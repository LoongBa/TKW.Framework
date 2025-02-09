using DomainTest;
using DomainTest.Managers;
using DomainTest.Services.Contracts;
using Microsoft.AspNetCore.Mvc;
using TKW.Framework.Domain;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace WebAPITest.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UsersController : ControllerBase
{
    private readonly ProjectDomainUser _CurrentProjectDomainUser;
    private readonly IUserServiceContract _UserService;

    public UsersController()
    {
        var userHelper = DomainHost.Root.UserHelper<ProjectDomainUser, SessionHelper>();

        //游客
        var session = userHelper.NewGuestSession();

        //登录
        //var session = userHelper.UserLogin("", "", UserAuthenticationType.WechatApp);

        //会话
        var sessionKey = "xxxxxxxxxxx"; //来自 Cookie/localStorage/Header/QueryString 等等
        //session = userHelper.RetrieveAndActiveUserSession(sessionKey);

        //获得用户实例
        _CurrentProjectDomainUser = session.User;
        _UserService = _CurrentProjectDomainUser.Use<IUserServiceContract>();
    }

    // GET: api/<UsersController>
    [HttpGet("Users1")]
    public IEnumerable<User> GetUsers1()
    {
        return _UserService.ListAllUsers1();
    }
    [HttpGet("Users2")]
    public IEnumerable<User> GetUsers2()
    {
        return _UserService.ListAllUsers1();
    }
    [HttpGet("Users3")]
    public IEnumerable<User> GetUsers3()
    {
        return _UserService.ListAllUsers1();
    }

    // GET api/<UsersController>/5
    [HttpGet("{uid:guid}")]
    public User Get(Guid uid)
    {
        return _UserService.GetUserByUid(uid);
    }

    // DELETE api/<UsersController>/5
    [HttpDelete("{uid:guid}")]
    public void Delete(Guid uid)
    {
        _UserService.DelUserByUid(uid);
    }

    [HttpDelete("{username}")]
    public void Delete(string username)
    {
        _UserService.SearchUsers(username);
    }
}