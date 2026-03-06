using IotGrpcLearning.Infrastructure;
using IotGrpcLearning.Interfaces;
using IotGrpcLearning.Models;
using IotGrpcLearning.Proto;
using Microsoft.Data.Sqlite;
using System.Reflection.PortableExecutable;
using System.Text;

namespace IotGrpcLearning.Services
{
	public sealed class MachineService : IMachineService
	{
		private readonly ISqliteConnectionFactory _dbFactory;
		private readonly Helper _helper;
		public MachineService(ISqliteConnectionFactory dbFactory)
		{
			_helper = new Helper();
			_dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
		}

		public async Task<MachineDto> CreateAsync(MachineDto dto, CancellationToken ct = default)
		{
			if (dto == null) throw new ArgumentNullException(nameof(dto));

			using var conn = _dbFactory.CreateConnection();
			await conn.OpenAsync(ct);

			using var cmd = conn.CreateCommand();
			cmd.CommandText =
				"INSERT INTO Machines (  name, alias, details, vendor, purchase_price, purchase_date, site) " +
				"VALUES ( @name, @alias, @details, @vendor, @price, @pdate, @site); " +
				"SELECT last_insert_rowid();";

			cmd.Parameters.AddWithValue("@name", dto.Name ?? string.Empty);
			cmd.Parameters.AddWithValue("@alias", dto.Alias ?? string.Empty);
			cmd.Parameters.AddWithValue("@details", dto.Details ?? string.Empty);
			cmd.Parameters.AddWithValue("@vendor", dto.Vendor);
			cmd.Parameters.AddWithValue("@price", dto.PurchasePrice);

			if (dto.PurchaseDate == DateTime.MinValue)
				cmd.Parameters.AddWithValue("@pdate", DBNull.Value);
			else
				cmd.Parameters.AddWithValue("@pdate", dto.PurchaseDate.ToString("o"));

			cmd.Parameters.AddWithValue("@site", dto.Site);

			var result = await cmd.ExecuteScalarAsync(ct);
			var newId = Convert.ToInt32(result);

			return new MachineDto(newId, dto.Name ?? string.Empty, dto.Alias ?? string.Empty, dto.Details ?? string.Empty,
				dto.Vendor, dto.PurchasePrice, dto.PurchaseDate, dto.Site);

		}

		public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
		{


			using var conn = _dbFactory.CreateConnection();
			await conn.OpenAsync(ct);

			using var cmd = conn.CreateCommand();
			cmd.CommandText = "DELETE FROM Machines WHERE id = @id;";
			cmd.Parameters.AddWithValue("@id", id);

			var rows = await cmd.ExecuteNonQueryAsync(ct);
			return rows > 0;
		}

		public async Task<ListDto<MachineResponse>> GetAllAsync(PaginationDto body, CancellationToken ct = default)
		{
			var list = new List<MachineResponse>();
			string tableName = "Machines";
			using var conn = _dbFactory.CreateConnection();
			await conn.OpenAsync(ct);

			using var cmd = conn.CreateCommand();

			// Start building the base query
			var queryBuilder = new StringBuilder($"SELECT id, name, alias, details, vendor, purchase_price, purchase_date, site FROM {tableName}");
			if (body.filters != null)
			{
				// Call the BuildFilterQuery method
				var (filterQuery, parameters) = _helper.BuildFilterQuery(tableName, body.filters);
				if (!string.IsNullOrEmpty(filterQuery))
				{
					queryBuilder.Append(filterQuery);
				}
				if (parameters.Count > 0)
				{
					foreach (var parameter in parameters)
					{
						cmd.Parameters.Add(parameter);
					}
				}
			}
			// Adding pagination and ordering
			queryBuilder.Append($" ORDER BY id LIMIT {body.limit} OFFSET {body.offset};");

			cmd.CommandText = queryBuilder.ToString();
			Console.WriteLine($"Total parameters set: {cmd.Parameters.Count}");

			using var rdr = await cmd.ExecuteReaderAsync(ct);

			while (await rdr.ReadAsync(ct))
			{
				MachineDto machine = ReadMachine(rdr);

				string vendorName = await _helper.GetPropertyTableAsync(conn, ct, "Vendors", "id", machine.Vendor.ToString(), "name") ?? string.Empty;
				string siteName = await _helper.GetPropertyTableAsync(conn, ct, "Sites", "id", machine.Site.ToString(), "name") ?? string.Empty;

				MachineResponse machineResponse = new MachineResponse(
				machine.Id,
				machine.Name,
				machine.Alias,
				machine.Details,
				machine.Vendor,
				vendorName,
				machine.PurchasePrice,
				machine.PurchaseDate,
				machine.Site,
				siteName
				);

				list.Add(machineResponse);
			}
			int total = await _helper.GetTotalCountWithConditions(conn, ct, tableName, body.filters);

			ListDto<MachineResponse> result = new ListDto<MachineResponse>(list, total);

			return result;
		}

