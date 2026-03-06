using IotGrpcLearning.Infrastructure;
using IotGrpcLearning.Interfaces;
using IotGrpcLearning.Models;
using Microsoft.Data.Sqlite;
using System.Reflection.PortableExecutable;

namespace IotGrpcLearning.Services
{
	public sealed class MachineInfoService : IMachineInfoService
	{
		private readonly ISqliteConnectionFactory _dbFactory;
		private readonly Helper _helper;
		public MachineInfoService(ISqliteConnectionFactory dbFactory)
		{
			_helper = new Helper();
			_dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
		}

		public async Task<MachineInfoDto> CreateAsync(MachineInfoDto dto, CancellationToken ct = default)
		{
			if (dto == null) throw new ArgumentNullException(nameof(dto));

			using var conn = _dbFactory.CreateConnection();
			await conn.OpenAsync(ct);

			using var cmd = conn.CreateCommand();
			cmd.CommandText =
				"INSERT INTO MachinesInfo (machine, line_overseer_id, test_suite) " +
				"VALUES ( @machine, @line_overseer_id, @test_suite); " +
				"SELECT last_insert_rowid();";

			cmd.Parameters.AddWithValue("@machine", dto.MachineId);
			cmd.Parameters.AddWithValue("@line_overseer_id", dto.LineOverseer);
			cmd.Parameters.AddWithValue("@test_suite", dto.TestSuite);

			var result = await cmd.ExecuteScalarAsync(ct);
			var newId = Convert.ToInt32(result);

			return new MachineInfoDto(newId, dto.MachineId, dto.LineOverseer, dto.TestSuite);
		}

		public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
		{
			using var conn = _dbFactory.CreateConnection();
			await conn.OpenAsync(ct);

			using var cmd = conn.CreateCommand();
			cmd.CommandText = "DELETE FROM MachinesInfo WHERE id = @id;";
			cmd.Parameters.AddWithValue("@id", id);

			var rows = await cmd.ExecuteNonQueryAsync(ct);
			return rows > 0;
		}

		public async Task<ListDto<MachineInfoDto>> GetAllAsync(PaginationDto body, CancellationToken ct = default)
		{
			var list = new List<MachineInfoDto>();
			string tableName = "MachinesInfo";
			using var conn = _dbFactory.CreateConnection();
			await conn.OpenAsync(ct);

			using var cmd = conn.CreateCommand();
			cmd.CommandText = $"SELECT id, machine, line_overseer_id, test_suite FROM {tableName} ORDER BY id LIMIT {body.limit} OFFSET {body.offset};";
			using var rdr = await cmd.ExecuteReaderAsync(ct);

			while (await rdr.ReadAsync(ct))
			{

				static T GetSafe<T>(SqliteDataReader r, int i, Func<object, T> conv, T @default = default!)
				{
					if (r.IsDBNull(i)) return @default!;
					return conv(r.GetValue(i));
				}
				int id = GetSafe(rdr, 0, o => Convert.ToInt32(o));
				int machine = GetSafe(rdr, 1, o => Convert.ToInt32(o));
				int line_overseer = GetSafe(rdr, 2, o => Convert.ToInt32(o));
				int test_suite = GetSafe(rdr, 3, o => Convert.ToInt32(o));

				MachineInfoDto machineInfo = new MachineInfoDto(id, machine, line_overseer, test_suite);
				list.Add(machineInfo);
			}
			int total = await _helper.GetTotalCountFromTable(conn, ct, tableName);

			ListDto<MachineInfoDto> result = new ListDto<MachineInfoDto>(list, total);

			return result;
		}

		public async Task<MachineInfoDto?> GetAsync(int id, CancellationToken ct = default)
		{

			using var conn = _dbFactory.CreateConnection();
			await conn.OpenAsync(ct);

			using var cmd = conn.CreateCommand();
			cmd.CommandText = "SELECT id, machine, line_overseer_id, test_suite FROM MachinesInfo WHERE id = @id LIMIT 1;";
			cmd.Parameters.AddWithValue("@id", id);

			using var rdr = await cmd.ExecuteReaderAsync(ct);
			if (await rdr.ReadAsync(ct))
			{
				static T GetSafe<T>(SqliteDataReader r, int i, Func<object, T> conv, T @default = default!)
				{
					if (r.IsDBNull(i)) return @default!;
					return conv(r.GetValue(i));
				}
				int info_id = GetSafe(rdr, 0, o => Convert.ToInt32(o));
				int machine = GetSafe(rdr, 1, o => Convert.ToInt32(o));
				int line_overseer = GetSafe(rdr, 2, o => Convert.ToInt32(o));
				int test_suite = GetSafe(rdr, 3, o => Convert.ToInt32(o));

				MachineInfoDto machineInfo = new MachineInfoDto(info_id, machine, line_overseer, test_suite);
				return machineInfo;
			}

			return null;
		}

		public async Task<bool> UpdateAsync(int id, MachineInfoDto dto, CancellationToken ct = default)
		{

			if (dto == null) throw new ArgumentNullException(nameof(dto));

			using var conn = _dbFactory.CreateConnection();
			await conn.OpenAsync(ct);

			using var cmd = conn.CreateCommand();
			cmd.CommandText =
				"UPDATE MachinesInfo SET machine = @machine, line_overseer_id = @line_overseer_id, test_suite = @test_suite WHERE id = @id;";

			cmd.Parameters.AddWithValue("@machine", dto.MachineId);
			cmd.Parameters.AddWithValue("@line_overseer_id", dto.LineOverseer);
			cmd.Parameters.AddWithValue("@test_suite", dto.TestSuite);

			cmd.Parameters.AddWithValue("@id", id);

			var rows = await cmd.ExecuteNonQueryAsync(ct);
			return rows > 0;
		}

	}
}
