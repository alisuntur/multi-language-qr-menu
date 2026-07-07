using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging.Console;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true, reloadOnChange: false)
    .AddJsonFile(Path.Combine(AppContext.BaseDirectory, $"appsettings.{builder.Environment.EnvironmentName}.json"), optional: true, reloadOnChange: false);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "HH:mm:ss ";
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSingleton<AppDb>();
builder.Services.AddSingleton<PasswordHasher>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<RestaurantService>();
builder.Services.AddSingleton<MenuCategoryService>();
builder.Services.AddSingleton<MenuItemService>();
builder.Services.AddSingleton<LanguageService>();
builder.Services.AddSingleton<QrCodeService>();
builder.Services.AddSingleton<TableService>();
builder.Services.AddSingleton<ThemeService>();
builder.Services.AddSingleton<PublicMenuService>();
builder.Services.AddSingleton<DatabaseBootstrapper>();

var app = builder.Build();

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (UnauthorizedAccessException exception)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(ApiResponse.Fail("Unauthorized", exception.Message));
    }
    catch (InvalidOperationException exception)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(ApiResponse.Fail("Request rejected", exception.Message));
    }
    catch (Exception exception)
    {
        app.Logger.LogError(exception, "Unhandled exception");
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(ApiResponse.Fail("Unexpected server error", "Please inspect application logs."));
    }
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

await app.Services.GetRequiredService<DatabaseBootstrapper>().InitializeAsync();

app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(ApiResponse.Ok(new
{
    status = "ok",
    serverTimeUtc = DateTimeOffset.UtcNow
}, "API is healthy.")));

app.MapPost("/api/auth/login", async (LoginRequest request, HttpContext context, AuthService authService, CancellationToken cancellationToken) =>
{
    var validationErrors = ValidateModel(request);
    if (validationErrors.Count > 0)
    {
        return Results.BadRequest(ApiResponse.Fail("Validation failed", validationErrors.ToArray()));
    }

    var response = await authService.LoginAsync(request, context.Connection.RemoteIpAddress?.ToString() ?? "unknown", cancellationToken);
    return Results.Ok(ApiResponse.Ok(response, "Login successful."));
});

app.MapPost("/api/auth/refresh", async (RefreshTokenRequest request, HttpContext context, AuthService authService, CancellationToken cancellationToken) =>
{
    var validationErrors = ValidateModel(request);
    if (validationErrors.Count > 0)
    {
        return Results.BadRequest(ApiResponse.Fail("Validation failed", validationErrors.ToArray()));
    }

    var response = await authService.RefreshAsync(request.RefreshToken, context.Connection.RemoteIpAddress?.ToString() ?? "unknown", cancellationToken);
    return Results.Ok(ApiResponse.Ok(response, "Token refreshed."));
});

app.MapPost("/api/auth/logout", async (RefreshTokenRequest request, ClaimsPrincipal user, AuthService authService, CancellationToken cancellationToken) =>
{
    var validationErrors = ValidateModel(request);
    if (validationErrors.Count > 0)
    {
        return Results.BadRequest(ApiResponse.Fail("Validation failed", validationErrors.ToArray()));
    }

    await authService.LogoutAsync(user.GetUserId(), request.RefreshToken, cancellationToken);
    return Results.Ok(ApiResponse.Ok(new { }, "Logout successful."));
}).RequireAuthorization();

app.MapGet("/api/auth/me", async (ClaimsPrincipal user, AuthService authService, CancellationToken cancellationToken) =>
{
    var currentUser = await authService.GetCurrentUserAsync(user.GetUserId(), cancellationToken);
    return Results.Ok(ApiResponse.Ok(currentUser));
}).RequireAuthorization();

app.MapGet("/api/restaurants/current", async (ClaimsPrincipal user, RestaurantService restaurantService, CancellationToken cancellationToken) =>
{
    var restaurant = await restaurantService.GetCurrentAsync(user.GetRestaurantId(), cancellationToken);
    return Results.Ok(ApiResponse.Ok(restaurant));
}).RequireAuthorization();

