using Autofac;
using DomainTest;
using DomainTest.Managers;
using DomainTest.Models;
using DomainTest.Services;
using DomainTest.Services.Contracts;
using GraphQL.Server.Ui.Voyager;
using HotChocolate.AspNetCore;
using HotChocolate.AspNetCore.Voyager;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Session;
using Microsoft.AspNetCore.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;
using VoyagerOptions = GraphQL.Server.Ui.Voyager.VoyagerOptions;

var builder = WebApplication.CreateBuilder(args);

#region 配置 JWT
/*var configuration = builder.Configuration;

//注册服务
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters()
        {
            ValidateIssuer = true, //是否验证Issuer
            ValidIssuer = configuration["Jwt:Issuer"], //发行人Issuer
            ValidateAudience = true, //是否验证Audience
            ValidAudience = configuration["Jwt:Audience"], //订阅人Audience
            ValidateIssuerSigningKey = true, //是否验证SecurityKey
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:SecretKey"])), //SecurityKey
            ValidateLifetime = true, //是否验证失效时间
            ClockSkew = TimeSpan.FromSeconds(30), //过期时间容错值，解决服务器端时间不同步问题（秒）
            RequireExpirationTime = true,
        };
    });*/
#endregion

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "TestApi", Version = "v1" });
});

//初始化领域
DomainHost.Initial<ProjectDomainUser, SessionHelper>(ConfigureServices, builder.Services);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();
app.MapControllers();

app.UseRouting();
//app.MapGraphQL();
app.UseEndpoints(endpoints =>
{
    endpoints.MapGraphQL("/graphql").WithOptions(new GraphQLServerOptions
    {
        Tool = { Enable = app.Environment.IsDevelopment() },
    });
});
// 添加Voyager中间件并配置URL
app.UseGraphQLVoyager("/graphql-voyager", new VoyagerOptions {
    GraphQLEndPoint = "/graphql"
});

app.UseAuthentication();
app.UseAuthorization();
app.Run();

SessionHelperBase<ProjectDomainUser> ConfigureServices(ContainerBuilder containerBuilder, IServiceCollection services,
    ConfigurationBuilder configurationBuilder, IServiceCollection upLevelServices)
{
    #region 1. 基础准备工作

    // 1.1 处理配置（来自环境变量、json 配置文件等的数据）
    configurationBuilder.Add(new MemoryConfigurationSource());

    // 1.2 第三方服务：注册其它公用 Services
    upLevelServices.AddMemoryCache();

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
    #region 配置 OData

    upLevelServices.AddControllers()
        .AddOData(options => options
            .AddRouteComponents("odata", GetEdmModel())
            .Select()
            .Filter()
            .Expand()
            .SetMaxTop(100)
            .Count()
            .OrderBy());

    static IEdmModel GetEdmModel()
    {
        //TODO: 尝试领域层封装对 OData 的调用，而不是将 ViewModel 直接开放给表现层
        var builder = new ODataConventionModelBuilder();
        var type = builder.EntityType<VStaff>();
        type.HasKey(s => s.Uid);
        builder.EntitySet<VStaff>("VStaffs");
        return builder.GetEdmModel();
    }

    #endregion

    #region 配置 GraphQL
    // upLevelServices.AddDbContext<DomainTestContext>(options => options.UseSqlServer(connectionString)); //已改用 DomainService，所以不需要
    upLevelServices.AddGraphQLServer().AddQueryType<VStaffQuery>().AddProjections().AddFiltering().AddSorting();
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
    //用于 Controller、GraphQL 的 XXXXXXQuery 等的构造器
    upLevelServices.AddDomainSessionHelper<SessionHelper, ProjectDomainUser>(sessionHelper);
    return sessionHelper;
}

