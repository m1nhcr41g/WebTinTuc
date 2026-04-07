using Microsoft.Data.Sqlite;
using WebTinTuc.Models.Auth;
using System.Security.Cryptography;
using System.Text;

namespace WebTinTuc.Services;

public class AdminAuthService
{
    private readonly string _connectionString;

    public AdminAuthService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection");
    }

    public async Task InitAsync()
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        CREATE TABLE IF NOT EXISTS AdminAccounts (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Email TEXT UNIQUE,
            PasswordHash TEXT
        )";
        await cmd.ExecuteNonQueryAsync();
    }

    public string HashPassword(string password)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }

    public async Task<bool> Register(string email, string password)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

   
        var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM AdminAccounts WHERE Email = $e";
        checkCmd.Parameters.AddWithValue("$e", email);

        var count = (long)await checkCmd.ExecuteScalarAsync();
        if (count > 0)
            return false; 

      
        var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO AdminAccounts (Email, PasswordHash) VALUES ($e, $p)";
        cmd.Parameters.AddWithValue("$e", email);
        cmd.Parameters.AddWithValue("$p", HashPassword(password));

        await cmd.ExecuteNonQueryAsync();
        return true;
    }

    public async Task<bool> Login(string email, string password)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT PasswordHash FROM AdminAccounts WHERE Email = $e";
        cmd.Parameters.AddWithValue("$e", email);

        var result = await cmd.ExecuteScalarAsync();

        if (result == null) return false;

        return result.ToString() == HashPassword(password);
    }
}