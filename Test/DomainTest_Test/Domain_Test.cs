using System;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Autofac;
using Castle.Core.Logging;
using DomainTest;
using DomainTest.Managers;
using DomainTest.Models;
using DomainTest.Services;
using DomainTest.Services.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Session;

namespace DomainTest_Test
{
    [TestClass]
    public class DomainTest
    {
        [TestInitialize]
        public void Init()
        {
            DomainHost.Initial<ProjectDomainUser, SessionHelper>(ConfigureServices, null);
        }

        private SessionHelperBase<ProjectDomainUser> ConfigureServices(ContainerBuilder containerBuilder, IServiceCollection services,
            ConfigurationBuilder configurationBuilder, IServiceCollection upLevelServices)
        {
            #region 1. 基础准备工作

            // 1.1 处理配置（来自环境变量、json 配置文件等的数据）
            configurationBuilder.Add(new MemoryConfigurationSource());

            // 1.2 第三方服务：注册其它公用 Services
            upLevelServices?.AddMemoryCache();

            #region 1.3 日志相关操作：注册 LoggerFactory
            // var loggerFactory = new LoggerFactory(new ILoggerProvider[] { new ConsoleLoggerProvider(new ConsoleLoggerSettings()) });

            var configureNamedOptions = new ConfigureNamedOptions<ConsoleLoggerOptions>("", null);
            var optionsFactory = new OptionsFactory<ConsoleLoggerOptions>(new[] { configureNamedOptions },
                Enumerable.Empty<IPostConfigureOptions<ConsoleLoggerOptions>>());
            var optionsMonitor = new OptionsMonitor<ConsoleLoggerOptions>(optionsFactory,
                Enumerable.Empty<IOptionsChangeTokenSource<ConsoleLoggerOptions>>(), new OptionsCache<ConsoleLoggerOptions>());
            var loggerFactory = new LoggerFactory(
                new[] { new ConsoleLoggerProvider(optionsMonitor) },
                new LoggerFilterOptions { MinLevel = LogLevel.Information }
            );
            containerBuilder.UseLogger(
                loggerFactory
            );
            upLevelServices?.AddSingleton(new ConsoleLogger());

            #endregion

            #region 1.4 数据库相关配置
            var connectionString = @"Data Source=.;Initial Catalog=DomainFrameworkTest;Integrated Security=True;Persist Security Info=True";
            //containerBuilder.AddDomainDataAccessHelper<DomainTestDataAccessHelper>(new DomainTestDataAccessHelper(connectionString));
            containerBuilder.AddDomainTestDataAccessHelper(connectionString); //TODO: 优化连接池

            // 数据库批量操作：Zack.EFCore.Batch
            /*containerBuilder.UseBatchEF_MSSQL();// MSSQL Server 
            containerBuilder.UseBatchEF_Npgsql();//Postgresql 
            containerBuilder.UseBatchEF_MySQLPomelo();//MySQL 
            containerBuilder.UseBatchEF_Sqlite();//Sqlite 
            containerBuilder.UseBatchEF_Oracle();//Oracle 
            containerBuilder.UseBatchEF_DM();//DM(达梦) 
            containerBuilder.UseBatchEF_InMemory();//In Memory(内存数据库) */
            #endregion

            #endregion

            #region 2. 领域相关准备工作
            // 注册会话Manager
            containerBuilder.UseSessionManager(new SessionManager<ProjectDomainUser>());
            //containerBuilder.UseSessionManager<ProjectDomainUser>();

            // 2.1 注册若干 DomainManager
            containerBuilder.AddDomainManager<DomainTestDataAccessHelper, UserManager>();
            containerBuilder.AddDomainManager<DomainTestDataAccessHelper, DepartmentManager>();

            // 2.2 注册若干 DomainService（注：控制器受到 AOP 框架约束）
            //注册不受拦截的控制器
            containerBuilder.AddDomainService<DepartmentService>();
            containerBuilder.AddDomainService<VStaffService>();

            //注册受拦截的控制器（需指定接口方能生效）
            containerBuilder.AddDomainServiceIntercepted<IUserServiceContract, UserService>();

            //注册若干全局 Filters
            //containerBuilder.AddAction<AuthorityActionFilterAttribute>();
            #endregion

            //3. 返回 SessionHelper（可改为返回多个 SessionHelper，支持多用户类型）
            var sessionHelper = new SessionHelper(() => DomainHost.Root);
            upLevelServices?.AddSingleton(sessionHelper); //用于 GraphQL 的 XXXXXXQuery 的构造器
            return sessionHelper;
        }

        [TestMethod]
        public void GetUsers()
        {
            var user = UserLogin();

            //执行领域业务：通过对应领域控制器
            var userController = user.Use<IUserServiceContract>();
            userController.ListAllUsers1();

            var userController2 = user.Use<UserService>();
            userController2.ListAllUsers1();
        }

        [TestMethod]
        public void GetDepartments()
        {
            //执行领域业务
            var deptController = UserLogin().Use<DepartmentService>();
            var rootDept = deptController.CreateRootDepartment("testDept");
            var subDept = deptController.CreateSubDepartment(rootDept.Uid, "subDept");

            var departments = deptController.ListRootDepartments();
            Debug.WriteLine($"RootDepts:{departments.Count}");
            departments = deptController.ListAllDepartments();
            Debug.WriteLine($"AllDepts:{departments.Count}");

            foreach (var department in departments)
            {
                Debug.WriteLine($"{department.Id}\t{department.Name}");
            }
        }

        [TestMethod]
        public void ClearDepartments()
        {
            var deptController = UserLogin().Use<DepartmentService>();
            deptController.ClearDepartments();
        }


        private static ProjectDomainUser UserLogin()
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
            var user = session.User;
            return user;
        }
    }
}
