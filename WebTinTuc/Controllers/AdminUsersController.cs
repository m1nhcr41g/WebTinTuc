using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using WebTinTuc.Models.Auth;
using WebTinTuc.Services;

namespace WebTinTuc.Controllers;

[Authorize(AuthenticationSchemes = "AdminScheme")]
public class AdminUsersController : Controller
{
    private readonly string _connectionString;
    private readonly AuthAccountStore _store;

    public AdminUsersController(AuthAccountStore store)
    {
        _store = store;
    }

    // 📊 Trang quản lý
    public async Task<IActionResult> Index()
    {
        ViewBag.UserCount = await _store.CountByRole("User");
        ViewBag.JournalistCount = await _store.CountByRole("Journalist");

        ViewBag.Users = await _store.GetUsersByRole("User");
        ViewBag.Journalists = await _store.GetUsersByRole("Journalist");

        return View();
    }

    // ❌ Xóa user
    public async Task<IActionResult> Delete(long id)
    {
        var currentEmail = User.Identity?.Name;

        var users = await _store.GetAllUsers();
        var user = users.FirstOrDefault(x => x.Id == id);

        if (user != null && user.Email == currentEmail)
        {
            TempData["Error"] = "Không thể xóa chính mình!";
            return RedirectToAction("Index");
        }

        await _store.DeleteUser(id);
        return RedirectToAction("Index");
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