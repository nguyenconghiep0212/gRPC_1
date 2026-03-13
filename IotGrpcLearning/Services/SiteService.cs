using IotGrpcLearning.Infrastructure;
using IotGrpcLearning.Interfaces;
using IotGrpcLearning.Models;
using Microsoft.Data.Sqlite;

namespace IotGrpcLearning.Services;

public class SiteService : ISite
{
    private readonly ISqliteConnectionFactory _dbFactory;
    
    public SiteService(ISqliteConnectionFactory dbFactory)
    {
        _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
    }

    public async Task<SitesDto> CreateAsync(SitesDto dto, CancellationToken ct = default)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));

        using var conn = _dbFactory.CreateConnection();
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO Sites (name, location, address) " +
            "VALUES (@name, @location, @address); " +
            "SELECT last_insert_rowid();";

        cmd.Parameters.AddWithValue("@name", dto.Name ?? string.Empty);
        cmd.Parameters.AddWithValue("@location", dto.Location ?? string.Empty);
        cmd.Parameters.AddWithValue("@address", dto.Address ?? string.Empty);
        
        var result = await cmd.ExecuteScalarAsync(ct);
        var newId = Convert.ToInt32(result);

        return new SitesDto(newId, dto.Name ?? string.Empty, dto.Location ?? string.Empty, dto.Address ?? string.Empty);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        using var conn = _dbFactory.CreateConnection();
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Sites WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    public async Task<IEnumerable<SitesDto>> GetAllAsync(PaginationDto body, CancellationToken ct = default)
    {
        using var conn = _dbFactory.CreateConnection();
        await conn.OpenAsync(ct);
        
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, location, address FROM Sites LIMIT @limit OFFSET @offset;";
        cmd.Parameters.AddWithValue("@limit", body.limit);
        cmd.Parameters.AddWithValue("@offset", body.offset);
        
        var sites = new List<SitesDto>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetInt32(0);
            var name = reader.GetString(1);
            var location = reader.GetString(2);
            var address = reader.GetString(3);
            sites.Add(new SitesDto(id, name, location, address));
        }
        
        return sites;
    }

    public async Task<SitesDto?> GetAsync(int id, CancellationToken ct = default)
    {
        using var conn = _dbFactory.CreateConnection();
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, location, address FROM Sites WHERE id = @id LIMIT 1;";
        cmd.Parameters.AddWithValue("@id", id);

        using var rdr = await cmd.ExecuteReaderAsync(ct);
        if (await rdr.ReadAsync(ct))
        {
            int site_id = rdr.GetInt32(0);
            string site_name = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1);
            string site_location = rdr.IsDBNull(2) ? string.Empty : rdr.GetString(2);
            string site_address = rdr.IsDBNull(3) ? string.Empty : rdr.GetString(3);
            
            return new SitesDto(site_id, site_name, site_location, site_address);
        }

        return null;
    }

    public async Task<bool> UpdateAsync(int id, SitesDto dto, CancellationToken ct = default)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));

        using var conn = _dbFactory.CreateConnection();
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "UPDATE Sites SET name = @name, location = @location, address = @address WHERE id = @id;";

        cmd.Parameters.AddWithValue("@name", dto.Name ?? string.Empty);
        cmd.Parameters.AddWithValue("@location", dto.Location ?? string.Empty);
        cmd.Parameters.AddWithValue("@address", dto.Address ?? string.Empty);
        cmd.Parameters.AddWithValue("@id", id);

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }
}