app.MapPut("/api/restaurants/current", async (UpdateRestaurantRequest request, ClaimsPrincipal user, RestaurantService restaurantService, CancellationToken cancellationToken) =>
{
    var validationErrors = ValidateModel(request);
    if (validationErrors.Count > 0)
    {
        return Results.BadRequest(ApiResponse.Fail("Validation failed", validationErrors.ToArray()));
    }

    if (!user.IsInRole(RoleConstants.RestaurantOwner) && !user.IsInRole(RoleConstants.BranchManager))
    {
        return Results.Forbid();
    }

    var restaurant = await restaurantService.UpdateCurrentAsync(user.GetRestaurantId(), user.GetUserId(), request, cancellationToken);
    return Results.Ok(ApiResponse.Ok(restaurant, "Restaurant settings updated."));
}).RequireAuthorization();

app.MapMenuManagementEndpoints();
app.MapQrManagementEndpoints();
app.MapPublicMenuEndpoints();
app.MapThemeManagementEndpoints();
app.Run();

static List<string> ValidateModel<T>(T model)
{
    var validationResults = new List<ValidationResult>();
    var context = new ValidationContext(model!);
    Validator.TryValidateObject(model!, context, validationResults, true);
    return validationResults.Select(result => result.ErrorMessage ?? "Validation error.").ToList();
}

static class PathResolver
{
    public static string ResolveDatabaseScriptPath(string contentRootPath)
    {
        var candidates = new[]
        {
            Path.Combine(contentRootPath, "..", "..", "database", "init.sql"),
            Path.Combine(contentRootPath, "database", "init.sql"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "database", "init.sql"),
            Path.Combine(Directory.GetCurrentDirectory(), "database", "init.sql"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "database", "init.sql")
        };

        foreach (var candidate in candidates.Select(Path.GetFullPath))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.GetFullPath(candidates[0]);
    }
}

sealed class AppDb(IConfiguration configuration)
{
    private readonly string _connectionString = configuration["Database:ConnectionString"]
        ?? throw new InvalidOperationException("Database connection string is missing.");

    public async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}

