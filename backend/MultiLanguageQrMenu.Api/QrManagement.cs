using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Npgsql;

static class QrManagementEndpointExtensions
{
    public static IEndpointRouteBuilder MapQrManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();

        api.MapGet("/tables", async (ClaimsPrincipal user, TableService service, CancellationToken cancellationToken) =>
        {
            var data = await service.GetTablesAsync(user.GetRestaurantId(), cancellationToken);
            return Results.Ok(ApiResponse.Ok(data));
        });

        api.MapPost("/tables", async (UpsertRestaurantTableRequest request, ClaimsPrincipal user, TableService service, CancellationToken cancellationToken) =>
        {
            var errors = ValidationHelpers.Validate(request);
            if (errors.Count > 0) return Results.BadRequest(ApiResponse.Fail("Validation failed", [.. errors]));
            if (!user.HasAnyRole(RoleConstants.RestaurantOwner, RoleConstants.BranchManager)) return Results.Forbid();
            var data = await service.CreateAsync(user.GetRestaurantId(), user.GetUserId(), request, cancellationToken);
            return Results.Ok(ApiResponse.Ok(data, "Table created."));
        });

        api.MapPut("/tables/{tableId:guid}", async (Guid tableId, UpsertRestaurantTableRequest request, ClaimsPrincipal user, TableService service, CancellationToken cancellationToken) =>
        {
            var errors = ValidationHelpers.Validate(request);
            if (errors.Count > 0) return Results.BadRequest(ApiResponse.Fail("Validation failed", [.. errors]));
            if (!user.HasAnyRole(RoleConstants.RestaurantOwner, RoleConstants.BranchManager)) return Results.Forbid();
            var data = await service.UpdateAsync(user.GetRestaurantId(), user.GetUserId(), tableId, request, cancellationToken);
            return Results.Ok(ApiResponse.Ok(data, "Table updated."));
        });

        api.MapDelete("/tables/{tableId:guid}", async (Guid tableId, ClaimsPrincipal user, TableService service, CancellationToken cancellationToken) =>
        {
            if (!user.HasAnyRole(RoleConstants.RestaurantOwner, RoleConstants.BranchManager)) return Results.Forbid();
            await service.DeleteAsync(user.GetRestaurantId(), user.GetUserId(), tableId, cancellationToken);
            return Results.Ok(ApiResponse.Ok(new { }, "Table deleted."));
        });

        api.MapPost("/tables/{tableId:guid}/qr/regenerate", async (Guid tableId, ClaimsPrincipal user, TableService service, CancellationToken cancellationToken) =>
        {
            if (!user.HasAnyRole(RoleConstants.RestaurantOwner, RoleConstants.BranchManager)) return Results.Forbid();
            var data = await service.RegenerateQrAsync(user.GetRestaurantId(), user.GetUserId(), tableId, cancellationToken);
            return Results.Ok(ApiResponse.Ok(data, "Table QR regenerated."));
        });

        api.MapGet("/qr/general", async (ClaimsPrincipal user, QrCodeService service, CancellationToken cancellationToken) =>
        {
            var data = await service.GetGeneralQrAsync(user.GetRestaurantId(), cancellationToken);
            return Results.Ok(ApiResponse.Ok(data));
        });

        api.MapPost("/qr/general/regenerate", async (ClaimsPrincipal user, QrCodeService service, CancellationToken cancellationToken) =>
        {
            if (!user.HasAnyRole(RoleConstants.RestaurantOwner, RoleConstants.BranchManager)) return Results.Forbid();
            var data = await service.RegenerateGeneralQrAsync(user.GetRestaurantId(), user.GetUserId(), cancellationToken);
            return Results.Ok(ApiResponse.Ok(data, "General QR regenerated."));
        });

        api.MapGet("/qr/printable", async (ClaimsPrincipal user, QrCodeService service, CancellationToken cancellationToken) =>
        {
            var data = await service.GetPrintableCardsAsync(user.GetRestaurantId(), cancellationToken);
            return Results.Ok(ApiResponse.Ok(data));
        });

        return app;
    }
}

