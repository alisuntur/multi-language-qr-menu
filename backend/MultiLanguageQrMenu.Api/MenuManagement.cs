
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Npgsql;

static class MenuEndpointExtensions
{
    public static IEndpointRouteBuilder MapMenuManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();

        api.MapGet("/menu-categories", async (ClaimsPrincipal user, MenuCategoryService service, CancellationToken cancellationToken) =>
        {
            var data = await service.GetListAsync(user.GetRestaurantId(), cancellationToken);
            return Results.Ok(ApiResponse.Ok(data));
        });

        api.MapPost("/menu-categories", async (MenuCategoryUpsertRequest request, ClaimsPrincipal user, MenuCategoryService service, CancellationToken cancellationToken) =>
        {
            var guard = GuardMenuEditorAccess(user, request);
            if (guard is not null) return guard;
            var data = await service.CreateAsync(user.GetRestaurantId(), user.GetUserId(), request, cancellationToken);
            return Results.Ok(ApiResponse.Ok(data, "Category created."));
        });

        api.MapPut("/menu-categories/{categoryId:guid}", async (Guid categoryId, MenuCategoryUpsertRequest request, ClaimsPrincipal user, MenuCategoryService service, CancellationToken cancellationToken) =>
        {
            var guard = GuardMenuEditorAccess(user, request);
            if (guard is not null) return guard;
            var data = await service.UpdateAsync(user.GetRestaurantId(), user.GetUserId(), categoryId, request, cancellationToken);
            return Results.Ok(ApiResponse.Ok(data, "Category updated."));
        });

        api.MapDelete("/menu-categories/{categoryId:guid}", async (Guid categoryId, ClaimsPrincipal user, MenuCategoryService service, CancellationToken cancellationToken) =>
        {
            var guard = GuardMenuEditorAccess(user);
            if (guard is not null) return guard;
            await service.DeleteAsync(user.GetRestaurantId(), user.GetUserId(), categoryId, cancellationToken);
            return Results.Ok(ApiResponse.Ok(new { }, "Category deleted."));
        });

        api.MapPatch("/menu-categories/reorder", async (CategoryReorderRequest request, ClaimsPrincipal user, MenuCategoryService service, CancellationToken cancellationToken) =>
        {
            var guard = GuardMenuEditorAccess(user, request);
            if (guard is not null) return guard;
            await service.ReorderAsync(user.GetRestaurantId(), user.GetUserId(), request, cancellationToken);
            return Results.Ok(ApiResponse.Ok(new { }, "Category order updated."));
        });

        api.MapGet("/menu-items", async (Guid? categoryId, ClaimsPrincipal user, MenuItemService service, CancellationToken cancellationToken) =>
        {
            var data = await service.GetListAsync(user.GetRestaurantId(), categoryId, cancellationToken);
            return Results.Ok(ApiResponse.Ok(data));
        });

        api.MapPost("/menu-items", async (MenuItemUpsertRequest request, ClaimsPrincipal user, MenuItemService service, CancellationToken cancellationToken) =>
        {
            var guard = GuardMenuEditorAccess(user, request);
            if (guard is not null) return guard;
            var data = await service.CreateAsync(user.GetRestaurantId(), user.GetUserId(), request, cancellationToken);
            return Results.Ok(ApiResponse.Ok(data, "Menu item created."));
        });

        api.MapPut("/menu-items/{itemId:guid}", async (Guid itemId, MenuItemUpsertRequest request, ClaimsPrincipal user, MenuItemService service, CancellationToken cancellationToken) =>
        {
            var guard = GuardMenuEditorAccess(user, request);
            if (guard is not null) return guard;
            var data = await service.UpdateAsync(user.GetRestaurantId(), user.GetUserId(), itemId, request, cancellationToken);
            return Results.Ok(ApiResponse.Ok(data, "Menu item updated."));
        });

        api.MapDelete("/menu-items/{itemId:guid}", async (Guid itemId, ClaimsPrincipal user, MenuItemService service, CancellationToken cancellationToken) =>
        {
            var guard = GuardMenuEditorAccess(user);
            if (guard is not null) return guard;
            await service.DeleteAsync(user.GetRestaurantId(), user.GetUserId(), itemId, cancellationToken);
            return Results.Ok(ApiResponse.Ok(new { }, "Menu item deleted."));
        });

        api.MapPatch("/menu-items/reorder", async (MenuItemReorderRequest request, ClaimsPrincipal user, MenuItemService service, CancellationToken cancellationToken) =>
        {
            var guard = GuardMenuEditorAccess(user, request);
            if (guard is not null) return guard;
            await service.ReorderAsync(user.GetRestaurantId(), user.GetUserId(), request, cancellationToken);
            return Results.Ok(ApiResponse.Ok(new { }, "Menu item order updated."));
        });