sealed class DatabaseBootstrapper(
    IWebHostEnvironment environment,
    IConfiguration configuration,
    AppDb appDb,
    PasswordHasher passwordHasher,
    ILogger<DatabaseBootstrapper> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var scriptPath = PathResolver.ResolveDatabaseScriptPath(environment.ContentRootPath);
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException("Database bootstrap script was not found.", scriptPath);
        }

        var sql = await File.ReadAllTextAsync(scriptPath, cancellationToken);
        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        await using (var command = new NpgsqlCommand(sql, connection))
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await SeedRolesAsync(connection, cancellationToken);
        await SeedRestaurantOwnerAsync(connection, configuration, passwordHasher, logger, cancellationToken);
    }

    private static async Task SeedRolesAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO roles (code, name)
            VALUES
                (@systemAdmin, 'System Admin'),
                (@restaurantOwner, 'Restaurant Owner'),
                (@branchManager, 'Branch Manager'),
                (@menuEditor, 'Menu Editor')
            ON CONFLICT (code) DO NOTHING;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("systemAdmin", RoleConstants.SystemAdmin);
        command.Parameters.AddWithValue("restaurantOwner", RoleConstants.RestaurantOwner);
        command.Parameters.AddWithValue("branchManager", RoleConstants.BranchManager);
        command.Parameters.AddWithValue("menuEditor", RoleConstants.MenuEditor);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task SeedRestaurantOwnerAsync(
        NpgsqlConnection connection,
        IConfiguration configuration,
        PasswordHasher passwordHasher,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var bootstrap = configuration.GetSection("Bootstrap").Get<BootstrapSettings>() ?? new BootstrapSettings();

        Guid restaurantId;
        const string findRestaurantSql = "SELECT id FROM restaurants WHERE slug = @slug LIMIT 1;";
        await using (var findRestaurant = new NpgsqlCommand(findRestaurantSql, connection))
        {
            findRestaurant.Parameters.AddWithValue("slug", bootstrap.RestaurantSlug.Trim().ToLowerInvariant());
            var existingRestaurantId = await findRestaurant.ExecuteScalarAsync(cancellationToken);
            if (existingRestaurantId is Guid id)
            {
                restaurantId = id;
            }
            else
            {
                restaurantId = Guid.NewGuid();
                const string insertRestaurantSql = """
                    INSERT INTO restaurants
                    (id, name, slug, phone, whatsapp_phone, email, address, logo_url, default_language, currency, is_active, created_at, updated_at)
                    VALUES
                    (@id, @name, @slug, '', '', @email, '', '', 'tr', @currency, TRUE, NOW(), NOW());
                    """;

                await using var insertRestaurant = new NpgsqlCommand(insertRestaurantSql, connection);
                insertRestaurant.Parameters.AddWithValue("id", restaurantId);
                insertRestaurant.Parameters.AddWithValue("name", bootstrap.RestaurantName.Trim());
                insertRestaurant.Parameters.AddWithValue("slug", bootstrap.RestaurantSlug.Trim().ToLowerInvariant());
                insertRestaurant.Parameters.AddWithValue("email", bootstrap.OwnerEmail.Trim().ToLowerInvariant());
                insertRestaurant.Parameters.AddWithValue("currency", bootstrap.Currency.Trim().ToUpperInvariant());
                await insertRestaurant.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        Guid userId;
        const string findUserSql = "SELECT id FROM users WHERE lower(email) = lower(@email) LIMIT 1;";
        await using (var findUser = new NpgsqlCommand(findUserSql, connection))
        {
            findUser.Parameters.AddWithValue("email", bootstrap.OwnerEmail.Trim());
            var existingUserId = await findUser.ExecuteScalarAsync(cancellationToken);
            if (existingUserId is Guid id)
            {
                userId = id;
            }
            else
            {
                userId = Guid.NewGuid();
                const string insertUserSql = """
                    INSERT INTO users
                    (id, restaurant_id, full_name, email, phone, password_hash, is_active, created_at, updated_at)
                    VALUES
                    (@id, @restaurantId, @fullName, @email, '', @passwordHash, TRUE, NOW(), NOW());
                    """;

                await using var insertUser = new NpgsqlCommand(insertUserSql, connection);
                insertUser.Parameters.AddWithValue("id", userId);
                insertUser.Parameters.AddWithValue("restaurantId", restaurantId);
                insertUser.Parameters.AddWithValue("fullName", bootstrap.OwnerFullName.Trim());
                insertUser.Parameters.AddWithValue("email", bootstrap.OwnerEmail.Trim().ToLowerInvariant());
                insertUser.Parameters.AddWithValue("passwordHash", passwordHasher.HashPassword(bootstrap.OwnerPassword));
                await insertUser.ExecuteNonQueryAsync(cancellationToken);
                logger.LogInformation("Seeded default restaurant owner {Email}", bootstrap.OwnerEmail);
            }
        }

        const string assignRoleSql = """
            INSERT INTO user_roles (user_id, role_code)
            VALUES (@userId, @roleCode)
            ON CONFLICT (user_id, role_code) DO NOTHING;
            """;
        await using (var assignRole = new NpgsqlCommand(assignRoleSql, connection))
        {
            assignRole.Parameters.AddWithValue("userId", userId);
            assignRole.Parameters.AddWithValue("roleCode", RoleConstants.RestaurantOwner);
            await assignRole.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}

sealed class AuthService(AppDb appDb, PasswordHasher passwordHasher, JwtTokenService jwtTokenService, IConfiguration configuration)
{
    public async Task<AuthResponse> LoginAsync(LoginRequest request, string ipAddress, CancellationToken cancellationToken)
    {
        var user = await FindUserByEmailAsync(request.Email.Trim(), cancellationToken)
            ?? throw new InvalidOperationException("Invalid email or password.");

        EnsureAccess(user);

        if (!passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new InvalidOperationException("Invalid email or password.");
        }

        var response = await BuildAuthResponseAsync(user, ipAddress, cancellationToken);
        await WriteAuditAsync(user.RestaurantId, user.Id, "LOGIN", "user", user.Id.ToString(), JsonSerializer.Serialize(new { ipAddress }), cancellationToken);
        return response;
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken, string ipAddress, CancellationToken cancellationToken)
    {
        var tokenHash = jwtTokenService.HashRefreshToken(refreshToken);
        var refreshRecord = await FindRefreshTokenAsync(tokenHash, cancellationToken)
            ?? throw new InvalidOperationException("Refresh token is invalid.");

        if (refreshRecord.RevokedAt.HasValue || refreshRecord.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("Refresh token has expired.");
        }

        EnsureAccess(refreshRecord.User);
        await RevokeRefreshTokenAsync(refreshRecord.Id, cancellationToken);
        return await BuildAuthResponseAsync(refreshRecord.User, ipAddress, cancellationToken);
    }

    public async Task LogoutAsync(Guid userId, string refreshToken, CancellationToken cancellationToken)
    {
        var tokenHash = jwtTokenService.HashRefreshToken(refreshToken);
        var refreshRecord = await FindRefreshTokenAsync(tokenHash, cancellationToken)
            ?? throw new InvalidOperationException("Refresh token is invalid.");

        if (refreshRecord.UserId != userId)
        {
            throw new UnauthorizedAccessException("You cannot revoke another user's token.");
        }

        await RevokeRefreshTokenAsync(refreshRecord.Id, cancellationToken);
        await WriteAuditAsync(refreshRecord.User.RestaurantId, userId, "LOGOUT", "refresh_token", refreshRecord.Id.ToString(), "{}", cancellationToken);
    }

    public async Task<CurrentUserResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await FindUserByIdAsync(userId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Current user was not found.");

        EnsureAccess(user);
        return MapCurrentUser(user);
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(AppUser user, string ipAddress, CancellationToken cancellationToken)
    {
        var accessToken = jwtTokenService.CreateAccessToken(user);
        var refreshToken = jwtTokenService.CreateRefreshToken();
        var refreshTokenHash = jwtTokenService.HashRefreshToken(refreshToken);
        var refreshTokenDays = configuration.GetValue<int>("Jwt:RefreshTokenDays");

        await StoreRefreshTokenAsync(user.Id, refreshTokenHash, DateTimeOffset.UtcNow.AddDays(refreshTokenDays), ipAddress, cancellationToken);

        return new AuthResponse
        {
            AccessToken = accessToken.Token,
            RefreshToken = refreshToken,
            ExpiresAt = accessToken.ExpiresAt,
            User = MapCurrentUser(user)
        };
    }

    private async Task<AppUser?> FindUserByEmailAsync(string email, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                u.id,
                u.restaurant_id,
                u.full_name,
                u.email,
                u.phone,
                u.password_hash,
                u.is_active,
                r.id AS restaurant_entity_id,
                r.name AS restaurant_name,
                r.slug,
                r.phone AS restaurant_phone,
                r.whatsapp_phone,
                r.email AS restaurant_email,
                r.address,
                r.logo_url,
                r.default_language,
                r.currency,
                r.is_active AS restaurant_is_active
            FROM users u
            INNER JOIN restaurants r ON r.id = u.restaurant_id
            WHERE lower(u.email) = lower(@email)
            LIMIT 1;
            """;

        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("email", email);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var user = MapUser(reader);
        await reader.CloseAsync();
        user.Roles = await LoadRolesAsync(connection, user.Id, cancellationToken);
        return user;
    }

    private async Task<AppUser?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                u.id,
                u.restaurant_id,
                u.full_name,
                u.email,
                u.phone,
                u.password_hash,
                u.is_active,
                r.id AS restaurant_entity_id,
                r.name AS restaurant_name,
                r.slug,
                r.phone AS restaurant_phone,
                r.whatsapp_phone,
                r.email AS restaurant_email,
                r.address,
                r.logo_url,
                r.default_language,
                r.currency,
                r.is_active AS restaurant_is_active
            FROM users u
            INNER JOIN restaurants r ON r.id = u.restaurant_id
            WHERE u.id = @userId
            LIMIT 1;
            """;

        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var user = MapUser(reader);
        await reader.CloseAsync();
        user.Roles = await LoadRolesAsync(connection, user.Id, cancellationToken);
        return user;
    }

    private async Task<RefreshTokenRecord?> FindRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                rt.id AS refresh_token_id,
                rt.user_id,
                rt.token_hash,
                rt.expires_at,
                rt.revoked_at,
                u.id,
                u.restaurant_id,
                u.full_name,
                u.email,
                u.phone,
                u.password_hash,
                u.is_active,
                r.id AS restaurant_entity_id,
                r.name AS restaurant_name,
                r.slug,
                r.phone AS restaurant_phone,
                r.whatsapp_phone,
                r.email AS restaurant_email,
                r.address,
                r.logo_url,
                r.default_language,
                r.currency,
                r.is_active AS restaurant_is_active
            FROM refresh_tokens rt
            INNER JOIN users u ON u.id = rt.user_id
            INNER JOIN restaurants r ON r.id = u.restaurant_id
            WHERE rt.token_hash = @tokenHash
            LIMIT 1;
            """;

        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("tokenHash", tokenHash);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var user = MapUser(reader);
        var refreshRecord = new RefreshTokenRecord
        {
            Id = reader.GetGuid(reader.GetOrdinal("refresh_token_id")),
            UserId = reader.GetGuid(reader.GetOrdinal("user_id")),
            TokenHash = reader.GetString(reader.GetOrdinal("token_hash")),
            ExpiresAt = new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal("expires_at")), DateTimeKind.Utc)),
            RevokedAt = reader.IsDBNull(reader.GetOrdinal("revoked_at"))
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal("revoked_at")), DateTimeKind.Utc)),
            User = user
        };

        await reader.CloseAsync();
        user.Roles = await LoadRolesAsync(connection, user.Id, cancellationToken);
        return refreshRecord;
    }

    private static async Task<List<string>> LoadRolesAsync(NpgsqlConnection connection, Guid userId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT role_code FROM user_roles WHERE user_id = @userId ORDER BY role_code;";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId);
        var roles = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            roles.Add(reader.GetString(0));
        }

        return roles;
    }

    private async Task StoreRefreshTokenAsync(Guid userId, string tokenHash, DateTimeOffset expiresAt, string ipAddress, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO refresh_tokens (id, user_id, token_hash, expires_at, created_at, created_by_ip)
            VALUES (@id, @userId, @tokenHash, @expiresAt, NOW(), @createdByIp);
            """;

        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("tokenHash", tokenHash);
        command.Parameters.AddWithValue("expiresAt", expiresAt.UtcDateTime);
        command.Parameters.AddWithValue("createdByIp", ipAddress);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task RevokeRefreshTokenAsync(Guid tokenId, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE refresh_tokens SET revoked_at = NOW() WHERE id = @tokenId;";
        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("tokenId", tokenId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task WriteAuditAsync(Guid? restaurantId, Guid? userId, string actionType, string entityName, string entityId, string payload, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO audit_logs (restaurant_id, user_id, action_type, entity_name, entity_id, payload)
            VALUES (@restaurantId, @userId, @actionType, @entityName, @entityId, CAST(@payload AS jsonb));
            """;

        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("restaurantId", restaurantId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("userId", userId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("actionType", actionType);
        command.Parameters.AddWithValue("entityName", entityName);
        command.Parameters.AddWithValue("entityId", entityId);
        command.Parameters.AddWithValue("payload", payload);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static CurrentUserResponse MapCurrentUser(AppUser user) => new()
    {
        Id = user.Id,
        FullName = user.FullName,
        Email = user.Email,
        Phone = user.Phone,
        IsActive = user.IsActive,
        RestaurantId = user.RestaurantId,
        RestaurantName = user.Restaurant.Name,
        RestaurantSlug = user.Restaurant.Slug,
        RestaurantIsActive = user.Restaurant.IsActive,
        Roles = user.Roles
    };

    private static AppUser MapUser(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("id")),
        RestaurantId = reader.GetGuid(reader.GetOrdinal("restaurant_id")),
        FullName = reader.GetString(reader.GetOrdinal("full_name")),
        Email = reader.GetString(reader.GetOrdinal("email")),
        Phone = reader.IsDBNull(reader.GetOrdinal("phone")) ? string.Empty : reader.GetString(reader.GetOrdinal("phone")),
        PasswordHash = reader.GetString(reader.GetOrdinal("password_hash")),
        IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
        Restaurant = new RestaurantEntity
        {
            Id = reader.GetGuid(reader.GetOrdinal("restaurant_entity_id")),
            Name = reader.GetString(reader.GetOrdinal("restaurant_name")),
            Slug = reader.GetString(reader.GetOrdinal("slug")),
            Phone = reader.IsDBNull(reader.GetOrdinal("restaurant_phone")) ? string.Empty : reader.GetString(reader.GetOrdinal("restaurant_phone")),
            WhatsappPhone = reader.IsDBNull(reader.GetOrdinal("whatsapp_phone")) ? string.Empty : reader.GetString(reader.GetOrdinal("whatsapp_phone")),
            Email = reader.IsDBNull(reader.GetOrdinal("restaurant_email")) ? string.Empty : reader.GetString(reader.GetOrdinal("restaurant_email")),
            Address = reader.IsDBNull(reader.GetOrdinal("address")) ? string.Empty : reader.GetString(reader.GetOrdinal("address")),
            LogoUrl = reader.IsDBNull(reader.GetOrdinal("logo_url")) ? string.Empty : reader.GetString(reader.GetOrdinal("logo_url")),
            DefaultLanguage = reader.GetString(reader.GetOrdinal("default_language")),
            Currency = reader.GetString(reader.GetOrdinal("currency")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("restaurant_is_active"))
        }
    };

    private static void EnsureAccess(AppUser user)
    {
        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("User account is inactive.");
        }

        if (!user.Restaurant.IsActive)
        {
            throw new UnauthorizedAccessException("Restaurant account is inactive.");
        }
    }
}

sealed class RestaurantService(AppDb appDb)
{
    public async Task<RestaurantResponse> GetCurrentAsync(Guid restaurantId, CancellationToken cancellationToken)
    {
        var entity = await FindRestaurantEntityAsync(restaurantId, cancellationToken)
            ?? throw new InvalidOperationException("Restaurant could not be loaded.");

        return MapRestaurant(entity);
    }

    public async Task<RestaurantResponse> UpdateCurrentAsync(Guid restaurantId, Guid userId, UpdateRestaurantRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE restaurants
            SET
                name = @name,
                phone = @phone,
                whatsapp_phone = @whatsappPhone,
                email = @email,
                address = @address,
                logo_url = @logoUrl,
                default_language = @defaultLanguage,
                currency = @currency,
                updated_at = NOW()
            WHERE id = @restaurantId
            RETURNING id, name, slug, phone, whatsapp_phone, email, address, logo_url, default_language, currency, is_active;
            """;

        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("restaurantId", restaurantId);
        command.Parameters.AddWithValue("name", request.Name.Trim());
        command.Parameters.AddWithValue("phone", request.Phone.Trim());
        command.Parameters.AddWithValue("whatsappPhone", request.WhatsappPhone.Trim());
        command.Parameters.AddWithValue("email", request.Email.Trim());
        command.Parameters.AddWithValue("address", request.Address.Trim());
        command.Parameters.AddWithValue("logoUrl", request.LogoUrl.Trim());
        command.Parameters.AddWithValue("defaultLanguage", request.DefaultLanguage.Trim().ToLowerInvariant());
        command.Parameters.AddWithValue("currency", request.Currency.Trim().ToUpperInvariant());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Restaurant could not be updated.");
        }

        var updatedRestaurant = new RestaurantEntity
        {
            Id = reader.GetGuid(reader.GetOrdinal("id")),
            Name = reader.GetString(reader.GetOrdinal("name")),
            Slug = reader.GetString(reader.GetOrdinal("slug")),
            Phone = reader.IsDBNull(reader.GetOrdinal("phone")) ? string.Empty : reader.GetString(reader.GetOrdinal("phone")),
            WhatsappPhone = reader.IsDBNull(reader.GetOrdinal("whatsapp_phone")) ? string.Empty : reader.GetString(reader.GetOrdinal("whatsapp_phone")),
            Email = reader.IsDBNull(reader.GetOrdinal("email")) ? string.Empty : reader.GetString(reader.GetOrdinal("email")),
            Address = reader.IsDBNull(reader.GetOrdinal("address")) ? string.Empty : reader.GetString(reader.GetOrdinal("address")),
            LogoUrl = reader.IsDBNull(reader.GetOrdinal("logo_url")) ? string.Empty : reader.GetString(reader.GetOrdinal("logo_url")),
            DefaultLanguage = reader.GetString(reader.GetOrdinal("default_language")),
            Currency = reader.GetString(reader.GetOrdinal("currency")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("is_active"))
        };

        await reader.CloseAsync();
        await WriteAuditAsync(connection, restaurantId, userId, request, cancellationToken);
        return MapRestaurant(updatedRestaurant);
    }

    private async Task<RestaurantEntity?> FindRestaurantEntityAsync(Guid restaurantId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, name, slug, phone, whatsapp_phone, email, address, logo_url, default_language, currency, is_active
            FROM restaurants
            WHERE id = @restaurantId
            LIMIT 1;
            """;

        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("restaurantId", restaurantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new RestaurantEntity
        {
            Id = reader.GetGuid(reader.GetOrdinal("id")),
            Name = reader.GetString(reader.GetOrdinal("name")),
            Slug = reader.GetString(reader.GetOrdinal("slug")),
            Phone = reader.IsDBNull(reader.GetOrdinal("phone")) ? string.Empty : reader.GetString(reader.GetOrdinal("phone")),
            WhatsappPhone = reader.IsDBNull(reader.GetOrdinal("whatsapp_phone")) ? string.Empty : reader.GetString(reader.GetOrdinal("whatsapp_phone")),
            Email = reader.IsDBNull(reader.GetOrdinal("email")) ? string.Empty : reader.GetString(reader.GetOrdinal("email")),
            Address = reader.IsDBNull(reader.GetOrdinal("address")) ? string.Empty : reader.GetString(reader.GetOrdinal("address")),
            LogoUrl = reader.IsDBNull(reader.GetOrdinal("logo_url")) ? string.Empty : reader.GetString(reader.GetOrdinal("logo_url")),
            DefaultLanguage = reader.GetString(reader.GetOrdinal("default_language")),
            Currency = reader.GetString(reader.GetOrdinal("currency")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("is_active"))
        };
    }

    private static async Task WriteAuditAsync(NpgsqlConnection connection, Guid restaurantId, Guid userId, UpdateRestaurantRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO audit_logs (restaurant_id, user_id, action_type, entity_name, entity_id, payload)
            VALUES (@restaurantId, @userId, 'RESTAURANT_UPDATED', 'restaurant', @entityId, CAST(@payload AS jsonb));
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("restaurantId", restaurantId);
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("entityId", restaurantId.ToString());
        command.Parameters.AddWithValue("payload", JsonSerializer.Serialize(request));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static RestaurantResponse MapRestaurant(RestaurantEntity restaurant) => new()
    {
        Id = restaurant.Id,
        Name = restaurant.Name,
        Slug = restaurant.Slug,
        Phone = restaurant.Phone,
        WhatsappPhone = restaurant.WhatsappPhone,
        Email = restaurant.Email,
        Address = restaurant.Address,
        LogoUrl = restaurant.LogoUrl,
        DefaultLanguage = restaurant.DefaultLanguage,
        Currency = restaurant.Currency,
        IsActive = restaurant.IsActive
    };
}

sealed class PasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;

    public string HashPassword(string password)
    {
        Span<byte> salt = stackalloc byte[SaltSize];
        RandomNumberGenerator.Fill(salt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        var parts = passwordHash.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations))
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[1]);
        var expectedHash = Convert.FromBase64String(parts[2]);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}

sealed class JwtTokenService(IConfiguration configuration)
{
    private readonly JwtSettings _settings = configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();

    public (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(AppUser user)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_settings.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new("restaurant_id", user.RestaurantId.ToString())
        };

        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SigningKey)),
                SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public string CreateRefreshToken()
    {
        Span<byte> bytes = stackalloc byte[48];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    public string HashRefreshToken(string refreshToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToHexString(hash);
    }
}

sealed class JwtSettings
{
    public string Issuer { get; init; } = "MultiLanguageQrMenu";
    public string Audience { get; init; } = "MultiLanguageQrMenu.Admin";
    public string SigningKey { get; init; } = "local-development-signing-key-change-before-production";
    public int AccessTokenMinutes { get; init; } = 30;
    public int RefreshTokenDays { get; init; } = 14;
}

sealed class BootstrapSettings
{
    public string RestaurantName { get; init; } = "Demo QR Bistro";
    public string RestaurantSlug { get; init; } = "demo-qr-bistro";
    public string Currency { get; init; } = "TRY";
    public string OwnerFullName { get; init; } = "Demo Owner";
    public string OwnerEmail { get; init; } = "owner@demoqrmenu.local";
    public string OwnerPassword { get; init; } = "ChangeMe123!";
}

sealed class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; init; } = string.Empty;
}

sealed class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; init; } = string.Empty;
}

sealed class UpdateRestaurantRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string Phone { get; init; } = string.Empty;

    [MaxLength(32)]
    public string WhatsappPhone { get; init; } = string.Empty;

    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [MaxLength(512)]
    public string Address { get; init; } = string.Empty;

    [Url]
    [MaxLength(512)]
    public string LogoUrl { get; init; } = string.Empty;

    [Required]
    [RegularExpression("^[a-z]{2}$")]
    public string DefaultLanguage { get; init; } = "tr";

    [Required]
    [MaxLength(8)]
    public string Currency { get; init; } = "TRY";
}

sealed class AuthResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }
    public CurrentUserResponse User { get; init; } = new();
}

sealed class CurrentUserResponse
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public Guid RestaurantId { get; init; }
    public string RestaurantName { get; init; } = string.Empty;
    public string RestaurantSlug { get; init; } = string.Empty;
    public bool RestaurantIsActive { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
}

sealed class RestaurantResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string WhatsappPhone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string LogoUrl { get; init; } = string.Empty;
    public string DefaultLanguage { get; init; } = "tr";
    public string Currency { get; init; } = "TRY";
    public bool IsActive { get; init; }
}

sealed class AppUser
{
    public Guid Id { get; init; }
    public Guid RestaurantId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string PasswordHash { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public RestaurantEntity Restaurant { get; init; } = new();
    public List<string> Roles { get; set; } = [];
}

sealed class RestaurantEntity
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string WhatsappPhone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string LogoUrl { get; init; } = string.Empty;
    public string DefaultLanguage { get; init; } = "tr";
    public string Currency { get; init; } = "TRY";
    public bool IsActive { get; init; }
}

sealed class RefreshTokenRecord
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string TokenHash { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? RevokedAt { get; init; }
    public AppUser User { get; init; } = new();
}

static class RoleConstants
{
    public const string SystemAdmin = "SYSTEM_ADMIN";
    public const string RestaurantOwner = "RESTAURANT_OWNER";
    public const string BranchManager = "BRANCH_MANAGER";
    public const string MenuEditor = "MENU_EDITOR";
}

static class PrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var claim = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User claim is missing.");
        return Guid.Parse(claim);
    }

    public static Guid GetRestaurantId(this ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst("restaurant_id")?.Value
            ?? throw new UnauthorizedAccessException("Restaurant claim is missing.");
        return Guid.Parse(claim);
    }
}

sealed class ApiResponse<T>
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
}

static class ApiResponse
{
    public static ApiResponse<T> Ok<T>(T data, string message = "") => new()
    {
        Success = true,
        Message = message,
        Data = data,
        Errors = []
    };

    public static ApiResponse<object> Fail(string message, params string[] errors) => new()
    {
        Success = false,
        Message = message,
        Data = null,
        Errors = errors
    };
}











