using Microsoft.Data.Sqlite;
using WebTinTuc.Models.Auth;

namespace WebTinTuc.Services;

public class AuthAccountStore
{
    private readonly string _connectionString;

    public AuthAccountStore(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");
    }

    public async Task EnsureSchemaAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS auth_accounts (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                full_name TEXT NOT NULL,
                email TEXT NOT NULL UNIQUE,
                password_hash TEXT NOT NULL,
                role TEXT NOT NULL CHECK(role IN ('User', 'Journalist')),
                avatar_path TEXT NULL,
                created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            );";

        await command.ExecuteNonQueryAsync();

        await using var avatarColumnCheck = connection.CreateCommand();
        avatarColumnCheck.CommandText = @"
            SELECT COUNT(1)
            FROM pragma_table_info('auth_accounts')
            WHERE name = 'avatar_path';";

        var hasAvatarColumn = Convert.ToInt64(await avatarColumnCheck.ExecuteScalarAsync()) > 0;
        if (!hasAvatarColumn)
        {
            await using var addAvatarColumn = connection.CreateCommand();
            addAvatarColumn.CommandText = "ALTER TABLE auth_accounts ADD COLUMN avatar_path TEXT NULL;";
            await addAvatarColumn.ExecuteNonQueryAsync();
        }
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM auth_accounts WHERE lower(email) = lower($email) LIMIT 1;";
        command.Parameters.AddWithValue("$email", email.Trim());

        var result = await command.ExecuteScalarAsync();
        return result is not null;
    }

    public async Task<long> CreateAsync(RegisterViewModel model)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO auth_accounts (full_name, email, password_hash, role)
            VALUES ($fullName, $email, $passwordHash, $role);
            SELECT last_insert_rowid();";

        command.Parameters.AddWithValue("$fullName", model.FullName.Trim());
        command.Parameters.AddWithValue("$email", model.Email.Trim());
        command.Parameters.AddWithValue("$passwordHash", PasswordHasher.HashPassword(model.Password));
        command.Parameters.AddWithValue("$role", model.Role);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt64(result);
    }

    public async Task<AuthAccount?> ValidateCredentialsAsync(string email, string password)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, full_name, email, password_hash, role, avatar_path
            FROM auth_accounts
            WHERE lower(email) = lower($email)
            LIMIT 1;";
        command.Parameters.AddWithValue("$email", email.Trim());

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        var account = new AuthAccount
        {
            Id = reader.GetInt64(0),
            FullName = reader.GetString(1),
            Email = reader.GetString(2),
            PasswordHash = reader.GetString(3),
            Role = reader.GetString(4),
            AvatarPath = reader.IsDBNull(5) ? null : reader.GetString(5)
        };

        return PasswordHasher.VerifyPassword(password, account.PasswordHash) ? account : null;
    }

    public async Task<AuthAccount?> GetByIdAsync(long accountId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, full_name, email, password_hash, role, avatar_path
            FROM auth_accounts
            WHERE id = $id
            LIMIT 1;";
        command.Parameters.AddWithValue("$id", accountId);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new AuthAccount
        {
            Id = reader.GetInt64(0),
            FullName = reader.GetString(1),
            Email = reader.GetString(2),
            PasswordHash = reader.GetString(3),
            Role = reader.GetString(4),
            AvatarPath = reader.IsDBNull(5) ? null : reader.GetString(5)
        };
    }

    public async Task<bool> EmailExistsForOtherAccountAsync(string email, long accountId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 1
            FROM auth_accounts
            WHERE lower(email) = lower($email) AND id <> $id
            LIMIT 1;";
        command.Parameters.AddWithValue("$email", email.Trim());
        command.Parameters.AddWithValue("$id", accountId);

        var result = await command.ExecuteScalarAsync();
        return result is not null;
    }

    public async Task<bool> UpdateAccountAsync(long accountId, string fullName, string email, string? avatarPath, string? newPasswordHash = null)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        if (string.IsNullOrWhiteSpace(newPasswordHash))
        {
            command.CommandText = @"
                UPDATE auth_accounts
                SET full_name = $fullName,
                    email = $email,
                    avatar_path = $avatarPath
                WHERE id = $id;";
        }
        else
        {
            command.CommandText = @"
                UPDATE auth_accounts
                SET full_name = $fullName,
                    email = $email,
                    avatar_path = $avatarPath,
                    password_hash = $passwordHash
                WHERE id = $id;";
            command.Parameters.AddWithValue("$passwordHash", newPasswordHash);
        }

        command.Parameters.AddWithValue("$fullName", fullName.Trim());
        command.Parameters.AddWithValue("$email", email.Trim());
        command.Parameters.AddWithValue("$avatarPath", (object?)avatarPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$id", accountId);

        var affected = await command.ExecuteNonQueryAsync();
        return affected > 0;
    }
}
