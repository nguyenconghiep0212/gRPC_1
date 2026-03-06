using Grpc.Core;
using IotGrpcLearning.Infrastructure;
using IotGrpcLearning.Interfaces;
using IotGrpcLearning.Models;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Numerics;
using System.Xml.Linq;

namespace IotGrpcLearning.Services
{
	public sealed class VendorService : IVendor
	{
		private readonly ISqliteConnectionFactory _dbFactory;
		private readonly Helper _helper;
		public VendorService(ISqliteConnectionFactory dbFactory)
		{
			_helper	= new Helper();
			_dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
		}

		public async Task<VendorDto> CreateAsync(VendorDto dto, CancellationToken ct = default)
		{
			if (dto == null) throw new ArgumentNullException(nameof(dto));

			using var conn = _dbFactory.CreateConnection();
			await conn.OpenAsync(ct);

			using var cmd = conn.CreateCommand();
			cmd.CommandText =
				"INSERT INTO Vendors (name) " +
				"VALUES (@name); " +
				"SELECT last_insert_rowid();";

			cmd.Parameters.AddWithValue("@name", dto.Name ?? string.Empty);
			var result = await cmd.ExecuteScalarAsync(ct);
			var newId = Convert.ToInt32(result);

			return new VendorDto(newId, dto.Name ?? string.Empty);
		}
		public async Task<ListDto<VendorDto>> GetAllAsync(PaginationDto body, CancellationToken ct = default)
		{
			using var conn = _dbFactory.CreateConnection();
			await conn.OpenAsync(ct);
			using var cmd = conn.CreateCommand();

			var list = new List<VendorDto>();
			string tableName = "Vendors";

			cmd.CommandText = $"SELECT id, name FROM {tableName} LIMIT {body.limit} OFFSET {body.offset};";
			using var reader = await cmd.ExecuteReaderAsync(ct);
			while (await reader.ReadAsync(ct))
			{
				var id = reader.GetInt32(0);
				var name = reader.GetString(1);
				list.Add(new VendorDto(id, name));
			}
			int total = await _helper.GetTotalCountFromTable(conn, ct, tableName);

			ListDto<VendorDto> result = new ListDto<VendorDto>(list, total);

			return result;
		}

		public async Task<bool> UpdateAsync(int id, VendorDto dto, CancellationToken ct = default)
		{ 

			if (dto == null) throw new ArgumentNullException(nameof(dto));

			using var conn = _dbFactory.CreateConnection();
			await conn.OpenAsync(ct);

			using var cmd = conn.CreateCommand();
			cmd.CommandText =
				"UPDATE Vendors SET name = @name WHERE id = @id;";

			cmd.Parameters.AddWithValue("@name", dto.Name ?? string.Empty);
			cmd.Parameters.AddWithValue("@id", id);

			var rows = await cmd.ExecuteNonQueryAsync(ct);
			return rows > 0;
		}

		public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
		{
		 

			using var conn = _dbFactory.CreateConnection();
			await conn.OpenAsync(ct);

			using var cmd = conn.CreateCommand();
			cmd.CommandText = "DELETE FROM Vendors WHERE id = @id;";
			cmd.Parameters.AddWithValue("@id", id);

			var rows = await cmd.ExecuteNonQueryAsync(ct);
			return rows > 0;
		}

		public async Task<VendorDto?> GetAsync(int id, CancellationToken ct = default)
		{
		 
			using var conn = _dbFactory.CreateConnection();
			await conn.OpenAsync(ct);

			using var cmd = conn.CreateCommand();
			cmd.CommandText = "SELECT id, name FROM Vendors WHERE id = @id LIMIT 1;";
			cmd.Parameters.AddWithValue("@id", id);

			using var rdr = await cmd.ExecuteReaderAsync(ct);
			if (await rdr.ReadAsync(ct))
			{
				static T GetSafe<T>(SqliteDataReader r, int i, Func<object, T> conv, T @default = default!)
				{
					if (r.IsDBNull(i)) return @default!;
					return conv(r.GetValue(i));
				}
				int vendor_id = GetSafe(rdr, 0, o => Convert.ToInt32(o));
				string vendor_name = GetSafe(rdr, 2, o => Convert.ToString(o) ?? string.Empty, string.Empty);
				return new VendorDto(vendor_id, vendor_name); ;
			}

			return null;
		}
	}
}
