using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Npgsql;

static class PublicMenuEndpointExtensions
{
    public static IEndpointRouteBuilder MapPublicMenuEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();

        api.MapGet("/languages", async (LanguageService service, CancellationToken cancellationToken) =>
        {
            var data = await service.GetLanguagesAsync(cancellationToken);
            return Results.Ok(ApiResponse.Ok(data));
        });

        api.MapGet("/restaurants/languages", async (ClaimsPrincipal user, LanguageService service, CancellationToken cancellationToken) =>
        {
            var data = await service.GetRestaurantLanguagesAsync(user.GetRestaurantId(), cancellationToken);
            return Results.Ok(ApiResponse.Ok(data));
        });

        api.MapPut("/restaurants/languages", async (UpdateRestaurantLanguagesRequest request, ClaimsPrincipal user, LanguageService service, CancellationToken cancellationToken) =>
        {
            var errors = ValidationHelpers.Validate(request);
            if (errors.Count > 0)
            {
                return Results.BadRequest(ApiResponse.Fail("Validation failed", [.. errors]));
            }

            if (!user.HasAnyRole(RoleConstants.RestaurantOwner, RoleConstants.BranchManager))
            {
                return Results.Forbid();
            }

            var data = await service.UpdateRestaurantLanguagesAsync(user.GetRestaurantId(), user.GetUserId(), request, cancellationToken);
            return Results.Ok(ApiResponse.Ok(data, "Restaurant languages updated."));
        });

        app.MapGet("/public/restaurants/{slug}", async (string slug, string? lang, PublicMenuService service, CancellationToken cancellationToken) =>
        {
            var data = await service.GetRestaurantAsync(slug, lang, cancellationToken);
            return data is null
                ? Results.NotFound(ApiResponse.Fail("Restaurant not found", "The requested restaurant is not available."))
                : Results.Ok(ApiResponse.Ok(data));
        });

        app.MapGet("/public/restaurants/{slug}/menu", async (string slug, string? lang, string? search, PublicMenuService service, CancellationToken cancellationToken) =>
        {
            var data = await service.GetMenuAsync(slug, lang, search, cancellationToken);
            return data is null
                ? Results.NotFound(ApiResponse.Fail("Menu not found", "The requested menu is not available."))
                : Results.Ok(ApiResponse.Ok(data));
        });

        return app;
    }
}

