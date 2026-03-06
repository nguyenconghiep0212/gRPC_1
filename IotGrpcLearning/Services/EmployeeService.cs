using Grpc.Core;
using IotGrpcLearning.Infrastructure;
using IotGrpcLearning.Interfaces;
using IotGrpcLearning.Models;
using Microsoft.Data.Sqlite;
using System.Numerics;
using System.Xml.Linq;

namespace IotGrpcLearning.Services
{
	public sealed class EmployeeService : IEmployee
	{
		private readonly ISqliteConnectionFactory _dbFactory;
		private readonly Helper _helper;
		public EmployeeService(ISqliteConnectionFactory dbFactory)
		{
			_helper = new Helper();
			_dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
		}

		public async Task<EmployeesDto> CreateAsync(EmployeesDto dto, CancellationToken ct = default)
		{
			if (dto == null) throw new ArgumentNullException(nameof(dto));

			using var conn = _dbFactory.CreateConnection();
			await conn.OpenAsync(ct);

			using var cmd = conn.CreateCommand();
			cmd.CommandText =
				"INSERT INTO Employees (avatar_url, name, email, role_id, division_id, supervisor, site) " +
				"VALUES (@avatar_url, @name, @email, @role_id, @division_id, @supervisor, @site); " +
				"SELECT last_insert_rowid();";

			cmd.Parameters.AddWithValue("@name", dto.Name ?? string.Empty);
			var result = await cmd.ExecuteScalarAsync(ct);
			var newId = Convert.ToInt32(result);

			return new EmployeesDto(newId, dto.AvatarUrl ?? string.Empty, dto.Name ?? string.Empty, dto.Email ?? string.Empty, dto.RoleId, dto.DivisionId, dto.SupervisorId, dto.SiteId);
		}
		public async Task<ListDto<EmployeeResponse>> GetAllAsync(PaginationDto body, CancellationToken ct = default)
		{
			using var conn = _dbFactory.CreateConnection();
			await conn.OpenAsync(ct);
			using var cmd = conn.CreateCommand();

			var employees = new List<EmployeeResponse>();
			string tableName = "Employees";

			using var cmdCount = conn.CreateCommand();
			cmd.CommandText = $"SELECT id, avatar_url, name, email, role_id, division_id, supervisor, site FROM {tableName} LIMIT {body.limit} OFFSET {body.offset};";
			using var reader = await cmd.ExecuteReaderAsync(ct);
			while (await reader.ReadAsync(ct))
			{
				int id = reader.GetInt32(0);
				string avatar_url = reader.GetString(1);
				string name = reader.GetString(2);
				string email = reader.GetString(3);
				int role_id = reader.GetInt32(4);
				int division_id = reader.GetInt32(5);
				int? supervisor_id = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6);
				int site_id = reader.GetInt32(7);

				string role = await _helper.GetPropertyTableAsync(conn, ct, "Roles", "id", role_id.ToString(), "name") ?? string.Empty;
				string division = await _helper.GetPropertyTableAsync(conn, ct, "Divisions", "id", division_id.ToString(), "name") ?? string.Empty;
				string? supervisor = string.IsNullOrEmpty(supervisor_id.ToString()) ? string.Empty : await _helper.GetPropertyTableAsync(conn, ct, "Employees", "id", supervisor_id.ToString(), "name");
				string site = await _helper.GetPropertyTableAsync(conn, ct, "Sites", "id", site_id.ToString(), "name") ?? string.Empty;
				employees.Add(new EmployeeResponse(id, avatar_url, name, email, role_id, role, division_id, division, supervisor_id, supervisor, site_id, site));
			}
		 
			int total = await _helper.GetTotalCountFromTable(conn, ct, tableName);

			ListDto<EmployeeResponse> result = new ListDto<EmployeeResponse>(employees, total);
			return result;
		}