sealed class TableService(AppDb appDb, QrCodeService qrService)
{
    public async Task<IReadOnlyList<RestaurantTableResponse>> GetTablesAsync(Guid restaurantId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT id, table_number, table_name, qr_token, is_active FROM restaurant_tables WHERE restaurant_id = @restaurantId ORDER BY table_number, table_name;";
        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        var restaurant = await qrService.GetRestaurantSummaryAsync(connection, restaurantId, cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("restaurantId", restaurantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<RestaurantTableResponse>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var number = reader.GetInt32(reader.GetOrdinal("table_number"));
            var name = DbValueOrEmpty(reader, "table_name");
            var token = reader.GetString(reader.GetOrdinal("qr_token"));
            result.Add(qrService.BuildTableResponse(reader.GetGuid(reader.GetOrdinal("id")), number, name, token, reader.GetBoolean(reader.GetOrdinal("is_active")), restaurant.Slug));
        }
        return result;
    }

    public async Task<RestaurantTableResponse> CreateAsync(Guid restaurantId, Guid userId, UpsertRestaurantTableRequest request, CancellationToken cancellationToken)
    {
        await EnsureTableNumberAvailableAsync(restaurantId, request.TableNumber, null, cancellationToken);
        var tableId = Guid.NewGuid();
        var token = QrCodeService.CreateToken();
        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var command = new NpgsqlCommand("INSERT INTO restaurant_tables (id, restaurant_id, table_number, table_name, qr_token, is_active) VALUES (@id, @restaurantId, @tableNumber, @tableName, @token, @isActive);", connection, transaction))
        {
            command.Parameters.AddWithValue("id", tableId);
            command.Parameters.AddWithValue("restaurantId", restaurantId);
            command.Parameters.AddWithValue("tableNumber", request.TableNumber);
            command.Parameters.AddWithValue("tableName", request.TableName.Trim());
            command.Parameters.AddWithValue("token", token);
            command.Parameters.AddWithValue("isActive", request.IsActive);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        var restaurant = await qrService.GetRestaurantSummaryAsync(connection, restaurantId, cancellationToken, transaction);
        await qrService.ReplaceTableQrAsync(connection, transaction, restaurant, tableId, request.TableNumber, request.TableName.Trim(), token, request.IsActive, cancellationToken);
        await WriteAuditAsync(connection, transaction, restaurantId, userId, "TABLE_CREATED", tableId.ToString(), request, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return qrService.BuildTableResponse(tableId, request.TableNumber, request.TableName.Trim(), token, request.IsActive, restaurant.Slug);
    }

    public async Task<RestaurantTableResponse> UpdateAsync(Guid restaurantId, Guid userId, Guid tableId, UpsertRestaurantTableRequest request, CancellationToken cancellationToken)
    {
        await EnsureTableNumberAvailableAsync(restaurantId, request.TableNumber, tableId, cancellationToken);
        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("UPDATE restaurant_tables SET table_number = @tableNumber, table_name = @tableName, is_active = @isActive, updated_at = NOW() WHERE id = @tableId AND restaurant_id = @restaurantId RETURNING qr_token;", connection, transaction);
        command.Parameters.AddWithValue("tableId", tableId);
        command.Parameters.AddWithValue("restaurantId", restaurantId);
        command.Parameters.AddWithValue("tableNumber", request.TableNumber);
        command.Parameters.AddWithValue("tableName", request.TableName.Trim());
        command.Parameters.AddWithValue("isActive", request.IsActive);
        var token = (string?)await command.ExecuteScalarAsync(cancellationToken) ?? throw new InvalidOperationException("Table could not be found.");
        var restaurant = await qrService.GetRestaurantSummaryAsync(connection, restaurantId, cancellationToken, transaction);
        await qrService.ReplaceTableQrAsync(connection, transaction, restaurant, tableId, request.TableNumber, request.TableName.Trim(), token, request.IsActive, cancellationToken);
        await WriteAuditAsync(connection, transaction, restaurantId, userId, "TABLE_UPDATED", tableId.ToString(), request, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return qrService.BuildTableResponse(tableId, request.TableNumber, request.TableName.Trim(), token, request.IsActive, restaurant.Slug);
    }

    public async Task DeleteAsync(Guid restaurantId, Guid userId, Guid tableId, CancellationToken cancellationToken)
    {
        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var qrCommand = new NpgsqlCommand("UPDATE qr_codes SET is_active = FALSE, revoked_at = NOW(), updated_at = NOW() WHERE restaurant_id = @restaurantId AND table_id = @tableId;", connection, transaction))
        {
            qrCommand.Parameters.AddWithValue("restaurantId", restaurantId);
            qrCommand.Parameters.AddWithValue("tableId", tableId);
            await qrCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        await using var command = new NpgsqlCommand("DELETE FROM restaurant_tables WHERE id = @tableId AND restaurant_id = @restaurantId;", connection, transaction);
        command.Parameters.AddWithValue("tableId", tableId);
        command.Parameters.AddWithValue("restaurantId", restaurantId);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0) throw new InvalidOperationException("Table could not be found.");
        await WriteAuditAsync(connection, transaction, restaurantId, userId, "TABLE_DELETED", tableId.ToString(), new { TableId = tableId }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<RestaurantTableResponse> RegenerateQrAsync(Guid restaurantId, Guid userId, Guid tableId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT table_number, table_name, is_active FROM restaurant_tables WHERE id = @tableId AND restaurant_id = @restaurantId;";
        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("tableId", tableId);
        command.Parameters.AddWithValue("restaurantId", restaurantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Table could not be found.");
        var number = reader.GetInt32(0);
        var name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
        var isActive = reader.GetBoolean(2);
        await reader.CloseAsync();
        var token = QrCodeService.CreateToken();
        await using (var update = new NpgsqlCommand("UPDATE restaurant_tables SET qr_token = @token, updated_at = NOW() WHERE id = @tableId;", connection, transaction))
        {
            update.Parameters.AddWithValue("token", token);
            update.Parameters.AddWithValue("tableId", tableId);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }
        var restaurant = await qrService.GetRestaurantSummaryAsync(connection, restaurantId, cancellationToken, transaction);
        await qrService.ReplaceTableQrAsync(connection, transaction, restaurant, tableId, number, name, token, isActive, cancellationToken);
        await WriteAuditAsync(connection, transaction, restaurantId, userId, "TABLE_QR_REGENERATED", tableId.ToString(), new { TableId = tableId, Token = token }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return qrService.BuildTableResponse(tableId, number, name, token, isActive, restaurant.Slug);
    }

    private async Task EnsureTableNumberAvailableAsync(Guid restaurantId, int tableNumber, Guid? ignoreTableId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT COUNT(*) FROM restaurant_tables WHERE restaurant_id = @restaurantId AND table_number = @tableNumber AND (@ignoreTableId IS NULL OR id <> @ignoreTableId);";
        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("restaurantId", restaurantId);
        command.Parameters.AddWithValue("tableNumber", tableNumber);
        command.Parameters.AddWithValue("ignoreTableId", ignoreTableId is null ? DBNull.Value : ignoreTableId.Value);
        if ((long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L) > 0) throw new InvalidOperationException("Table number already exists.");
    }

    private static async Task WriteAuditAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid restaurantId, Guid userId, string actionType, string entityId, object payload, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("INSERT INTO audit_logs (restaurant_id, user_id, action_type, entity_name, entity_id, payload) VALUES (@restaurantId, @userId, @actionType, 'restaurant_table', @entityId, CAST(@payload AS jsonb));", connection, transaction);
        command.Parameters.AddWithValue("restaurantId", restaurantId);
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("actionType", actionType);
        command.Parameters.AddWithValue("entityId", entityId);
        command.Parameters.AddWithValue("payload", JsonSerializer.Serialize(payload));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string DbValueOrEmpty(NpgsqlDataReader reader, string columnName) => reader.IsDBNull(reader.GetOrdinal(columnName)) ? string.Empty : reader.GetString(reader.GetOrdinal(columnName));
}

sealed class QrCodeService(AppDb appDb)
{
    public async Task<QrCodeCardResponse> GetGeneralQrAsync(Guid restaurantId, CancellationToken cancellationToken)
    {
        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        const string sql = "SELECT q.id, q.token, q.is_active, r.slug FROM qr_codes q INNER JOIN restaurants r ON r.id = q.restaurant_id WHERE q.restaurant_id = @restaurantId AND q.type = 'GENERAL' AND q.is_active = TRUE ORDER BY q.created_at DESC LIMIT 1;";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("restaurantId", restaurantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return BuildGeneralResponse(reader.GetGuid(0), reader.GetString(1), reader.GetBoolean(2), reader.GetString(3));
        }
        await reader.CloseAsync();
        return await RegenerateGeneralQrAsync(restaurantId, Guid.Empty, cancellationToken);
    }

    public async Task<QrCodeCardResponse> RegenerateGeneralQrAsync(Guid restaurantId, Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var deactivate = new NpgsqlCommand("UPDATE qr_codes SET is_active = FALSE, revoked_at = NOW(), updated_at = NOW() WHERE restaurant_id = @restaurantId AND type = 'GENERAL';", connection, transaction))
        {
            deactivate.Parameters.AddWithValue("restaurantId", restaurantId);
            await deactivate.ExecuteNonQueryAsync(cancellationToken);
        }
        var restaurant = await GetRestaurantSummaryAsync(connection, restaurantId, cancellationToken, transaction);
        var token = CreateToken();
        var card = await InsertQrAsync(connection, transaction, restaurant.Id, null, "GENERAL", token, BuildPath(restaurant.Slug, token, null, string.Empty), true, cancellationToken);
        if (userId != Guid.Empty)
        {
            await using var audit = new NpgsqlCommand("INSERT INTO audit_logs (restaurant_id, user_id, action_type, entity_name, entity_id, payload) VALUES (@restaurantId, @userId, 'GENERAL_QR_REGENERATED', 'qr_code', @entityId, CAST(@payload AS jsonb));", connection, transaction);
            audit.Parameters.AddWithValue("restaurantId", restaurantId);
            audit.Parameters.AddWithValue("userId", userId);
            audit.Parameters.AddWithValue("entityId", card.Id.ToString());
            audit.Parameters.AddWithValue("payload", JsonSerializer.Serialize(card));
            await audit.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return BuildGeneralResponse(card.Id, token, true, restaurant.Slug);
    }

    public async Task<IReadOnlyList<QrCodeCardResponse>> GetPrintableCardsAsync(Guid restaurantId, CancellationToken cancellationToken)
    {
        var cards = new List<QrCodeCardResponse> { await GetGeneralQrAsync(restaurantId, cancellationToken) };
        await using var connection = await appDb.OpenConnectionAsync(cancellationToken);
        var restaurant = await GetRestaurantSummaryAsync(connection, restaurantId, cancellationToken);
        await using var command = new NpgsqlCommand("SELECT id, table_number, table_name, qr_token, is_active FROM restaurant_tables WHERE restaurant_id = @restaurantId ORDER BY table_number, table_name;", connection);
        command.Parameters.AddWithValue("restaurantId", restaurantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            cards.Add(BuildTableCardResponse(reader.GetGuid(0), reader.GetInt32(1), reader.IsDBNull(2) ? string.Empty : reader.GetString(2), reader.GetString(3), reader.GetBoolean(4), restaurant.Slug));
        }
        return cards;
    }

    public async Task<RestaurantSlugSummary> GetRestaurantSummaryAsync(NpgsqlConnection connection, Guid restaurantId, CancellationToken cancellationToken, NpgsqlTransaction? transaction = null)
    {
        await using var command = new NpgsqlCommand("SELECT id, slug, name FROM restaurants WHERE id = @restaurantId LIMIT 1;", connection, transaction);
        command.Parameters.AddWithValue("restaurantId", restaurantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Restaurant could not be found.");
        return new RestaurantSlugSummary(reader.GetGuid(0), reader.GetString(1), reader.GetString(2));
    }

    public async Task ReplaceTableQrAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, RestaurantSlugSummary restaurant, Guid tableId, int tableNumber, string tableName, string token, bool isActive, CancellationToken cancellationToken)
    {
        await using (var deactivate = new NpgsqlCommand("UPDATE qr_codes SET is_active = FALSE, revoked_at = NOW(), updated_at = NOW() WHERE restaurant_id = @restaurantId AND table_id = @tableId AND type = 'TABLE';", connection, transaction))
        {
            deactivate.Parameters.AddWithValue("restaurantId", restaurant.Id);
            deactivate.Parameters.AddWithValue("tableId", tableId);
            await deactivate.ExecuteNonQueryAsync(cancellationToken);
        }
        await InsertQrAsync(connection, transaction, restaurant.Id, tableId, "TABLE", token, BuildPath(restaurant.Slug, token, tableNumber, tableName), isActive, cancellationToken);
    }

    private static async Task<QrRow> InsertQrAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid restaurantId, Guid? tableId, string type, string token, string targetUrl, bool isActive, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        await using var command = new NpgsqlCommand("INSERT INTO qr_codes (id, restaurant_id, table_id, type, token, target_url, is_active) VALUES (@id, @restaurantId, @tableId, @type, @token, @targetUrl, @isActive);", connection, transaction);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("restaurantId", restaurantId);
        command.Parameters.AddWithValue("tableId", tableId is null ? DBNull.Value : tableId.Value);
        command.Parameters.AddWithValue("type", type);
        command.Parameters.AddWithValue("token", token);
        command.Parameters.AddWithValue("targetUrl", targetUrl);
        command.Parameters.AddWithValue("isActive", isActive);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return new QrRow(id, token, isActive);
    }

    public RestaurantTableResponse BuildTableResponse(Guid id, int tableNumber, string tableName, string token, bool isActive, string slug) => new()
    {
        Id = id,
        TableNumber = tableNumber,
        TableName = tableName,
        QrToken = token,
        IsActive = isActive,
        TargetUrl = BuildPath(slug, token, tableNumber, tableName),
        QrImageUrl = BuildQrImageUrl(BuildAbsoluteUrl(slug, token, tableNumber, tableName))
    };

    private QrCodeCardResponse BuildTableCardResponse(Guid id, int tableNumber, string tableName, string token, bool isActive, string slug) => new()
    {
        Id = id,
        Type = "TABLE",
        Label = string.IsNullOrWhiteSpace(tableName) ? $"Masa {tableNumber}" : $"Masa {tableNumber} · {tableName}",
        Token = token,
        IsActive = isActive,
        TargetUrl = BuildPath(slug, token, tableNumber, tableName),
        QrImageUrl = BuildQrImageUrl(BuildAbsoluteUrl(slug, token, tableNumber, tableName))
    };

    private QrCodeCardResponse BuildGeneralResponse(Guid id, string token, bool isActive, string slug) => new()
    {
        Id = id,
        Type = "GENERAL",
        Label = "Genel Menü QR",
        Token = token,
        IsActive = isActive,
        TargetUrl = BuildPath(slug, token, null, string.Empty),
        QrImageUrl = BuildQrImageUrl(BuildAbsoluteUrl(slug, token, null, string.Empty))
    };

    private static string BuildPath(string slug, string token, int? tableNumber, string tableName)
    {
        var parts = new List<string> { $"qr={Uri.EscapeDataString(token)}" };
        if (tableNumber.HasValue) parts.Add($"table={tableNumber.Value}");
        if (!string.IsNullOrWhiteSpace(tableName)) parts.Add($"label={Uri.EscapeDataString(tableName)}");
        return $"/menu/{slug}?{string.Join("&", parts)}";
    }

    private static string BuildAbsoluteUrl(string slug, string token, int? tableNumber, string tableName) => $"http://127.0.0.1:5173{BuildPath(slug, token, tableNumber, tableName)}";
    private static string BuildQrImageUrl(string absoluteUrl) => $"https://api.qrserver.com/v1/create-qr-code/?size=512x512&data={Uri.EscapeDataString(absoluteUrl)}";
    public static string CreateToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}

sealed record RestaurantSlugSummary(Guid Id, string Slug, string Name);
sealed record QrRow(Guid Id, string Token, bool IsActive);

sealed class UpsertRestaurantTableRequest
{
    [Range(1, 500)]
    public int TableNumber { get; init; }

    [MaxLength(120)]
    public string TableName { get; init; } = string.Empty;

    public bool IsActive { get; init; } = true;
}

sealed class RestaurantTableResponse
{
    public Guid Id { get; init; }
    public int TableNumber { get; init; }
    public string TableName { get; init; } = string.Empty;
    public string QrToken { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public string TargetUrl { get; init; } = string.Empty;
    public string QrImageUrl { get; init; } = string.Empty;
}

sealed class QrCodeCardResponse
{
    public Guid Id { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public string TargetUrl { get; init; } = string.Empty;
    public string QrImageUrl { get; init; } = string.Empty;
}
