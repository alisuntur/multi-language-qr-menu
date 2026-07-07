using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Npgsql;

static class ThemeManagementEndpointExtensions
{
    public static IEndpointRouteBuilder MapThemeManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();

        api.MapGet("/theme", async (ClaimsPrincipal user, ThemeService service, CancellationToken cancellationToken) =>
        {
            var data = await service.GetAsync(user.GetRestaurantId(), cancellationToken);
            return Results.Ok(ApiResponse.Ok(data));
        });

        api.MapPut("/theme", async (UpdateThemeSettingsRequest request, ClaimsPrincipal user, ThemeService service, CancellationToken cancellationToken) =>
        {
            var errors = ValidationHelpers.Validate(request);
            if (errors.Count > 0) return Results.BadRequest(ApiResponse.Fail("Validation failed", [.. errors]));
            if (!user.HasAnyRole(RoleConstants.RestaurantOwner, RoleConstants.BranchManager)) return Results.Forbid();
            var data = await service.UpdateAsync(user.GetRestaurantId(), user.GetUserId(), request, cancellationToken);
            return Results.Ok(ApiResponse.Ok(data, "Theme updated."));
        });

        app.MapGet("/public/restaurants/{slug}/theme", async (string slug, ThemeService service, CancellationToken cancellationToken) =>
        {
            var data = await service.GetPublicAsync(slug, cancellationToken);
            return data is null
                ? Results.NotFound(ApiResponse.Fail("Theme not found", "The requested restaurant theme is not available."))
                : Results.Ok(ApiResponse.Ok(data));
        });

        return app;
    }
}

sealed class ThemeService(AppDb appDb)
{
    public async Task<ThemeSettingsResponse> GetAsync(Guid restaurantId, CancellationToken cancellationToken)
    {
        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        return await EnsureAsync(connection, restaurantId, cancellationToken);
    }

