using Microsoft.Data.Sqlite;
using WebTinTuc.Models.Preferences;

namespace WebTinTuc.Services.Preferences;

public class UserPreferenceStore
{
    private readonly string _connectionString;

    public UserPreferenceStore(IConfiguration configuration)
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
            CREATE TABLE IF NOT EXISTS favorite_teams (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE
            );

            CREATE TABLE IF NOT EXISTS favorite_leagues (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE
            );

            CREATE TABLE IF NOT EXISTS user_favorite_teams (
                account_id INTEGER NOT NULL,
                team_id INTEGER NOT NULL,
                PRIMARY KEY (account_id, team_id),
                FOREIGN KEY(account_id) REFERENCES auth_accounts(id) ON DELETE CASCADE,
                FOREIGN KEY(team_id) REFERENCES favorite_teams(id)
            );

            CREATE TABLE IF NOT EXISTS user_favorite_leagues (
                account_id INTEGER NOT NULL,
                league_id INTEGER NOT NULL,
                PRIMARY KEY (account_id, league_id),
                FOREIGN KEY(account_id) REFERENCES auth_accounts(id) ON DELETE CASCADE,
                FOREIGN KEY(league_id) REFERENCES favorite_leagues(id)
            );";
        await command.ExecuteNonQueryAsync();

        await SeedDefaultsAsync(connection);
    }

    public async Task<List<PreferenceOption>> GetTeamOptionsAsync()
    {
        var result = new List<PreferenceOption>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name FROM favorite_teams ORDER BY name;";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new PreferenceOption
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1)
            });
        }

        return result;
    }

    public async Task<List<PreferenceOption>> GetLeagueOptionsAsync()
    {
        var result = new List<PreferenceOption>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name FROM favorite_leagues ORDER BY name;";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new PreferenceOption
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1)
            });
        }

        return result;
    }

    public async Task SaveUserPreferencesAsync(long accountId, IReadOnlyCollection<long> teamIds, IReadOnlyCollection<long> leagueIds)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();

        await using (var deleteTeams = connection.CreateCommand())
        {
            deleteTeams.Transaction = transaction;
            deleteTeams.CommandText = "DELETE FROM user_favorite_teams WHERE account_id = $accountId;";
            deleteTeams.Parameters.AddWithValue("$accountId", accountId);
            await deleteTeams.ExecuteNonQueryAsync();
        }

        await using (var deleteLeagues = connection.CreateCommand())
        {
            deleteLeagues.Transaction = transaction;
            deleteLeagues.CommandText = "DELETE FROM user_favorite_leagues WHERE account_id = $accountId;";
            deleteLeagues.Parameters.AddWithValue("$accountId", accountId);
            await deleteLeagues.ExecuteNonQueryAsync();
        }

        foreach (var teamId in teamIds.Where(id => id > 0).Distinct())
        {
            await using var insertTeam = connection.CreateCommand();
            insertTeam.Transaction = transaction;
            insertTeam.CommandText = @"
                INSERT OR IGNORE INTO user_favorite_teams (account_id, team_id)
                VALUES ($accountId, $teamId);";
            insertTeam.Parameters.AddWithValue("$accountId", accountId);
            insertTeam.Parameters.AddWithValue("$teamId", teamId);
            await insertTeam.ExecuteNonQueryAsync();
        }

        foreach (var leagueId in leagueIds.Where(id => id > 0).Distinct())
        {
            await using var insertLeague = connection.CreateCommand();
            insertLeague.Transaction = transaction;
            insertLeague.CommandText = @"
                INSERT OR IGNORE INTO user_favorite_leagues (account_id, league_id)
                VALUES ($accountId, $leagueId);";
            insertLeague.Parameters.AddWithValue("$accountId", accountId);
            insertLeague.Parameters.AddWithValue("$leagueId", leagueId);
            await insertLeague.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    public async Task<(List<long> TeamIds, List<long> LeagueIds)> GetSelectedPreferenceIdsAsync(long accountId)
    {
        var teamIds = new List<long>();
        var leagueIds = new List<long>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using (var teamCommand = connection.CreateCommand())
        {
            teamCommand.CommandText = @"
                SELECT team_id
                FROM user_favorite_teams
                WHERE account_id = $accountId
                ORDER BY team_id;";
            teamCommand.Parameters.AddWithValue("$accountId", accountId);

            await using var reader = await teamCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                teamIds.Add(reader.GetInt64(0));
            }
        }

        await using (var leagueCommand = connection.CreateCommand())
        {
            leagueCommand.CommandText = @"
                SELECT league_id
                FROM user_favorite_leagues
                WHERE account_id = $accountId
                ORDER BY league_id;";
            leagueCommand.Parameters.AddWithValue("$accountId", accountId);

            await using var reader = await leagueCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                leagueIds.Add(reader.GetInt64(0));
            }
        }

        return (teamIds, leagueIds);
    }

    public async Task<List<string>> GetPreferenceKeywordsAsync(long accountId)
    {
        var result = new List<string>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using (var teamCommand = connection.CreateCommand())
        {
            teamCommand.CommandText = @"
                SELECT t.name
                FROM user_favorite_teams uft
                JOIN favorite_teams t ON t.id = uft.team_id
                WHERE uft.account_id = $accountId
                ORDER BY t.name;";
            teamCommand.Parameters.AddWithValue("$accountId", accountId);

            await using var reader = await teamCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(reader.GetString(0));
            }
        }

        await using (var leagueCommand = connection.CreateCommand())
        {
            leagueCommand.CommandText = @"
                SELECT l.name
                FROM user_favorite_leagues ufl
                JOIN favorite_leagues l ON l.id = ufl.league_id
                WHERE ufl.account_id = $accountId
                ORDER BY l.name;";
            leagueCommand.Parameters.AddWithValue("$accountId", accountId);

            await using var reader = await leagueCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(reader.GetString(0));
            }
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static async Task SeedDefaultsAsync(SqliteConnection connection)
    {
        var teams = new[]
        {
            "Manchester United", "Manchester City", "Liverpool", "Arsenal", "Chelsea",
            "Real Madrid", "Barcelona", "Atletico Madrid", "Bayern Munich", "PSG"
        };

        var leagues = new[]
        {
            "Premier League", "La Liga", "Serie A", "Bundesliga", "Ligue 1",
            "Champions League", "Europa League", "V.League 1"
        };

        foreach (var team in teams)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "INSERT OR IGNORE INTO favorite_teams (name) VALUES ($name);";
            command.Parameters.AddWithValue("$name", team);
            await command.ExecuteNonQueryAsync();
        }

        foreach (var league in leagues)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "INSERT OR IGNORE INTO favorite_leagues (name) VALUES ($name);";
            command.Parameters.AddWithValue("$name", league);
            await command.ExecuteNonQueryAsync();
        }
    }
}