		public async Task<MachineResponse?> GetAsync(int id, CancellationToken ct = default)
		{

			using var conn = _dbFactory.CreateConnection();
			await conn.OpenAsync(ct);

			using var cmd = conn.CreateCommand();
			cmd.CommandText = "SELECT id, name, alias, details, vendor, purchase_price, purchase_date, site FROM Machines WHERE id = @id LIMIT 1;";
			cmd.Parameters.AddWithValue("@id", id);

			using var rdr = await cmd.ExecuteReaderAsync(ct);
			if (await rdr.ReadAsync(ct))
			{
				MachineDto machine = ReadMachine(rdr);

				string vendorName = await _helper.GetPropertyTableAsync(conn, ct, "Vendors", "id", machine.Vendor.ToString(), "name") ?? string.Empty;
				string siteName = await _helper.GetPropertyTableAsync(conn, ct, "Sites", "id", machine.Site.ToString(), "name") ?? string.Empty;

				MachineResponse machineResponse = new MachineResponse(
				machine.Id,
				machine.Name,
				machine.Alias,
				machine.Details,
				machine.Vendor,
				vendorName,
				machine.PurchasePrice,
				machine.PurchaseDate,
				machine.Site,
				siteName
				);

				return machineResponse;
			}

			return null;
		}

		public async Task<bool> UpdateAsync(int id, MachineDto dto, CancellationToken ct = default)
		{

			if (dto == null) throw new ArgumentNullException(nameof(dto));

			using var conn = _dbFactory.CreateConnection();
			await conn.OpenAsync(ct);

			using var cmd = conn.CreateCommand();
			cmd.CommandText =
				"UPDATE Machines SET name = @name, alias = @alias, details = @details, vendor = @vendor, " +
				"purchase_price = @price, purchase_date = @pdate, site = @site WHERE id = @id;";

			cmd.Parameters.AddWithValue("@name", dto.Name ?? string.Empty);
			cmd.Parameters.AddWithValue("@alias", dto.Alias ?? string.Empty);
			cmd.Parameters.AddWithValue("@details", dto.Details ?? string.Empty);
			cmd.Parameters.AddWithValue("@vendor", dto.Vendor);
			cmd.Parameters.AddWithValue("@price", dto.PurchasePrice);

			if (dto.PurchaseDate == DateTime.MinValue)
				cmd.Parameters.AddWithValue("@pdate", DBNull.Value);
			else
				cmd.Parameters.AddWithValue("@pdate", dto.PurchaseDate.ToString("o"));

			cmd.Parameters.AddWithValue("@site", dto.Site);
			cmd.Parameters.AddWithValue("@id", id);

			var rows = await cmd.ExecuteNonQueryAsync(ct);
			return rows > 0;
		}

		#region ===== HELPER ======
		private static MachineDto ReadMachine(SqliteDataReader rdr)
		{
			static T GetSafe<T>(SqliteDataReader r, int i, Func<object, T> conv, T @default = default!)
			{
				if (r.IsDBNull(i)) return @default!;
				return conv(r.GetValue(i));
			}

			int id = GetSafe(rdr, 0, o => Convert.ToInt32(o));
			string name = GetSafe(rdr, 1, o => Convert.ToString(o) ?? string.Empty, string.Empty);
			string alias = GetSafe(rdr, 2, o => Convert.ToString(o) ?? string.Empty, string.Empty);
			string details = GetSafe(rdr, 3, o => Convert.ToString(o) ?? string.Empty, string.Empty);
			int vendor = GetSafe(rdr, 4, o => Convert.ToInt32(o), 0);
			double price = GetSafe(rdr, 5, o => Convert.ToDouble(o), 0.0);

			// purchase_date might be stored as TEXT or numeric; attempt to parse
			DateTime purchaseDate;
			if (!rdr.IsDBNull(6))
			{
				var val = rdr.GetValue(6);
				if (val is DateTime dt) purchaseDate = dt;
				else if (DateTime.TryParse(Convert.ToString(val), out var parsed)) purchaseDate = parsed;
				else purchaseDate = DateTime.MinValue;
			}
			else
			{
				purchaseDate = DateTime.MinValue;
			}

			int site = GetSafe(rdr, 7, o => Convert.ToInt32(o), 0);

			return new MachineDto(id, name, alias, details, vendor, price, purchaseDate, site);
		}
		#endregion
	}
}
