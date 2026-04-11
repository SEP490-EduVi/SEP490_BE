using EduVi.Repositories;
using EduVi.Repositories.DBContext;
using EduVi.Repositories.Interfaces;
using EduVi.Services.Admin;
using EduVi.Services.Authentication;
using EduVi.Services.Curriculum;
using EduVi.Services.CurriculumIngestion;
using EduVi.Services.TextbookIngestion;
using EduVi.Services.Email;
using EduVi.Services.Expert;
using EduVi.Services.Games;
using EduVi.Services.Classroom;
using EduVi.Services.Material;
using EduVi.Services.Otp;
using EduVi.Services.Payment;
using EduVi.Services.Pipeline;
using EduVi.Services.Project;
using EduVi.Services.RateLimit;
using EduVi.Services.Withdrawal;
using EduVi.Services.Staff;
using EduVi.Services.Teacher;
using EduVi.WebAPI.BackgroundServices;
using EduVi.WebAPI.Hubs;
using EduVi.WebAPI.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
using System.Reflection;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Database Context
builder.Services.AddDbContext<EduViContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Redis Configuration
var redisConnection = builder.Configuration.GetConnectionString("RedisConnection")
    ?? throw new InvalidOperationException("RedisConnection is not configured");

var redisOptions = ConfigurationOptions.Parse(redisConnection);
redisOptions.AbortOnConnectFail = false;

var redisMultiplexer = await ConnectionMultiplexer.ConnectAsync(redisOptions);
builder.Services.AddSingleton<IConnectionMultiplexer>(redisMultiplexer);

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero
    };

    // SignalR sends the token as a query string parameter because WebSocket/SSE
    // connections cannot set Authorization headers.
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    // Policy cho Expert đã được Staff duyệt hồ sơ
    // Dùng trong các endpoint chỉ Expert verified mới được thực hiện (upload material, xem doanh thu,...)
    // Expert chưa verified vẫn đăng nhập được nhưng sẽ nhận 403 khi gọi các endpoint này
    options.AddPolicy("VerifiedExpert", policy =>
        policy.RequireRole("Expert")
              .RequireClaim("expert_is_verified", "true"));

    // Policy cho Expert bất kỳ (kể cả chưa verify) - dùng cho endpoint nộp hồ sơ
    options.AddPolicy("AnyExpert", policy =>
        policy.RequireRole("Expert"));
});

// Register UnitOfWork
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// PayOS Configuration (v1 via IPayOSService wrapper)
builder.Services.AddSingleton<IPayOSService, PayOSService>();

// Register Services
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IRateLimitService, RateLimitService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IPipelineService, PipelineService>();
builder.Services.AddScoped<IInputDocumentService, InputDocumentService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IGameService, GameService>();
builder.Services.AddScoped<IClassroomService, ClassroomService>();
builder.Services.AddScoped<ICurriculumService, CurriculumService>();
builder.Services.AddScoped<IExpertService, ExpertService>();
builder.Services.AddScoped<ITeacherService, TeacherService>();
builder.Services.AddScoped<IStaffProfileService, StaffProfileService>();
builder.Services.AddScoped<IMaterialService, MaterialService>();
builder.Services.AddScoped<ICurriculumIngestionService, CurriculumIngestionService>();
builder.Services.AddScoped<ITextbookIngestionService, TextbookIngestionService>();
builder.Services.AddScoped<IWithdrawalService, WithdrawalService>();

// RabbitMQ Publisher
builder.Services.AddSingleton<IRabbitMqPublisherService, RabbitMqPublisherService>();

// Pipeline Result Consumer (BackgroundService)
builder.Services.AddHostedService<PipelineResultConsumerService>();

// Curriculum Ingestion Result Consumer (BackgroundService)
builder.Services.AddHostedService<CurriculumResultConsumerService>();
builder.Services.AddHostedService<TextbookResultConsumerService>();
builder.Services.AddHostedService<GameResultConsumerService>();

// SignalR with Redis backplane
builder.Services.AddSignalR()
    .AddStackExchangeRedis(redisConnection!, options =>
    {
        options.Configuration.ChannelPrefix = RedisChannel.Literal("EduVi");
    });

// CORS Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddControllers();

// Swagger/OpenAPI with JWT Support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "EduVi API",
        Version = "v1",
        Description = "EduVi Education Platform API"
    });

    // đọc XML comment để hiện summary trên swagger
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));


    // JWT Authentication in Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer' [space] and then your token"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.Logger.LogInformation("Redis connected successfully");
app.Logger.LogInformation("PayOS configured");
app.Logger.LogInformation("SignalR + Pipeline configured");

// When behind a reverse proxy (Nginx), forward headers so HTTPS redirect
// and auth schemes work correctly with the original client request.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                     | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});

// Swagger and static files — enabled in all environments for testing/debugging.
// Nginx is the only internet-facing service, so these are not directly public.
app.UseSwagger();
app.UseSwaggerUI();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseSessionValidation(); // Middleware xác thực session với Redis
app.UseAuthorization();

app.MapControllers();
app.MapHub<PipelineHub>("/hubs/pipeline");

app.Run();
