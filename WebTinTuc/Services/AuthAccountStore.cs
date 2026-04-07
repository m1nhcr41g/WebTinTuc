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
                created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            );";

        await command.ExecuteNonQueryAsync();
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
            SELECT id, full_name, email, password_hash, role
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
            Role = reader.GetString(4)
        };

        return PasswordHasher.VerifyPassword(password, account.PasswordHash) ? account : null;
    }
    // ================= USER MANAGEMENT =================

    // 📋 Lấy theo role
    public async Task<List<AuthAccount>> GetUsersByRole(string role)
    {
        var list = new List<AuthAccount>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
        SELECT id, full_name, email, password_hash, role
        FROM auth_accounts
        WHERE role = $role";

        command.Parameters.AddWithValue("$role", role);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            list.Add(new AuthAccount
            {
                Id = reader.GetInt64(0),
                FullName = reader.GetString(1),
                Email = reader.GetString(2),
                PasswordHash = reader.GetString(3),
                Role = reader.GetString(4)
            });
        }

        return list;
    }
    // 📊 Đếm theo role
    public async Task<int> CountByRole(string role)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM auth_accounts WHERE role = $role";
        command.Parameters.AddWithValue("$role", role);

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
    // ❌ Xóa user
    public async Task DeleteUser(long id)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM auth_accounts WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);

        await command.ExecuteNonQueryAsync();
    }
    // 📋 Lấy tất cả user
    public async Task<List<AuthAccount>> GetAllUsers()
    {
        var list = new List<AuthAccount>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, full_name, email, password_hash, role FROM auth_accounts";

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            list.Add(new AuthAccount
            {
                Id = reader.GetInt64(0),
                FullName = reader.GetString(1),
                Email = reader.GetString(2),
                PasswordHash = reader.GetString(3),
                Role = reader.GetString(4)
            });
        }

        return list;
    }
}
