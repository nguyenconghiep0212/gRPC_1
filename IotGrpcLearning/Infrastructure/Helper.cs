using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Data.Sqlite;
using System.Text;

namespace IotGrpcLearning.Infrastructure
{
	public class Helper
	{
		public async Task<string?> GetPropertyTableAsync(SqliteConnection conn, CancellationToken ct, string table, string column, string columnValue, string getColumn)
		{
			// try select
			using var sel = conn.CreateCommand();
			sel.CommandText = $"SELECT {getColumn} FROM \"{table}\" WHERE {column} = @{column} LIMIT 1;";
			var pSel = sel.CreateParameter();
			pSel.ParameterName = $"@{column}";
			pSel.Value = columnValue;
			sel.Parameters.Add(pSel);

			var scalar = await sel.ExecuteScalarAsync(ct);
			if (scalar != null && scalar != DBNull.Value)
				return Convert.ToString(scalar);
			return null;
		}

		public async Task<int> GetTotalCountFromTable(SqliteConnection conn, CancellationToken ct, string table)
		{
			int total = 0;

			var cmd = conn.CreateCommand();
			cmd.CommandText = $"SELECT COUNT(*) FROM {table};";
			using var reader = await cmd.ExecuteReaderAsync(ct);

			if (await reader.ReadAsync(ct))
			{
				total = reader.GetInt32(0);
			}

			return total;
		}

		public (string filterQuery, List<SqliteParameter> parameters) BuildFilterQuery(string tableName, Dictionary<string, string[]> filters)
		{
			if (filters == null || filters.Count == 0)
			{
				return (string.Empty, new List<SqliteParameter>());
			}

			var parameters = new List<SqliteParameter>();
			var conditions = new List<string>();

			foreach (var filter in filters)
			{
				var columnName = filter.Key;
				var filterValues = filter.Value;

				if (filterValues == null || filterValues.Length == 0)
					continue;

				var parameterNames = new List<string>();
				for (int i = 0; i < filterValues.Length; i++)
				{
					var parameterName = $"@{columnName}Value{i}"; // Create unique parameter name
					parameterNames.Add(parameterName);
					parameters.Add(new SqliteParameter(parameterName, filterValues[i]));
				}

				// Create the IN clause for this column
				var condition = $"{columnName} IN ({string.Join(", ", parameterNames)})";
				conditions.Add(condition);
			}

			// Combine all conditions with AND
			string filterQuery = " WHERE " + string.Join(" AND ", conditions);

			return (filterQuery, parameters);
		}
		public async Task<int> GetTotalCountWithConditions(SqliteConnection conn, CancellationToken ct, string table, Dictionary<string, string[]> filters)
		{
			int total = 0;

			// Create the command
			var cmd = conn.CreateCommand();
			var queryBuilder = new StringBuilder($"SELECT COUNT(*) FROM {table}"); 
		

			// Build the filter query and add it to the command
			var (filterQuery, parameters) = BuildFilterQuery(table, filters);

			queryBuilder.Append(filterQuery);
			// Add parameters to the command
			foreach (var parameter in parameters)
			{
				cmd.Parameters.Add(parameter);
			}
			cmd.CommandText = queryBuilder.ToString(); 
			// Execute the command and get the total count 
			using var reader = await cmd.ExecuteReaderAsync(ct);
			if (await reader.ReadAsync(ct))
			{
				total = reader.GetInt32(0);
			}

			return total;
		}
	}
}