sealed class LanguageService(AppDb appDb)
{
    public async Task<IReadOnlyList<LanguageOptionResponse>> GetLanguagesAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT code, name, is_active FROM languages WHERE is_active = TRUE ORDER BY CASE code WHEN 'tr' THEN 0 WHEN 'en' THEN 1 WHEN 'de' THEN 2 WHEN 'ru' THEN 3 ELSE 9 END, name;";

        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var result = new List<LanguageOptionResponse>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new LanguageOptionResponse
            {
                Code = reader.GetString(reader.GetOrdinal("code")),
                Name = reader.GetString(reader.GetOrdinal("name")),
                IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                IsEnabled = true,
                IsDefault = false
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<LanguageOptionResponse>> GetRestaurantLanguagesAsync(Guid restaurantId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                l.code,
                l.name,
                l.is_active,
                COALESCE(rl.is_enabled, TRUE) AS is_enabled,
                l.code = r.default_language AS is_default
            FROM restaurants r
            INNER JOIN languages l ON l.is_active = TRUE
            LEFT JOIN restaurant_languages rl ON rl.restaurant_id = r.id AND rl.language_code = l.code
            WHERE r.id = @restaurantId
            ORDER BY CASE l.code WHEN 'tr' THEN 0 WHEN 'en' THEN 1 WHEN 'de' THEN 2 WHEN 'ru' THEN 3 ELSE 9 END, l.name;
            """;

        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("restaurantId", restaurantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var result = new List<LanguageOptionResponse>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new LanguageOptionResponse
            {
                Code = reader.GetString(reader.GetOrdinal("code")),
                Name = reader.GetString(reader.GetOrdinal("name")),
                IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                IsEnabled = reader.GetBoolean(reader.GetOrdinal("is_enabled")),
                IsDefault = reader.GetBoolean(reader.GetOrdinal("is_default"))
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<LanguageOptionResponse>> UpdateRestaurantLanguagesAsync(Guid restaurantId, Guid userId, UpdateRestaurantLanguagesRequest request, CancellationToken cancellationToken)
    {
        var normalizedCodes = request.EnabledLanguageCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        if (normalizedCodes.Count == 0)
        {
            throw new InvalidOperationException("At least one language must stay enabled.");
        }

        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var activeCodes = new List<string>();
        await using (var languageCommand = new NpgsqlCommand("SELECT code FROM languages WHERE is_active = TRUE;", connection, transaction))
        await using (var reader = await languageCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                activeCodes.Add(reader.GetString(0));
            }
        }

        var unsupported = normalizedCodes.Except(activeCodes).ToList();
        if (unsupported.Count > 0)
        {
            throw new InvalidOperationException($"Unsupported language selection: {string.Join(", ", unsupported)}");
        }

        var defaultLanguage = string.IsNullOrWhiteSpace(request.DefaultLanguage)
            ? normalizedCodes[0]
            : request.DefaultLanguage.Trim().ToLowerInvariant();

        if (!normalizedCodes.Contains(defaultLanguage))
        {
            throw new InvalidOperationException("Default language must be one of the enabled languages.");
        }

        await using (var deleteCommand = new NpgsqlCommand("DELETE FROM restaurant_languages WHERE restaurant_id = @restaurantId;", connection, transaction))
        {
            deleteCommand.Parameters.AddWithValue("restaurantId", restaurantId);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var code in activeCodes)
        {
            await using var insertCommand = new NpgsqlCommand("INSERT INTO restaurant_languages (restaurant_id, language_code, is_enabled) VALUES (@restaurantId, @languageCode, @isEnabled);", connection, transaction);
            insertCommand.Parameters.AddWithValue("restaurantId", restaurantId);
            insertCommand.Parameters.AddWithValue("languageCode", code);
            insertCommand.Parameters.AddWithValue("isEnabled", normalizedCodes.Contains(code));
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var updateCommand = new NpgsqlCommand("UPDATE restaurants SET default_language = @defaultLanguage, updated_at = NOW() WHERE id = @restaurantId;", connection, transaction))
        {
            updateCommand.Parameters.AddWithValue("restaurantId", restaurantId);
            updateCommand.Parameters.AddWithValue("defaultLanguage", defaultLanguage);
            await updateCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await WriteAuditAsync(connection, transaction, restaurantId, userId, request, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetRestaurantLanguagesAsync(restaurantId, cancellationToken);
    }

    private static async Task WriteAuditAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid restaurantId, Guid userId, UpdateRestaurantLanguagesRequest request, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("INSERT INTO audit_logs (restaurant_id, user_id, action_type, entity_name, entity_id, payload) VALUES (@restaurantId, @userId, @actionType, @entityName, @entityId, CAST(@payload AS jsonb));", connection, transaction);
        command.Parameters.AddWithValue("restaurantId", restaurantId);
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("actionType", "RESTAURANT_LANGUAGES_UPDATED");
        command.Parameters.AddWithValue("entityName", "restaurant_language_settings");
        command.Parameters.AddWithValue("entityId", restaurantId.ToString());
        command.Parameters.AddWithValue("payload", JsonSerializer.Serialize(request));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

sealed class PublicMenuService(AppDb appDb)
{
    public async Task<PublicRestaurantResponse?> GetRestaurantAsync(string slug, string? requestedLanguage, CancellationToken cancellationToken)
    {
        var restaurant = await LoadRestaurantAsync(slug, cancellationToken);
        if (restaurant is null)
        {
            return null;
        }

        restaurant.SelectedLanguage = ResolveLanguage(requestedLanguage, restaurant);
        return restaurant;
    }

    public async Task<PublicMenuResponse?> GetMenuAsync(string slug, string? requestedLanguage, string? search, CancellationToken cancellationToken)
    {
        var restaurant = await LoadRestaurantAsync(slug, cancellationToken);
        if (restaurant is null)
        {
            return null;
        }

        var languageCode = ResolveLanguage(requestedLanguage, restaurant);
        restaurant.SelectedLanguage = languageCode;
        var categories = await LoadCategoriesAsync(restaurant, languageCode, search, cancellationToken);

        return new PublicMenuResponse
        {
            Restaurant = restaurant,
            LanguageCode = languageCode,
            Search = search?.Trim() ?? string.Empty,
            Categories = categories
        };
    }

    private async Task<PublicRestaurantResponse?> LoadRestaurantAsync(string slug, CancellationToken cancellationToken)
    {
        const string restaurantSql = """
            SELECT id, name, slug, phone, whatsapp_phone, email, address, logo_url, default_language, currency
            FROM restaurants
            WHERE slug = @slug AND is_active = TRUE;
            """;

        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(restaurantSql, connection);
        command.Parameters.AddWithValue("slug", slug.Trim().ToLowerInvariant());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var restaurant = new PublicRestaurantResponse
        {
            Id = reader.GetGuid(reader.GetOrdinal("id")),
            Name = reader.GetString(reader.GetOrdinal("name")),
            Slug = reader.GetString(reader.GetOrdinal("slug")),
            Phone = DbValueOrEmpty(reader, "phone"),
            WhatsappPhone = DbValueOrEmpty(reader, "whatsapp_phone"),
            Email = DbValueOrEmpty(reader, "email"),
            Address = DbValueOrEmpty(reader, "address"),
            LogoUrl = DbValueOrEmpty(reader, "logo_url"),
            DefaultLanguage = reader.GetString(reader.GetOrdinal("default_language")),
            Currency = reader.GetString(reader.GetOrdinal("currency"))
        };

        await reader.CloseAsync();
        restaurant.ActiveLanguages = (await LoadRestaurantLanguagesAsync(connection, restaurant.Id, restaurant.DefaultLanguage, cancellationToken)).ToList();
        return restaurant;
    }

    private static async Task<IReadOnlyList<LanguageOptionResponse>> LoadRestaurantLanguagesAsync(NpgsqlConnection connection, Guid restaurantId, string defaultLanguage, CancellationToken cancellationToken)
    {
        const string languageSql = """
            SELECT l.code, l.name, l.is_active, COALESCE(rl.is_enabled, TRUE) AS is_enabled
            FROM languages l
            LEFT JOIN restaurant_languages rl ON rl.restaurant_id = @restaurantId AND rl.language_code = l.code
            WHERE l.is_active = TRUE
            ORDER BY CASE l.code WHEN 'tr' THEN 0 WHEN 'en' THEN 1 WHEN 'de' THEN 2 WHEN 'ru' THEN 3 ELSE 9 END, l.name;
            """;

        await using var command = new NpgsqlCommand(languageSql, connection);
        command.Parameters.AddWithValue("restaurantId", restaurantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var languages = new List<LanguageOptionResponse>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var code = reader.GetString(reader.GetOrdinal("code"));
            var isEnabled = reader.GetBoolean(reader.GetOrdinal("is_enabled"));
            if (!isEnabled)
            {
                continue;
            }

            languages.Add(new LanguageOptionResponse
            {
                Code = code,
                Name = reader.GetString(reader.GetOrdinal("name")),
                IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                IsEnabled = true,
                IsDefault = string.Equals(code, defaultLanguage, StringComparison.OrdinalIgnoreCase)
            });
        }

        if (languages.Count == 0)
        {
            languages.Add(new LanguageOptionResponse
            {
                Code = defaultLanguage,
                Name = defaultLanguage.ToUpperInvariant(),
                IsActive = true,
                IsEnabled = true,
                IsDefault = true
            });
        }

        return languages;
    }

    private async Task<IReadOnlyList<PublicMenuCategoryResponse>> LoadCategoriesAsync(PublicRestaurantResponse restaurant, string languageCode, string? search, CancellationToken cancellationToken)
    {
        const string categorySql = """
            SELECT
                c.id,
                c.slug,
                c.image_url,
                c.sort_order,
                COALESCE(NULLIF(cts.name, ''), NULLIF(ctd.name, ''), c.name) AS display_name,
                COALESCE(NULLIF(cts.description, ''), NULLIF(ctd.description, ''), c.description) AS display_description
            FROM menu_categories c
            LEFT JOIN menu_category_translations cts ON cts.category_id = c.id AND cts.language_code = @languageCode
            LEFT JOIN menu_category_translations ctd ON ctd.category_id = c.id AND ctd.language_code = @defaultLanguage
            WHERE c.restaurant_id = @restaurantId
              AND c.is_active = TRUE
            ORDER BY c.sort_order, c.name;
            """;

        const string itemSql = """
            SELECT
                i.id,
                i.category_id,
                i.slug,
                i.image_url,
                i.price,
                i.discounted_price,
                i.currency,
                i.preparation_time_minutes,
                i.calories,
                i.spice_level,
                i.is_vegetarian,
                i.is_vegan,
                i.is_gluten_free,
                i.is_featured,
                i.is_available,
                i.sort_order,
                COALESCE(NULLIF(its.name, ''), NULLIF(itd.name, ''), i.name) AS display_name,
                COALESCE(NULLIF(its.description, ''), NULLIF(itd.description, ''), i.description) AS display_description
            FROM menu_items i
            INNER JOIN menu_categories c ON c.id = i.category_id
            LEFT JOIN menu_item_translations its ON its.menu_item_id = i.id AND its.language_code = @languageCode
            LEFT JOIN menu_item_translations itd ON itd.menu_item_id = i.id AND itd.language_code = @defaultLanguage
            WHERE i.restaurant_id = @restaurantId
              AND i.is_active = TRUE
              AND c.is_active = TRUE
              AND (@search = '' OR COALESCE(NULLIF(its.name, ''), NULLIF(itd.name, ''), i.name) ILIKE '%' || @search || '%' OR COALESCE(NULLIF(its.description, ''), NULLIF(itd.description, ''), i.description) ILIKE '%' || @search || '%')
            ORDER BY i.sort_order, i.name;
            """;

        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        var categories = new List<PublicMenuCategoryResponse>();
        var byId = new Dictionary<Guid, PublicMenuCategoryResponse>();

        await using (var categoryCommand = new NpgsqlCommand(categorySql, connection))
        {
            categoryCommand.Parameters.AddWithValue("restaurantId", restaurant.Id);
            categoryCommand.Parameters.AddWithValue("languageCode", languageCode);
            categoryCommand.Parameters.AddWithValue("defaultLanguage", restaurant.DefaultLanguage);
            await using var reader = await categoryCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var category = new PublicMenuCategoryResponse
                {
                    Id = reader.GetGuid(reader.GetOrdinal("id")),
                    Slug = reader.GetString(reader.GetOrdinal("slug")),
                    Name = reader.GetString(reader.GetOrdinal("display_name")),
                    Description = DbValueOrEmpty(reader, "display_description"),
                    ImageUrl = DbValueOrEmpty(reader, "image_url"),
                    SortOrder = reader.GetInt32(reader.GetOrdinal("sort_order")),
                    Items = []
                };

                categories.Add(category);
                byId[category.Id] = category;
            }
        }

        await using (var itemCommand = new NpgsqlCommand(itemSql, connection))
        {
            itemCommand.Parameters.AddWithValue("restaurantId", restaurant.Id);
            itemCommand.Parameters.AddWithValue("languageCode", languageCode);
            itemCommand.Parameters.AddWithValue("defaultLanguage", restaurant.DefaultLanguage);
            itemCommand.Parameters.AddWithValue("search", search?.Trim() ?? string.Empty);
            await using var reader = await itemCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var categoryId = reader.GetGuid(reader.GetOrdinal("category_id"));
                if (!byId.TryGetValue(categoryId, out var category))
                {
                    continue;
                }

                category.Items.Add(new PublicMenuItemResponse
                {
                    Id = reader.GetGuid(reader.GetOrdinal("id")),
                    Slug = reader.GetString(reader.GetOrdinal("slug")),
                    Name = reader.GetString(reader.GetOrdinal("display_name")),
                    Description = DbValueOrEmpty(reader, "display_description"),
                    ImageUrl = DbValueOrEmpty(reader, "image_url"),
                    Price = reader.GetDecimal(reader.GetOrdinal("price")),
                    DiscountedPrice = reader.IsDBNull(reader.GetOrdinal("discounted_price")) ? null : reader.GetDecimal(reader.GetOrdinal("discounted_price")),
                    Currency = reader.GetString(reader.GetOrdinal("currency")),
                    PreparationTimeMinutes = reader.IsDBNull(reader.GetOrdinal("preparation_time_minutes")) ? null : reader.GetInt32(reader.GetOrdinal("preparation_time_minutes")),
                    Calories = reader.IsDBNull(reader.GetOrdinal("calories")) ? null : reader.GetInt32(reader.GetOrdinal("calories")),
                    SpiceLevel = reader.GetInt32(reader.GetOrdinal("spice_level")),
                    IsVegetarian = reader.GetBoolean(reader.GetOrdinal("is_vegetarian")),
                    IsVegan = reader.GetBoolean(reader.GetOrdinal("is_vegan")),
                    IsGlutenFree = reader.GetBoolean(reader.GetOrdinal("is_gluten_free")),
                    IsFeatured = reader.GetBoolean(reader.GetOrdinal("is_featured")),
                    IsAvailable = reader.GetBoolean(reader.GetOrdinal("is_available")),
                    SortOrder = reader.GetInt32(reader.GetOrdinal("sort_order"))
                });
            }
        }

        return categories
            .Where(category => category.Items.Count > 0 || string.IsNullOrWhiteSpace(search))
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name)
            .ToList();
    }

    private static string ResolveLanguage(string? requestedLanguage, PublicRestaurantResponse restaurant)
    {
        var enabled = restaurant.ActiveLanguages.Select(language => language.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var normalized = requestedLanguage?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(normalized) && enabled.Contains(normalized))
        {
            return normalized;
        }

        return enabled.Contains(restaurant.DefaultLanguage) ? restaurant.DefaultLanguage : restaurant.ActiveLanguages[0].Code;
    }

    private static string DbValueOrEmpty(NpgsqlDataReader reader, string columnName) => reader.IsDBNull(reader.GetOrdinal(columnName)) ? string.Empty : reader.GetString(reader.GetOrdinal(columnName));
}

sealed class UpdateRestaurantLanguagesRequest
{
    [MinLength(1)]
    public List<string> EnabledLanguageCodes { get; init; } = [];

    [RegularExpression("^[a-z]{2}$")]
    public string DefaultLanguage { get; init; } = "tr";
}

sealed class LanguageOptionResponse
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public bool IsEnabled { get; init; }
    public bool IsDefault { get; init; }
}

sealed class PublicRestaurantResponse
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
    public string SelectedLanguage { get; set; } = "tr";
    public string Currency { get; init; } = "TRY";
    public List<LanguageOptionResponse> ActiveLanguages { get; set; } = [];
}

sealed class PublicMenuResponse
{
    public PublicRestaurantResponse Restaurant { get; init; } = new();
    public string LanguageCode { get; init; } = "tr";
    public string Search { get; init; } = string.Empty;
    public IReadOnlyList<PublicMenuCategoryResponse> Categories { get; init; } = [];
}

sealed class PublicMenuCategoryResponse
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public List<PublicMenuItemResponse> Items { get; set; } = [];
}

sealed class PublicMenuItemResponse
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public decimal? DiscountedPrice { get; init; }
    public string Currency { get; init; } = string.Empty;
    public int? PreparationTimeMinutes { get; init; }
    public int? Calories { get; init; }
    public int SpiceLevel { get; init; }
    public bool IsVegetarian { get; init; }
    public bool IsVegan { get; init; }
    public bool IsGlutenFree { get; init; }
    public bool IsFeatured { get; init; }
    public bool IsAvailable { get; init; }
    public int SortOrder { get; init; }
}