		public async Task<bool> UpdateAsync(int id, EmployeesDto dto, CancellationToken ct = default)
		{

			if (dto == null) throw new ArgumentNullException(nameof(dto));

			using var conn = _dbFactory.CreateConnection();
			await conn.OpenAsync(ct);

			using var cmd = conn.CreateCommand();
			cmd.CommandText =
				"UPDATE Employees SET avatar_url = @avatar_url, name = @name, email = @email, role_id = @role_id, division_id = @division_id, supervisor = @supervisor, site = @site WHERE id = @id;";

			cmd.Parameters.AddWithValue("@avatar_url", dto.AvatarUrl ?? string.Empty);
			cmd.Parameters.AddWithValue("@name", dto.Name ?? string.Empty);
			cmd.Parameters.AddWithValue("@email", dto.Email ?? string.Empty);
			cmd.Parameters.AddWithValue("@role_id", dto.RoleId);
			cmd.Parameters.AddWithValue("@division_id", dto.DivisionId);
			cmd.Parameters.AddWithValue("@supervisor", dto.SupervisorId);
			cmd.Parameters.AddWithValue("@site", dto.SiteId);
			cmd.Parameters.AddWithValue("@id", id);

			var rows = await cmd.ExecuteNonQueryAsync(ct);
			return rows > 0;
		}

		public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
		{


			using var conn = _dbFactory.CreateConnection();
			await conn.OpenAsync(ct);

			using var cmd = conn.CreateCommand();
			cmd.CommandText = "DELETE FROM Employees WHERE id = @id;";
			cmd.Parameters.AddWithValue("@id", id);

			var rows = await cmd.ExecuteNonQueryAsync(ct);
			return rows > 0;
		}

		public async Task<EmployeeResponse?> GetAsync(int id, CancellationToken ct = default)
		{

			using var conn = _dbFactory.CreateConnection();
			await conn.OpenAsync(ct);

			using var cmd = conn.CreateCommand();
			cmd.CommandText = "SELECT id, avatar_url, name, email, role_id, division_id, supervisor, site FROM Employees WHERE id = @id LIMIT 1;";
			cmd.Parameters.AddWithValue("@id", id);

			using var rdr = await cmd.ExecuteReaderAsync(ct);
			if (await rdr.ReadAsync(ct))
			{
				static T GetSafe<T>(SqliteDataReader r, int i, Func<object, T> conv, T @default = default!)
				{
					if (r.IsDBNull(i)) return @default!;
					return conv(r.GetValue(i));
				}
				int employee_id = GetSafe(rdr, 0, o => Convert.ToInt32(o));
				string employee_avatar_url = GetSafe(rdr, 1, o => Convert.ToString(o) ?? string.Empty, string.Empty);
				string employee_name = GetSafe(rdr, 2, o => Convert.ToString(o) ?? string.Empty, string.Empty);
				string employee_email = GetSafe(rdr, 3, o => Convert.ToString(o) ?? string.Empty, string.Empty);
				int employee_role_id = GetSafe(rdr, 4, o => Convert.ToInt32(o));
				int employee_division_id = GetSafe(rdr, 5, o => Convert.ToInt32(o));
				int employee_supervisor_id = GetSafe(rdr, 6, o => Convert.ToInt32(o));
				int employee_site_id = GetSafe(rdr, 7, o => Convert.ToInt32(o));

				string role = await _helper.GetPropertyTableAsync(conn, ct, "Roles", "id", employee_role_id.ToString(), "name") ?? string.Empty;
				string division = await _helper.GetPropertyTableAsync(conn, ct, "Divisions", "id", employee_division_id.ToString(), "name") ?? string.Empty;
				string? supervisor = string.IsNullOrEmpty(employee_supervisor_id.ToString()) ? string.Empty : await _helper.GetPropertyTableAsync(conn, ct, "Employees", "id", employee_supervisor_id.ToString(), "name");
				string site = await _helper.GetPropertyTableAsync(conn, ct, "Sites", "id", employee_site_id.ToString(), "name") ?? string.Empty;
				return new EmployeeResponse(employee_id, employee_avatar_url, employee_name, employee_email, employee_role_id, role, employee_division_id, division, employee_supervisor_id, supervisor, employee_site_id, site);
			}

			return null;
		}
	}
}