        return app;
    }

    private static IResult? GuardMenuEditorAccess(ClaimsPrincipal user, object? request = null)
    {
        if (!user.HasAnyRole(RoleConstants.RestaurantOwner, RoleConstants.BranchManager, RoleConstants.MenuEditor))
        {
            return Results.Forbid();
        }

        if (request is null)
        {
            return null;
        }

        var errors = ValidationHelpers.Validate(request);
        return errors.Count > 0 ? Results.BadRequest(ApiResponse.Fail("Validation failed", [.. errors])) : null;
    }
}

sealed class MenuCategoryService(AppDb appDb)
{
    public async Task<IReadOnlyList<MenuCategoryResponse>> GetListAsync(Guid restaurantId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                c.id,
                c.restaurant_id,
                c.name,
                c.slug,
                c.description,
                c.image_url,
                c.sort_order,
                c.is_active,
                COALESCE(item_counts.item_count, 0) AS item_count,
                t.language_code,
                t.name AS translation_name,
                t.description AS translation_description
            FROM menu_categories c
            LEFT JOIN menu_category_translations t ON t.category_id = c.id
            LEFT JOIN (
                SELECT category_id, COUNT(*) AS item_count
                FROM menu_items
                WHERE restaurant_id = @restaurantId
                GROUP BY category_id
            ) item_counts ON item_counts.category_id = c.id
            WHERE c.restaurant_id = @restaurantId
            ORDER BY c.sort_order, c.name, t.language_code;
            """;

        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("restaurantId", restaurantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var categories = new Dictionary<Guid, MenuCategoryResponse>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetGuid(reader.GetOrdinal("id"));
            if (!categories.TryGetValue(id, out var category))
            {
                category = new MenuCategoryResponse
                {
                    Id = id,
                    RestaurantId = reader.GetGuid(reader.GetOrdinal("restaurant_id")),
                    Name = reader.GetString(reader.GetOrdinal("name")),
                    Slug = reader.GetString(reader.GetOrdinal("slug")),
                    Description = DbValueOrEmpty(reader, "description"),
                    ImageUrl = DbValueOrEmpty(reader, "image_url"),
                    SortOrder = reader.GetInt32(reader.GetOrdinal("sort_order")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                    ItemCount = reader.GetInt64(reader.GetOrdinal("item_count")),
                    Translations = []
                };
                categories[id] = category;
            }

            if (!reader.IsDBNull(reader.GetOrdinal("language_code")))
            {
                category.Translations.Add(new TranslationResponse
                {
                    LanguageCode = reader.GetString(reader.GetOrdinal("language_code")),
                    Name = reader.GetString(reader.GetOrdinal("translation_name")),
                    Description = DbValueOrEmpty(reader, "translation_description")
                });
            }
        }

        return categories.Values.OrderBy(category => category.SortOrder).ThenBy(category => category.Name).ToList();
    }

    public async Task<MenuCategoryResponse> CreateAsync(Guid restaurantId, Guid userId, MenuCategoryUpsertRequest request, CancellationToken cancellationToken)
    {
        ValidateUniqueTranslations(request.Translations);

        var categoryId = Guid.NewGuid();
        var slug = SlugHelper.Normalize(request.Slug, request.Name);

        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var sortOrder = await GetNextSortOrderAsync(connection, transaction, restaurantId, cancellationToken);

        const string insertCategorySql = """
            INSERT INTO menu_categories
            (id, restaurant_id, name, slug, description, image_url, sort_order, is_active, created_at, updated_at)
            VALUES
            (@id, @restaurantId, @name, @slug, @description, @imageUrl, @sortOrder, @isActive, NOW(), NOW());
            """;

        await using (var command = new NpgsqlCommand(insertCategorySql, connection, transaction))
        {
            command.Parameters.AddWithValue("id", categoryId);
            command.Parameters.AddWithValue("restaurantId", restaurantId);
            command.Parameters.AddWithValue("name", request.Name.Trim());
            command.Parameters.AddWithValue("slug", slug);
            command.Parameters.AddWithValue("description", request.Description.Trim());
            command.Parameters.AddWithValue("imageUrl", request.ImageUrl.Trim());
            command.Parameters.AddWithValue("sortOrder", sortOrder);
            command.Parameters.AddWithValue("isActive", request.IsActive);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await ReplaceTranslationsAsync(connection, transaction, categoryId, request.Translations, cancellationToken);
        await WriteAuditAsync(connection, transaction, restaurantId, userId, "MENU_CATEGORY_CREATED", "menu_category", categoryId.ToString(), request, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetByIdAsync(restaurantId, categoryId, cancellationToken)
            ?? throw new InvalidOperationException("Created category could not be loaded.");
    }

    public async Task<MenuCategoryResponse> UpdateAsync(Guid restaurantId, Guid userId, Guid categoryId, MenuCategoryUpsertRequest request, CancellationToken cancellationToken)
    {
        ValidateUniqueTranslations(request.Translations);
        var slug = SlugHelper.Normalize(request.Slug, request.Name);

        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string updateSql = """
            UPDATE menu_categories
            SET name = @name,
                slug = @slug,
                description = @description,
                image_url = @imageUrl,
                is_active = @isActive,
                updated_at = NOW()
            WHERE id = @categoryId AND restaurant_id = @restaurantId;
            """;

        await using (var command = new NpgsqlCommand(updateSql, connection, transaction))
        {
            command.Parameters.AddWithValue("categoryId", categoryId);
            command.Parameters.AddWithValue("restaurantId", restaurantId);
            command.Parameters.AddWithValue("name", request.Name.Trim());
            command.Parameters.AddWithValue("slug", slug);
            command.Parameters.AddWithValue("description", request.Description.Trim());
            command.Parameters.AddWithValue("imageUrl", request.ImageUrl.Trim());
            command.Parameters.AddWithValue("isActive", request.IsActive);
            var affected = await command.ExecuteNonQueryAsync(cancellationToken);
            if (affected == 0)
            {
                throw new InvalidOperationException("Category could not be found.");
            }
        }

        await ReplaceTranslationsAsync(connection, transaction, categoryId, request.Translations, cancellationToken);
        await WriteAuditAsync(connection, transaction, restaurantId, userId, "MENU_CATEGORY_UPDATED", "menu_category", categoryId.ToString(), request, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetByIdAsync(restaurantId, categoryId, cancellationToken)
            ?? throw new InvalidOperationException("Updated category could not be loaded.");
    }

    public async Task DeleteAsync(Guid restaurantId, Guid userId, Guid categoryId, CancellationToken cancellationToken)
    {
        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string itemCountSql = "SELECT COUNT(*) FROM menu_items WHERE restaurant_id = @restaurantId AND category_id = @categoryId;";
        await using (var countCommand = new NpgsqlCommand(itemCountSql, connection, transaction))
        {
            countCommand.Parameters.AddWithValue("restaurantId", restaurantId);
            countCommand.Parameters.AddWithValue("categoryId", categoryId);
            var itemCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));
            if (itemCount > 0)
            {
                throw new InvalidOperationException("This category still has menu items. Remove or move the items first.");
            }
        }

        const string deleteSql = "DELETE FROM menu_categories WHERE id = @categoryId AND restaurant_id = @restaurantId;";
        await using (var deleteCommand = new NpgsqlCommand(deleteSql, connection, transaction))
        {
            deleteCommand.Parameters.AddWithValue("categoryId", categoryId);
            deleteCommand.Parameters.AddWithValue("restaurantId", restaurantId);
            var affected = await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
            if (affected == 0)
            {
                throw new InvalidOperationException("Category could not be found.");
            }
        }

        await WriteAuditAsync(connection, transaction, restaurantId, userId, "MENU_CATEGORY_DELETED", "menu_category", categoryId.ToString(), new { categoryId }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ReorderAsync(Guid restaurantId, Guid userId, CategoryReorderRequest request, CancellationToken cancellationToken)
    {
        if (request.OrderedCategoryIds.Count == 0)
        {
            throw new InvalidOperationException("At least one category must be provided for reorder.");
        }

        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        for (var index = 0; index < request.OrderedCategoryIds.Count; index++)
        {
            await using var command = new NpgsqlCommand(
                "UPDATE menu_categories SET sort_order = @sortOrder, updated_at = NOW() WHERE id = @categoryId AND restaurant_id = @restaurantId;",
                connection,
                transaction);
            command.Parameters.AddWithValue("sortOrder", index + 1);
            command.Parameters.AddWithValue("categoryId", request.OrderedCategoryIds[index]);
            command.Parameters.AddWithValue("restaurantId", restaurantId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await WriteAuditAsync(connection, transaction, restaurantId, userId, "MENU_CATEGORY_REORDERED", "menu_category", restaurantId.ToString(), request, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<MenuCategoryResponse?> GetByIdAsync(Guid restaurantId, Guid categoryId, CancellationToken cancellationToken)
    {
        var categories = await GetListAsync(restaurantId, cancellationToken);
        return categories.FirstOrDefault(category => category.Id == categoryId);
    }

    private static async Task<int> GetNextSortOrderAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid restaurantId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT COALESCE(MAX(sort_order), 0) + 1 FROM menu_categories WHERE restaurant_id = @restaurantId;", connection, transaction);
        command.Parameters.AddWithValue("restaurantId", restaurantId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task ReplaceTranslationsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid categoryId, IReadOnlyList<TranslationInput> translations, CancellationToken cancellationToken)
    {
        await using (var deleteCommand = new NpgsqlCommand("DELETE FROM menu_category_translations WHERE category_id = @categoryId;", connection, transaction))
        {
            deleteCommand.Parameters.AddWithValue("categoryId", categoryId);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var translation in translations.Where(translation => !string.IsNullOrWhiteSpace(translation.LanguageCode) && !string.IsNullOrWhiteSpace(translation.Name)))
        {
            await using var insertCommand = new NpgsqlCommand(
                "INSERT INTO menu_category_translations (category_id, language_code, name, description) VALUES (@categoryId, @languageCode, @name, @description);",
                connection,
                transaction);
            insertCommand.Parameters.AddWithValue("categoryId", categoryId);
            insertCommand.Parameters.AddWithValue("languageCode", translation.LanguageCode.Trim().ToLowerInvariant());
            insertCommand.Parameters.AddWithValue("name", translation.Name.Trim());
            insertCommand.Parameters.AddWithValue("description", translation.Description.Trim());
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task WriteAuditAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid restaurantId, Guid userId, string actionType, string entityName, string entityId, object payload, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "INSERT INTO audit_logs (restaurant_id, user_id, action_type, entity_name, entity_id, payload) VALUES (@restaurantId, @userId, @actionType, @entityName, @entityId, CAST(@payload AS jsonb));",
            connection,
            transaction);
        command.Parameters.AddWithValue("restaurantId", restaurantId);
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("actionType", actionType);
        command.Parameters.AddWithValue("entityName", entityName);
        command.Parameters.AddWithValue("entityId", entityId);
        command.Parameters.AddWithValue("payload", JsonSerializer.Serialize(payload));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string DbValueOrEmpty(NpgsqlDataReader reader, string columnName) => reader.IsDBNull(reader.GetOrdinal(columnName)) ? string.Empty : reader.GetString(reader.GetOrdinal(columnName));

    private static void ValidateUniqueTranslations(IReadOnlyList<TranslationInput> translations)
    {
        var duplicateLanguage = translations.Where(translation => !string.IsNullOrWhiteSpace(translation.LanguageCode)).GroupBy(translation => translation.LanguageCode.Trim().ToLowerInvariant()).FirstOrDefault(group => group.Count() > 1);
        if (duplicateLanguage is not null)
        {
            throw new InvalidOperationException($"Duplicate translation language detected: {duplicateLanguage.Key}");
        }
    }
}

sealed class MenuItemService(AppDb appDb)
{
    public async Task<IReadOnlyList<MenuItemResponse>> GetListAsync(Guid restaurantId, Guid? categoryId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                i.id,
                i.restaurant_id,
                i.category_id,
                c.name AS category_name,
                i.name,
                i.slug,
                i.description,
                i.price,
                i.discounted_price,
                i.currency,
                i.image_url,
                i.preparation_time_minutes,
                i.calories,
                i.spice_level,
                i.is_vegetarian,
                i.is_vegan,
                i.is_gluten_free,
                i.is_featured,
                i.is_available,
                i.is_active,
                i.sort_order,
                t.language_code,
                t.name AS translation_name,
                t.description AS translation_description
            FROM menu_items i
            INNER JOIN menu_categories c ON c.id = i.category_id
            LEFT JOIN menu_item_translations t ON t.menu_item_id = i.id
            WHERE i.restaurant_id = @restaurantId
              AND (@categoryId::uuid IS NULL OR i.category_id = @categoryId)
            ORDER BY c.sort_order, i.sort_order, i.name, t.language_code;
            """;

        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("restaurantId", restaurantId);
        command.Parameters.AddWithValue("categoryId", NpgsqlTypes.NpgsqlDbType.Uuid, categoryId ?? (object)DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var items = new Dictionary<Guid, MenuItemResponse>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetGuid(reader.GetOrdinal("id"));
            if (!items.TryGetValue(id, out var item))
            {
                item = new MenuItemResponse
                {
                    Id = id,
                    RestaurantId = reader.GetGuid(reader.GetOrdinal("restaurant_id")),
                    CategoryId = reader.GetGuid(reader.GetOrdinal("category_id")),
                    CategoryName = reader.GetString(reader.GetOrdinal("category_name")),
                    Name = reader.GetString(reader.GetOrdinal("name")),
                    Slug = reader.GetString(reader.GetOrdinal("slug")),
                    Description = DbValueOrEmpty(reader, "description"),
                    Price = reader.GetDecimal(reader.GetOrdinal("price")),
                    DiscountedPrice = reader.IsDBNull(reader.GetOrdinal("discounted_price")) ? null : reader.GetDecimal(reader.GetOrdinal("discounted_price")),
                    Currency = reader.GetString(reader.GetOrdinal("currency")),
                    ImageUrl = DbValueOrEmpty(reader, "image_url"),
                    PreparationTimeMinutes = reader.IsDBNull(reader.GetOrdinal("preparation_time_minutes")) ? null : reader.GetInt32(reader.GetOrdinal("preparation_time_minutes")),
                    Calories = reader.IsDBNull(reader.GetOrdinal("calories")) ? null : reader.GetInt32(reader.GetOrdinal("calories")),
                    SpiceLevel = reader.GetInt32(reader.GetOrdinal("spice_level")),
                    IsVegetarian = reader.GetBoolean(reader.GetOrdinal("is_vegetarian")),
                    IsVegan = reader.GetBoolean(reader.GetOrdinal("is_vegan")),
                    IsGlutenFree = reader.GetBoolean(reader.GetOrdinal("is_gluten_free")),
                    IsFeatured = reader.GetBoolean(reader.GetOrdinal("is_featured")),
                    IsAvailable = reader.GetBoolean(reader.GetOrdinal("is_available")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                    SortOrder = reader.GetInt32(reader.GetOrdinal("sort_order")),
                    Translations = []
                };
                items[id] = item;
            }

            if (!reader.IsDBNull(reader.GetOrdinal("language_code")))
            {
                item.Translations.Add(new TranslationResponse
                {
                    LanguageCode = reader.GetString(reader.GetOrdinal("language_code")),
                    Name = reader.GetString(reader.GetOrdinal("translation_name")),
                    Description = DbValueOrEmpty(reader, "translation_description")
                });
            }
        }

        return items.Values.OrderBy(item => item.CategoryName).ThenBy(item => item.SortOrder).ThenBy(item => item.Name).ToList();
    }

    public async Task<MenuItemResponse> CreateAsync(Guid restaurantId, Guid userId, MenuItemUpsertRequest request, CancellationToken cancellationToken)
    {
        ValidateUniqueTranslations(request.Translations);
        ValidateItemPricing(request);
        var slug = SlugHelper.Normalize(request.Slug, request.Name);

        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await EnsureCategoryBelongsToRestaurantAsync(connection, transaction, restaurantId, request.CategoryId, cancellationToken);
        var sortOrder = await GetNextSortOrderAsync(connection, transaction, restaurantId, request.CategoryId, cancellationToken);
        var itemId = Guid.NewGuid();

        const string insertSql = """
            INSERT INTO menu_items
            (id, restaurant_id, category_id, name, slug, description, price, discounted_price, currency, image_url, preparation_time_minutes, calories, spice_level, is_vegetarian, is_vegan, is_gluten_free, is_featured, is_available, is_active, sort_order, created_at, updated_at)
            VALUES
            (@id, @restaurantId, @categoryId, @name, @slug, @description, @price, @discountedPrice, @currency, @imageUrl, @preparationTimeMinutes, @calories, @spiceLevel, @isVegetarian, @isVegan, @isGlutenFree, @isFeatured, @isAvailable, @isActive, @sortOrder, NOW(), NOW());
            """;

        await using (var command = new NpgsqlCommand(insertSql, connection, transaction))
        {
            FillItemParameters(command, itemId, restaurantId, request, slug, sortOrder);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await ReplaceTranslationsAsync(connection, transaction, itemId, request.Translations, cancellationToken);
        await WriteAuditAsync(connection, transaction, restaurantId, userId, "MENU_ITEM_CREATED", "menu_item", itemId.ToString(), request, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetByIdAsync(restaurantId, itemId, cancellationToken) ?? throw new InvalidOperationException("Created menu item could not be loaded.");
    }

    public async Task<MenuItemResponse> UpdateAsync(Guid restaurantId, Guid userId, Guid itemId, MenuItemUpsertRequest request, CancellationToken cancellationToken)
    {
        ValidateUniqueTranslations(request.Translations);
        ValidateItemPricing(request);
        var slug = SlugHelper.Normalize(request.Slug, request.Name);

        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await EnsureCategoryBelongsToRestaurantAsync(connection, transaction, restaurantId, request.CategoryId, cancellationToken);
        var sortOrder = await GetExistingOrNextSortOrderAsync(connection, transaction, restaurantId, itemId, request.CategoryId, cancellationToken);

        const string updateSql = """
            UPDATE menu_items
            SET category_id = @categoryId,
                name = @name,
                slug = @slug,
                description = @description,
                price = @price,
                discounted_price = @discountedPrice,
                currency = @currency,
                image_url = @imageUrl,
                preparation_time_minutes = @preparationTimeMinutes,
                calories = @calories,
                spice_level = @spiceLevel,
                is_vegetarian = @isVegetarian,
                is_vegan = @isVegan,
                is_gluten_free = @isGlutenFree,
                is_featured = @isFeatured,
                is_available = @isAvailable,
                is_active = @isActive,
                sort_order = @sortOrder,
                updated_at = NOW()
            WHERE id = @id AND restaurant_id = @restaurantId;
            """;

        await using (var command = new NpgsqlCommand(updateSql, connection, transaction))
        {
            FillItemParameters(command, itemId, restaurantId, request, slug, sortOrder);
            var affected = await command.ExecuteNonQueryAsync(cancellationToken);
            if (affected == 0)
            {
                throw new InvalidOperationException("Menu item could not be found.");
            }
        }

        await ReplaceTranslationsAsync(connection, transaction, itemId, request.Translations, cancellationToken);
        await WriteAuditAsync(connection, transaction, restaurantId, userId, "MENU_ITEM_UPDATED", "menu_item", itemId.ToString(), request, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetByIdAsync(restaurantId, itemId, cancellationToken) ?? throw new InvalidOperationException("Updated menu item could not be loaded.");
    }

    public async Task DeleteAsync(Guid restaurantId, Guid userId, Guid itemId, CancellationToken cancellationToken)
    {
        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var command = new NpgsqlCommand("DELETE FROM menu_items WHERE id = @itemId AND restaurant_id = @restaurantId;", connection, transaction))
        {
            command.Parameters.AddWithValue("itemId", itemId);
            command.Parameters.AddWithValue("restaurantId", restaurantId);
            var affected = await command.ExecuteNonQueryAsync(cancellationToken);
            if (affected == 0)
            {
                throw new InvalidOperationException("Menu item could not be found.");
            }
        }

        await WriteAuditAsync(connection, transaction, restaurantId, userId, "MENU_ITEM_DELETED", "menu_item", itemId.ToString(), new { itemId }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ReorderAsync(Guid restaurantId, Guid userId, MenuItemReorderRequest request, CancellationToken cancellationToken)
    {
        if (request.OrderedItemIds.Count == 0)
        {
            throw new InvalidOperationException("At least one menu item must be provided for reorder.");
        }

        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await EnsureCategoryBelongsToRestaurantAsync(connection, transaction, restaurantId, request.CategoryId, cancellationToken);

        for (var index = 0; index < request.OrderedItemIds.Count; index++)
        {
            await using var command = new NpgsqlCommand("UPDATE menu_items SET sort_order = @sortOrder, updated_at = NOW() WHERE id = @itemId AND category_id = @categoryId AND restaurant_id = @restaurantId;", connection, transaction);
            command.Parameters.AddWithValue("sortOrder", index + 1);
            command.Parameters.AddWithValue("itemId", request.OrderedItemIds[index]);
            command.Parameters.AddWithValue("categoryId", request.CategoryId);
            command.Parameters.AddWithValue("restaurantId", restaurantId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await WriteAuditAsync(connection, transaction, restaurantId, userId, "MENU_ITEM_REORDERED", "menu_item", request.CategoryId.ToString(), request, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<MenuItemResponse?> GetByIdAsync(Guid restaurantId, Guid itemId, CancellationToken cancellationToken)
    {
        var items = await GetListAsync(restaurantId, null, cancellationToken);
        return items.FirstOrDefault(item => item.Id == itemId);
    }

    private static async Task EnsureCategoryBelongsToRestaurantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid restaurantId, Guid categoryId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT COUNT(*) FROM menu_categories WHERE id = @categoryId AND restaurant_id = @restaurantId;", connection, transaction);
        command.Parameters.AddWithValue("categoryId", categoryId);
        command.Parameters.AddWithValue("restaurantId", restaurantId);
        var exists = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
        if (!exists)
        {
            throw new InvalidOperationException("Selected category does not belong to the restaurant.");
        }
    }

    private static async Task<int> GetNextSortOrderAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid restaurantId, Guid categoryId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT COALESCE(MAX(sort_order), 0) + 1 FROM menu_items WHERE restaurant_id = @restaurantId AND category_id = @categoryId;", connection, transaction);
        command.Parameters.AddWithValue("restaurantId", restaurantId);
        command.Parameters.AddWithValue("categoryId", categoryId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<int> GetExistingOrNextSortOrderAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid restaurantId, Guid itemId, Guid categoryId, CancellationToken cancellationToken)
    {
        await using var currentCommand = new NpgsqlCommand("SELECT category_id, sort_order FROM menu_items WHERE id = @itemId AND restaurant_id = @restaurantId;", connection, transaction);
        currentCommand.Parameters.AddWithValue("itemId", itemId);
        currentCommand.Parameters.AddWithValue("restaurantId", restaurantId);
        await using var reader = await currentCommand.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Menu item could not be found.");
        }

        var currentCategoryId = reader.GetGuid(reader.GetOrdinal("category_id"));
        var currentSortOrder = reader.GetInt32(reader.GetOrdinal("sort_order"));
        await reader.CloseAsync();

        return currentCategoryId == categoryId ? currentSortOrder : await GetNextSortOrderAsync(connection, transaction, restaurantId, categoryId, cancellationToken);
    }

    private static async Task ReplaceTranslationsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid itemId, IReadOnlyList<TranslationInput> translations, CancellationToken cancellationToken)
    {
        await using (var deleteCommand = new NpgsqlCommand("DELETE FROM menu_item_translations WHERE menu_item_id = @itemId;", connection, transaction))
        {
            deleteCommand.Parameters.AddWithValue("itemId", itemId);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var translation in translations.Where(translation => !string.IsNullOrWhiteSpace(translation.LanguageCode) && !string.IsNullOrWhiteSpace(translation.Name)))
        {
            await using var insertCommand = new NpgsqlCommand("INSERT INTO menu_item_translations (menu_item_id, language_code, name, description) VALUES (@itemId, @languageCode, @name, @description);", connection, transaction);
            insertCommand.Parameters.AddWithValue("itemId", itemId);
            insertCommand.Parameters.AddWithValue("languageCode", translation.LanguageCode.Trim().ToLowerInvariant());
            insertCommand.Parameters.AddWithValue("name", translation.Name.Trim());
            insertCommand.Parameters.AddWithValue("description", translation.Description.Trim());
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static void FillItemParameters(NpgsqlCommand command, Guid itemId, Guid restaurantId, MenuItemUpsertRequest request, string slug, int sortOrder)
    {
        command.Parameters.AddWithValue("id", itemId);
        command.Parameters.AddWithValue("restaurantId", restaurantId);
        command.Parameters.AddWithValue("categoryId", request.CategoryId);
        command.Parameters.AddWithValue("name", request.Name.Trim());
        command.Parameters.AddWithValue("slug", slug);
        command.Parameters.AddWithValue("description", request.Description.Trim());
        command.Parameters.AddWithValue("price", request.Price);
        command.Parameters.AddWithValue("discountedPrice", request.DiscountedPrice ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("currency", request.Currency.Trim().ToUpperInvariant());
        command.Parameters.AddWithValue("imageUrl", request.ImageUrl.Trim());
        command.Parameters.AddWithValue("preparationTimeMinutes", request.PreparationTimeMinutes ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("calories", request.Calories ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("spiceLevel", request.SpiceLevel);
        command.Parameters.AddWithValue("isVegetarian", request.IsVegetarian);
        command.Parameters.AddWithValue("isVegan", request.IsVegan);
        command.Parameters.AddWithValue("isGlutenFree", request.IsGlutenFree);
        command.Parameters.AddWithValue("isFeatured", request.IsFeatured);
        command.Parameters.AddWithValue("isAvailable", request.IsAvailable);
        command.Parameters.AddWithValue("isActive", request.IsActive);
        command.Parameters.AddWithValue("sortOrder", sortOrder);
    }

    private static async Task WriteAuditAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid restaurantId, Guid userId, string actionType, string entityName, string entityId, object payload, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("INSERT INTO audit_logs (restaurant_id, user_id, action_type, entity_name, entity_id, payload) VALUES (@restaurantId, @userId, @actionType, @entityName, @entityId, CAST(@payload AS jsonb));", connection, transaction);
        command.Parameters.AddWithValue("restaurantId", restaurantId);
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("actionType", actionType);
        command.Parameters.AddWithValue("entityName", entityName);
        command.Parameters.AddWithValue("entityId", entityId);
        command.Parameters.AddWithValue("payload", JsonSerializer.Serialize(payload));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string DbValueOrEmpty(NpgsqlDataReader reader, string columnName) => reader.IsDBNull(reader.GetOrdinal(columnName)) ? string.Empty : reader.GetString(reader.GetOrdinal(columnName));

    private static void ValidateItemPricing(MenuItemUpsertRequest request)
    {
        if (request.Price < 0)
        {
            throw new InvalidOperationException("Price cannot be negative.");
        }

        if (request.DiscountedPrice is not null && (request.DiscountedPrice < 0 || request.DiscountedPrice > request.Price))
        {
            throw new InvalidOperationException("Discounted price must be between 0 and price.");
        }
    }

    private static void ValidateUniqueTranslations(IReadOnlyList<TranslationInput> translations)
    {
        var duplicateLanguage = translations.Where(translation => !string.IsNullOrWhiteSpace(translation.LanguageCode)).GroupBy(translation => translation.LanguageCode.Trim().ToLowerInvariant()).FirstOrDefault(group => group.Count() > 1);
        if (duplicateLanguage is not null)
        {
            throw new InvalidOperationException($"Duplicate translation language detected: {duplicateLanguage.Key}");
        }
    }
}

sealed class MenuCategoryUpsertRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; init; } = string.Empty;
    [MaxLength(200)]
    public string Slug { get; init; } = string.Empty;
    [MaxLength(4000)]
    public string Description { get; init; } = string.Empty;
    [MaxLength(512)]
    public string ImageUrl { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
    public List<TranslationInput> Translations { get; init; } = [];
}

sealed class MenuItemUpsertRequest
{
    [Required]
    public Guid CategoryId { get; init; }
    [Required]
    [MaxLength(200)]
    public string Name { get; init; } = string.Empty;
    [MaxLength(200)]
    public string Slug { get; init; } = string.Empty;
    [MaxLength(4000)]
    public string Description { get; init; } = string.Empty;
    [Range(typeof(decimal), "0", "9999999")]
    public decimal Price { get; init; }
    [Range(typeof(decimal), "0", "9999999")]
    public decimal? DiscountedPrice { get; init; }
    [Required]
    [MaxLength(8)]
    public string Currency { get; init; } = "TRY";
    [MaxLength(512)]
    public string ImageUrl { get; init; } = string.Empty;
    [Range(0, 300)]
    public int? PreparationTimeMinutes { get; init; }
    [Range(0, 5000)]
    public int? Calories { get; init; }
    [Range(0, 5)]
    public int SpiceLevel { get; init; }
    public bool IsVegetarian { get; init; }
    public bool IsVegan { get; init; }
    public bool IsGlutenFree { get; init; }
    public bool IsFeatured { get; init; }
    public bool IsAvailable { get; init; } = true;
    public bool IsActive { get; init; } = true;
    public List<TranslationInput> Translations { get; init; } = [];
}

sealed class CategoryReorderRequest
{
    [MinLength(1)]
    public List<Guid> OrderedCategoryIds { get; init; } = [];
}

sealed class MenuItemReorderRequest
{
    public Guid CategoryId { get; init; }
    [MinLength(1)]
    public List<Guid> OrderedItemIds { get; init; } = [];
}

sealed class TranslationInput
{
    [RegularExpression("^[a-z]{2}$")]
    public string LanguageCode { get; init; } = string.Empty;
    [MaxLength(200)]
    public string Name { get; init; } = string.Empty;
    [MaxLength(4000)]
    public string Description { get; init; } = string.Empty;
}

sealed class MenuCategoryResponse
{
    public Guid Id { get; init; }
    public Guid RestaurantId { get; init; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public long ItemCount { get; set; }
    public List<TranslationResponse> Translations { get; set; } = [];
}

sealed class MenuItemResponse
{
    public Guid Id { get; init; }
    public Guid RestaurantId { get; init; }
    public Guid CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public decimal? DiscountedPrice { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    public int? PreparationTimeMinutes { get; init; }
    public int? Calories { get; init; }
    public int SpiceLevel { get; init; }
    public bool IsVegetarian { get; init; }
    public bool IsVegan { get; init; }
    public bool IsGlutenFree { get; init; }
    public bool IsFeatured { get; init; }
    public bool IsAvailable { get; init; }
    public bool IsActive { get; init; }
    public int SortOrder { get; init; }
    public List<TranslationResponse> Translations { get; set; } = [];
}

sealed class TranslationResponse
{
    public string LanguageCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

static class ValidationHelpers
{
    public static List<string> Validate<T>(T model)
    {
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(model!, new ValidationContext(model!), validationResults, true);
        return validationResults.Select(result => result.ErrorMessage ?? "Validation error.").ToList();
    }
}

static class ClaimsPrincipalMenuExtensions
{
    public static bool HasAnyRole(this ClaimsPrincipal principal, params string[] roles) => roles.Any(principal.IsInRole);
}

static class SlugHelper
{
    public static string Normalize(string? slug, string fallback)
    {
        var value = string.IsNullOrWhiteSpace(slug) ? fallback : slug;
        var normalized = Regex.Replace(value.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-");
        normalized = Regex.Replace(normalized, "-+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? Guid.NewGuid().ToString("N") : normalized;
    }
}