    public async Task<ThemeSettingsResponse> UpdateAsync(Guid restaurantId, Guid userId, UpdateThemeSettingsRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO restaurant_theme_settings
                (restaurant_id, logo_url, cover_image_url, primary_color, secondary_color, font_family, menu_layout, show_whatsapp_button, show_google_review_button, google_review_url, updated_at)
            VALUES
                (@restaurantId, @logoUrl, @coverImageUrl, @primaryColor, @secondaryColor, @fontFamily, @menuLayout, @showWhatsappButton, @showGoogleReviewButton, @googleReviewUrl, NOW())
            ON CONFLICT (restaurant_id) DO UPDATE
            SET logo_url = EXCLUDED.logo_url,
                cover_image_url = EXCLUDED.cover_image_url,
                primary_color = EXCLUDED.primary_color,
                secondary_color = EXCLUDED.secondary_color,
                font_family = EXCLUDED.font_family,
                menu_layout = EXCLUDED.menu_layout,
                show_whatsapp_button = EXCLUDED.show_whatsapp_button,
                show_google_review_button = EXCLUDED.show_google_review_button,
                google_review_url = EXCLUDED.google_review_url,
                updated_at = NOW()
            RETURNING restaurant_id, logo_url, cover_image_url, primary_color, secondary_color, font_family, menu_layout, show_whatsapp_button, show_google_review_button, google_review_url;
            """;

        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        BindParameters(command, restaurantId, request);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Theme settings could not be updated.");
        var response = Map(reader);
        await reader.CloseAsync();
        await using var audit = new NpgsqlCommand("INSERT INTO audit_logs (restaurant_id, user_id, action_type, entity_name, entity_id, payload) VALUES (@restaurantId, @userId, 'THEME_UPDATED', 'restaurant_theme_settings', @entityId, CAST(@payload AS jsonb));", connection, transaction);
        audit.Parameters.AddWithValue("restaurantId", restaurantId);
        audit.Parameters.AddWithValue("userId", userId);
        audit.Parameters.AddWithValue("entityId", restaurantId.ToString());
        audit.Parameters.AddWithValue("payload", JsonSerializer.Serialize(request));
        await audit.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    public async Task<ThemeSettingsResponse?> GetPublicAsync(string slug, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT r.id AS restaurant_id,
                   COALESCE(NULLIF(t.logo_url, ''), r.logo_url, '') AS logo_url,
                   COALESCE(t.cover_image_url, '') AS cover_image_url,
                   COALESCE(t.primary_color, '#ff6b35') AS primary_color,
                   COALESCE(t.secondary_color, '#132238') AS secondary_color,
                   COALESCE(t.font_family, 'Manrope') AS font_family,
                   COALESCE(t.menu_layout, 'cards') AS menu_layout,
                   COALESCE(t.show_whatsapp_button, TRUE) AS show_whatsapp_button,
                   COALESCE(t.show_google_review_button, FALSE) AS show_google_review_button,
                   COALESCE(t.google_review_url, '') AS google_review_url
            FROM restaurants r
            LEFT JOIN restaurant_theme_settings t ON t.restaurant_id = r.id
            WHERE r.slug = @slug AND r.is_active = TRUE
            LIMIT 1;
            """;

        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("slug", slug.Trim().ToLowerInvariant());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    private async Task<ThemeSettingsResponse> EnsureAsync(NpgsqlConnection connection, Guid restaurantId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT restaurant_id, logo_url, cover_image_url, primary_color, secondary_color, font_family, menu_layout, show_whatsapp_button, show_google_review_button, google_review_url FROM restaurant_theme_settings WHERE restaurant_id = @restaurantId LIMIT 1;";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("restaurantId", restaurantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken)) return Map(reader);
        await reader.CloseAsync();
        await using (var create = new NpgsqlCommand("INSERT INTO restaurant_theme_settings (restaurant_id) VALUES (@restaurantId) ON CONFLICT (restaurant_id) DO NOTHING;", connection))
        {
            create.Parameters.AddWithValue("restaurantId", restaurantId);
            await create.ExecuteNonQueryAsync(cancellationToken);
        }
        await using var retry = new NpgsqlCommand(sql, connection);
        retry.Parameters.AddWithValue("restaurantId", restaurantId);
        await using var retryReader = await retry.ExecuteReaderAsync(cancellationToken);
        if (!await retryReader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Theme settings could not be initialized.");
        return Map(retryReader);
    }

    private static void BindParameters(NpgsqlCommand command, Guid restaurantId, UpdateThemeSettingsRequest request)
    {
        command.Parameters.AddWithValue("restaurantId", restaurantId);
        command.Parameters.AddWithValue("logoUrl", request.LogoUrl.Trim());
        command.Parameters.AddWithValue("coverImageUrl", request.CoverImageUrl.Trim());
        command.Parameters.AddWithValue("primaryColor", request.PrimaryColor.Trim());
        command.Parameters.AddWithValue("secondaryColor", request.SecondaryColor.Trim());
        command.Parameters.AddWithValue("fontFamily", request.FontFamily.Trim());
        command.Parameters.AddWithValue("menuLayout", request.MenuLayout.Trim().ToLowerInvariant());
        command.Parameters.AddWithValue("showWhatsappButton", request.ShowWhatsappButton);
        command.Parameters.AddWithValue("showGoogleReviewButton", request.ShowGoogleReviewButton);
        command.Parameters.AddWithValue("googleReviewUrl", request.GoogleReviewUrl.Trim());
    }

    private static ThemeSettingsResponse Map(NpgsqlDataReader reader) => new()
    {
        RestaurantId = reader.GetGuid(reader.GetOrdinal("restaurant_id")),
        LogoUrl = DbValueOrEmpty(reader, "logo_url"),
        CoverImageUrl = DbValueOrEmpty(reader, "cover_image_url"),
        PrimaryColor = DbValueOrEmpty(reader, "primary_color"),
        SecondaryColor = DbValueOrEmpty(reader, "secondary_color"),
        FontFamily = DbValueOrEmpty(reader, "font_family"),
        MenuLayout = DbValueOrEmpty(reader, "menu_layout"),
        ShowWhatsappButton = reader.GetBoolean(reader.GetOrdinal("show_whatsapp_button")),
        ShowGoogleReviewButton = reader.GetBoolean(reader.GetOrdinal("show_google_review_button")),
        GoogleReviewUrl = DbValueOrEmpty(reader, "google_review_url")
    };

    private static string DbValueOrEmpty(NpgsqlDataReader reader, string columnName) => reader.IsDBNull(reader.GetOrdinal(columnName)) ? string.Empty : reader.GetString(reader.GetOrdinal(columnName));
}

sealed class UpdateThemeSettingsRequest
{
    [Url]
    [MaxLength(512)]
    public string LogoUrl { get; init; } = string.Empty;

    [Url]
    [MaxLength(512)]
    public string CoverImageUrl { get; init; } = string.Empty;

    [Required]
    [RegularExpression("^#(?:[0-9a-fA-F]{6})$")]
    public string PrimaryColor { get; init; } = "#ff6b35";

    [Required]
    [RegularExpression("^#(?:[0-9a-fA-F]{6})$")]
    public string SecondaryColor { get; init; } = "#132238";

    [Required]
    [MaxLength(120)]
    public string FontFamily { get; init; } = "Manrope";

    [Required]
    [RegularExpression("^(cards|list)$")]
    public string MenuLayout { get; init; } = "cards";

    public bool ShowWhatsappButton { get; init; } = true;
    public bool ShowGoogleReviewButton { get; init; } = false;

    [Url]
    [MaxLength(512)]
    public string GoogleReviewUrl { get; init; } = string.Empty;
}

sealed class ThemeSettingsResponse
{
    public Guid RestaurantId { get; init; }
    public string LogoUrl { get; init; } = string.Empty;
    public string CoverImageUrl { get; init; } = string.Empty;
    public string PrimaryColor { get; init; } = "#ff6b35";
    public string SecondaryColor { get; init; } = "#132238";
    public string FontFamily { get; init; } = "Manrope";
    public string MenuLayout { get; init; } = "cards";
    public bool ShowWhatsappButton { get; init; } = true;
    public bool ShowGoogleReviewButton { get; init; }
    public string GoogleReviewUrl { get; init; } = string.Empty;
}
