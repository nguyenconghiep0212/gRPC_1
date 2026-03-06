using IotGrpcLearning.Classes;
using IotGrpcLearning.Models;
using Microsoft.Data.Sqlite;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace IotGrpcLearning.Infrastructure
{
	public sealed class Seeder
	{
		private readonly ISqliteConnectionFactory _dbFactory;
		private readonly string _contentRoot;

		public Seeder(ISqliteConnectionFactory dbFactory, IHostEnvironment env)
		{
			_dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
			_contentRoot = env?.ContentRootPath ?? AppContext.BaseDirectory;

		}

		private sealed record MachineSeed(
			string Name,
			string Details,
			string VendorName,
			decimal PurchasePrice,
			DateTime PurchaseDate,
			string SiteName,
			string? Alias = null
		);

		/// <summary>
		/// Delete all rows from known application tables so seeding can start from a clean state.
		/// Uses a transaction and temporarily disables foreign key enforcement.
		/// WARNING: destructive operation — execute only in non-production environments.
		/// </summary>
		public async Task ClearAllDataAsync(CancellationToken ct = default)
		{
			// Order chosen to avoid FK constraint violations when deleting rows:
			var tables = new[]
			{
				// Independent tables first
				"Sites",
				"Divisions",
				"Roles",
				"Vendors",
				"Customers",
				"TestSuite",
				// Then dependent tables
				"Employees",
				"Projects",
				"MachineStatus",
				"MachinesInfo",
				"Machines"
				// Relationship tables last
			};

			using var conn = _dbFactory.CreateConnection();
			await conn.OpenAsync(ct);

			using var tx = conn.BeginTransaction();
			using var cmd = conn.CreateCommand();
			cmd.Transaction = tx;

			// Temporarily disable foreign key checks so deletes succeed regardless of FK order
			cmd.CommandText = "PRAGMA foreign_keys = OFF;";
			await cmd.ExecuteNonQueryAsync(ct);

			foreach (var table in tables)
			{
				ct.ThrowIfCancellationRequested();

				// Delete all rows
				cmd.CommandText = $"DELETE FROM \"{table}\";";
				try
				{
					await cmd.ExecuteNonQueryAsync(ct);

				}
				catch
				{
					Console.WriteLine($"Warning: Could not clear table '{table}'. It may not exist.");
					// ignore - table may not exist
				}

				// Reset AUTOINCREMENT counter for the table if present
				cmd.CommandText = $"DELETE FROM sqlite_sequence WHERE name = '{table}';";
				try
				{
					await cmd.ExecuteNonQueryAsync(ct);
				}
				catch
				{
					Console.WriteLine($"Warning: Could not reset AUTOINCREMENT for table '{table}'. It may not exist or may not use AUTOINCREMENT.");
					// ignore - sqlite_sequence may not exist or table may not use AUTOINCREMENT
				}
			}

			// Re-enable foreign key enforcement
			cmd.CommandText = "PRAGMA foreign_keys = ON;";
			await cmd.ExecuteNonQueryAsync(ct);

			tx.Commit();
		}


		/// <summary>
		/// Idempotently inserts a small set of sample into tables.
		/// Uses a transaction and parameterized SQL to avoid SQL injection.
		/// </summary>
		public async Task SeedVendorAsync(CancellationToken ct = default)
		{
			VendorDto[] samples = JsonFileLoader.LoadFromJson<VendorDto>(Path.Combine("Infrastructure", "SeedData", "vendors.json"), _contentRoot);

			using var conn = _dbFactory.CreateConnection();
			await conn.OpenAsync(ct);

			using var tx = conn.BeginTransaction();
			using var cmd = conn.CreateCommand();
			cmd.Transaction = tx;

			// Insert only if that name does not already exist (idempotent).
			cmd.CommandText = @"
                INSERT INTO Vendors (name)
                SELECT @name
                WHERE NOT EXISTS (SELECT 1 FROM Vendors WHERE name = @name LIMIT 1);
            ";

			var p = cmd.CreateParameter();
			p.ParameterName = "@name";
			cmd.Parameters.Add(p);

			foreach (var s in samples)
			{
				ct.ThrowIfCancellationRequested();
				p.Value = s.Name;
				await cmd.ExecuteNonQueryAsync(ct);
			}

			tx.Commit();
		}

		public async Task SeedTestSuiteAsync(CancellationToken ct = default)
		{
			TestSuiteDto[] samples = JsonFileLoader.LoadFromJson<TestSuiteDto>(Path.Combine("Infrastructure", "SeedData", "test_suite.json"), _contentRoot);

			using var conn = _dbFactory.CreateConnection();
			await conn.OpenAsync(ct);

			using var tx = conn.BeginTransaction();
			using var cmd = conn.CreateCommand();
			cmd.Transaction = tx;

			// Insert only if that name does not already exist (idempotent).
			cmd.CommandText = @"
                INSERT INTO TestSuite (name, machine, path, detail)
                SELECT @name, @machine, @path, @detail
                WHERE NOT EXISTS (SELECT 1 FROM TestSuite WHERE name = @name LIMIT 1);
            ";

			var pName = cmd.CreateParameter(); pName.ParameterName = "@name"; cmd.Parameters.Add(pName);
			var pMachineId = cmd.CreateParameter(); pMachineId.ParameterName = "@machine"; cmd.Parameters.Add(pMachineId);
			var pPath = cmd.CreateParameter(); pPath.ParameterName = "@path"; cmd.Parameters.Add(pPath);
			var pDetail = cmd.CreateParameter(); pDetail.ParameterName = "@detail"; cmd.Parameters.Add(pDetail);

			foreach (var s in samples)
			{
				ct.ThrowIfCancellationRequested();
				pName.Value = s.Name;
				pMachineId.Value = s.MachineId;
				pPath.Value = s.Path;
				pDetail.Value = s.Detail;
				await cmd.ExecuteNonQueryAsync(ct);
			}

			tx.Commit();
		}

		public async Task SeedCustomerAsync(CancellationToken ct = default)
		{
			CustomerDto[] samples = JsonFileLoader.LoadFromJson<CustomerDto>(Path.Combine("Infrastructure", "SeedData", "customers.json"), _contentRoot);

			using var conn = _dbFactory.CreateConnection();
			await conn.OpenAsync(ct);

			using var tx = conn.BeginTransaction();
			using var cmd = conn.CreateCommand();
			cmd.Transaction = tx;

			// Insert only if that name does not already exist (idempotent).
			cmd.CommandText = @"
                INSERT INTO Customers (name)
                SELECT @name
                WHERE NOT EXISTS (SELECT 1 FROM Customers WHERE name = @name LIMIT 1);
            ";

			var p = cmd.CreateParameter();
			p.ParameterName = "@name";
			cmd.Parameters.Add(p);

			foreach (var s in samples)
			{
				ct.ThrowIfCancellationRequested();
				p.Value = s.Name;
				await cmd.ExecuteNonQueryAsync(ct);
			}

			tx.Commit();
		}

		public async Task SeedRolesAsync(CancellationToken ct = default)
		{
			RolesDto[] samples = JsonFileLoader.LoadFromJson<RolesDto>(Path.Combine("Infrastructure", "SeedData", "roles.json"), _contentRoot);

			using var conn = _dbFactory.CreateConnection();
			await conn.OpenAsync(ct);

			using var tx = conn.BeginTransaction();
			using var cmd = conn.CreateCommand();
			cmd.Transaction = tx;

			// Insert only if that name does not already exist (idempotent).
			cmd.CommandText = @"
                INSERT INTO Roles (name)
                SELECT @name
                WHERE NOT EXISTS (SELECT 1 FROM Roles WHERE name = @name LIMIT 1);
            ";

			var p = cmd.CreateParameter();
			p.ParameterName = "@name";
			cmd.Parameters.Add(p);

			foreach (var s in samples)
			{
				ct.ThrowIfCancellationRequested();
				p.Value = s.Name;
				await cmd.ExecuteNonQueryAsync(ct);
			}

			tx.Commit();
		}

		public async Task SeedDivisionAsync(CancellationToken ct = default)
		{
			DivisionsDto[] samples = JsonFileLoader.LoadFromJson<DivisionsDto>(Path.Combine("Infrastructure", "SeedData", "divisions.json"), _contentRoot);

			using var conn = _dbFactory.CreateConnection();
			await conn.OpenAsync(ct);

			using var tx = conn.BeginTransaction();
			using var cmd = conn.CreateCommand();
			cmd.Transaction = tx;

			// Insert only if that name does not already exist (idempotent).
			cmd.CommandText = @"
                INSERT INTO Divisions (name)
                SELECT @name
                WHERE NOT EXISTS (SELECT 1 FROM Divisions WHERE name = @name LIMIT 1);
            ";

			var p = cmd.CreateParameter();
			p.ParameterName = "@name";
			cmd.Parameters.Add(p);

			foreach (var s in samples)
			{
				ct.ThrowIfCancellationRequested();
				p.Value = s.Name;
				await cmd.ExecuteNonQueryAsync(ct);
			}

			tx.Commit();
		}

		public async Task SeedSiteAsync(CancellationToken ct = default)
		{
			SitesDto[] samples = JsonFileLoader.LoadFromJson<SitesDto>(Path.Combine("Infrastructure", "SeedData", "sites.json"), _contentRoot);

			using var conn = _dbFactory.CreateConnection();
			await conn.OpenAsync(ct);

			using var tx = conn.BeginTransaction();
			using var cmd = conn.CreateCommand();
			cmd.Transaction = tx;

			// Insert only if that name does not already exist (idempotent).
			cmd.CommandText = @"
                INSERT INTO Sites (name, location, address)
                SELECT @name, @location, @address
                WHERE NOT EXISTS (SELECT 1 FROM Sites WHERE name = @name LIMIT 1);
           ";

			var pName = cmd.CreateParameter(); pName.ParameterName = "@name"; cmd.Parameters.Add(pName);
			var pLocation = cmd.CreateParameter(); pLocation.ParameterName = "@location"; cmd.Parameters.Add(pLocation);
			var pAddress = cmd.CreateParameter(); pAddress.ParameterName = "@address"; cmd.Parameters.Add(pAddress);

			foreach (var s in samples)
			{
				ct.ThrowIfCancellationRequested();
				pName.Value = s.Name;
				pLocation.Value = s.Location;
				pAddress.Value = s.Address;
				await cmd.ExecuteNonQueryAsync(ct);
			}

			tx.Commit();
		}

		public async Task SeedProjectAsync(CancellationToken ct = default)
		{
			ProjectDto[] samples = JsonFileLoader.LoadFromJson<ProjectDto>(Path.Combine("Infrastructure", "SeedData", "projects.json"), _contentRoot);

			using var conn = _dbFactory.CreateConnection();
			await conn.OpenAsync(ct);

			using var tx = conn.BeginTransaction();
			using var cmd = conn.CreateCommand();
			cmd.Transaction = tx;

			// Insert only if that name does not already exist (idempotent).
			cmd.CommandText = @"
                INSERT INTO Projects (name, customers_id, site, detail)
                SELECT @name, @customers_id, @site, @detail
                WHERE NOT EXISTS (SELECT 1 FROM Projects WHERE name = @name LIMIT 1);
           ";

			var pName = cmd.CreateParameter(); pName.ParameterName = "@name"; cmd.Parameters.Add(pName);
			var pCustomerId = cmd.CreateParameter(); pCustomerId.ParameterName = "@customers_id"; cmd.Parameters.Add(pCustomerId);
			var pSite = cmd.CreateParameter(); pSite.ParameterName = "@site"; cmd.Parameters.Add(pSite);
			var pDetail = cmd.CreateParameter(); pDetail.ParameterName = "@detail"; cmd.Parameters.Add(pDetail);

			foreach (var s in samples)
			{
				ct.ThrowIfCancellationRequested();
				pName.Value = s.Name ?? string.Empty;
				pCustomerId.Value = s.CustomerId;
				pSite.Value = s.SiteId;
				pDetail.Value = s.Details ?? string.Empty;
				await cmd.ExecuteNonQueryAsync(ct);
			}

			tx.Commit();
		}

		public async Task SeedEmployeeAsync(CancellationToken ct = default)
		{
			EmployeesDto[] samples = JsonFileLoader.LoadFromJson<EmployeesDto>(Path.Combine("Infrastructure", "SeedData", "employees.json"), _contentRoot);
			using var conn = _dbFactory.CreateConnection();
			await conn.OpenAsync(ct);

			using var tx = conn.BeginTransaction();
			using var cmd = conn.CreateCommand();
			cmd.Transaction = tx;

			// Insert only if that name does not already exist (idempotent).
			cmd.CommandText = @"
                INSERT INTO Employees (avatar_url, name, email, role_id, division_id, supervisor, site)
                SELECT @avatar_url, @name, @email, @role_id, @division_id, @supervisor, @site
                WHERE NOT EXISTS (SELECT 1 FROM Employees WHERE name = @name LIMIT 1);
            ";

			var pAvatarUrl = cmd.CreateParameter(); pAvatarUrl.ParameterName = "@avatar_url"; cmd.Parameters.Add(pAvatarUrl);
			var pName = cmd.CreateParameter(); pName.ParameterName = "@name"; cmd.Parameters.Add(pName);
			var pEmail = cmd.CreateParameter(); pEmail.ParameterName = "@email"; cmd.Parameters.Add(pEmail);
			var pRoleId = cmd.CreateParameter(); pRoleId.ParameterName = "@role_id"; cmd.Parameters.Add(pRoleId);
			var pDivisionId = cmd.CreateParameter(); pDivisionId.ParameterName = "@division_id"; cmd.Parameters.Add(pDivisionId);
			var pSupervisor = cmd.CreateParameter(); pSupervisor.ParameterName = "@supervisor"; cmd.Parameters.Add(pSupervisor);
			var pSite = cmd.CreateParameter(); pSite.ParameterName = "@site"; cmd.Parameters.Add(pSite);

			foreach (var employee in samples)
			{
				ct.ThrowIfCancellationRequested();
				pAvatarUrl.Value = employee.AvatarUrl;
				pName.Value = employee.Name;
				pEmail.Value = employee.Email;
				pRoleId.Value = employee.RoleId;
				pDivisionId.Value = employee.DivisionId;
				pSupervisor.Value = (object?)employee.SupervisorId ?? DBNull.Value;
				pSite.Value = employee.SiteId;
				await cmd.ExecuteNonQueryAsync(ct);
			}
			tx.Commit();
		}


		public async Task SeedMachineAsync(CancellationToken ct = default)
		{
			MachineDto[] samples = JsonFileLoader.LoadFromJson<MachineDto>(Path.Combine("Infrastructure", "SeedData", "machines.json"), _contentRoot);

			using var conn = _dbFactory.CreateConnection();
			await conn.OpenAsync(ct);
			using var tx = conn.BeginTransaction();
			using var cmd = conn.CreateCommand();
			cmd.Transaction = tx;

			cmd.CommandText = @"
                INSERT INTO Machines ( 
                    name,
					alias,
                    details,
                    vendor,
                    purchase_price,
                    purchase_date, 
                    site
                )
                SELECT 
                    @name,
					@alias,
                    @details,
                    @vendor,
                    @purchase_price,
                    @purchase_date, 
                    @site
            ";

			var pName = cmd.CreateParameter(); pName.ParameterName = "@name"; cmd.Parameters.Add(pName);
			var pAlias = cmd.CreateParameter(); pAlias.ParameterName = "@alias"; cmd.Parameters.Add(pAlias);
			var pDetails = cmd.CreateParameter(); pDetails.ParameterName = "@details"; cmd.Parameters.Add(pDetails);
			var pVendor = cmd.CreateParameter(); pVendor.ParameterName = "@vendor"; cmd.Parameters.Add(pVendor);
			var pPurchasePrice = cmd.CreateParameter(); pPurchasePrice.ParameterName = "@purchase_price"; cmd.Parameters.Add(pPurchasePrice);
			var pPurchaseDate = cmd.CreateParameter(); pPurchaseDate.ParameterName = "@purchase_date"; cmd.Parameters.Add(pPurchaseDate);
			var pSite = cmd.CreateParameter(); pSite.ParameterName = "@site"; cmd.Parameters.Add(pSite);


			foreach (var sample in samples)
			{
				ct.ThrowIfCancellationRequested();
				// ensure vendor and site exist and get their ids

				pName.Value = sample.Name;
				pAlias.Value = sample.Name;
				pDetails.Value = sample.Details ?? (object)DBNull.Value;
				pVendor.Value = sample.Vendor;
				// SQLite accepts REAL for decimal; convert to double
				pPurchasePrice.Value = Convert.ToDouble(sample.PurchasePrice);
				pPurchaseDate.Value = DateTime.SpecifyKind(sample.PurchaseDate, DateTimeKind.Utc);
				pSite.Value = sample.Site;

				await cmd.ExecuteNonQueryAsync(ct);
			}

			tx.Commit();
			await SeedMachineStatus(ct);
			await SeedMachineInfo(ct);
		}

		public async Task SeedMachineStatus(CancellationToken ct = default)
		{
			MachineDto[] samples = JsonFileLoader.LoadFromJson<MachineDto>(Path.Combine("Infrastructure", "SeedData", "machines.json"), _contentRoot);

			using var conn = _dbFactory.CreateConnection();
			await conn.OpenAsync(ct);
			using var tx = conn.BeginTransaction();
			using var cmd = conn.CreateCommand();
			cmd.Transaction = tx;

			cmd.CommandText = @"
                INSERT INTO MachineStatus ( 
                    machine,
					health,
                    is_online,
                    last_online
                )
                SELECT 
                    @machine,
					@health,
                    @is_online,
                    @last_online
                WHERE NOT EXISTS (SELECT 1 FROM MachineStatus WHERE machine = @machine LIMIT 1);
            ";
			var pMachine = cmd.CreateParameter(); pMachine.ParameterName = "@machine"; cmd.Parameters.Add(pMachine);
			var pHealth = cmd.CreateParameter(); pHealth.ParameterName = "@health"; cmd.Parameters.Add(pHealth);
			var pIsOnline = cmd.CreateParameter(); pIsOnline.ParameterName = "@is_online"; cmd.Parameters.Add(pIsOnline);
			var pLastOnline = cmd.CreateParameter(); pLastOnline.ParameterName = "@last_online"; cmd.Parameters.Add(pLastOnline);

			int index = 1;
			foreach (var sample in samples)
			{
				ct.ThrowIfCancellationRequested();
				var MachineId = index;

				pMachine.Value = MachineId;
				pHealth.Value = MachineHealth.Unavailable;
				pIsOnline.Value = MachineState.Offline;
				pLastOnline.Value = DateTime.Now;

				await cmd.ExecuteNonQueryAsync(ct);
				index++;
			}

			tx.Commit();
		}

		public async Task SeedMachineInfo(CancellationToken ct = default)
		{
			MachineDto[] samples = JsonFileLoader.LoadFromJson<MachineDto>(Path.Combine("Infrastructure", "SeedData", "machines.json"), _contentRoot);

			using var conn = _dbFactory.CreateConnection();
			await conn.OpenAsync(ct);
			using var tx = conn.BeginTransaction();
			using var cmd = conn.CreateCommand();
			cmd.Transaction = tx;

			cmd.CommandText = @"
                INSERT INTO MachinesInfo ( 
                    machine,
					line_overseer_id
                )
                SELECT 
                    @machine,
					@line_overseer_id
                WHERE NOT EXISTS (SELECT 1 FROM MachineStatus WHERE machine = @machine LIMIT 1);
            ";
			var pMachine = cmd.CreateParameter(); pMachine.ParameterName = "@machine"; cmd.Parameters.Add(pMachine);
			var pLineOverseer = cmd.CreateParameter(); pLineOverseer.ParameterName = "@line_overseer_id"; cmd.Parameters.Add(pLineOverseer);
			//var pTestSuite = cmd.CreateParameter(); pTestSuite.ParameterName = "@test_suite"; cmd.Parameters.Add(pTestSuite);

			static int getOverseer(int site) {
				int overseer = 0;
				switch (site)
				{
					case 1:
						overseer = 11;
						break;
					case 2:
						overseer = 5;
						break;
					case 5:
						overseer = 15;
						break;
					default:
						overseer = 0;
						break;

				}
				return overseer;
			}

			int index = 1;
			foreach (var sample in samples)
			{
				ct.ThrowIfCancellationRequested();
				var MachineId = index;

				pMachine.Value = MachineId;
				pLineOverseer.Value = getOverseer(index);
				//pTestSuite.Value = null;

				await cmd.ExecuteNonQueryAsync(ct);
				index++;
			}

			tx.Commit();
		}


		#region ==== Helper ====
		// helper that ensures a lookup row exists and returns its id (in same transaction)
		async Task<int> EnsureLookupIdAsync(SqliteConnection conn, SqliteTransaction tx, CancellationToken ct, string table, string name, string additionalInsertColumns = "", object? additionalInsertValues = null)
		{
			// try select
			using var sel = conn.CreateCommand();
			sel.Transaction = tx;
			sel.CommandText = $"SELECT id FROM \"{table}\" WHERE name = @name LIMIT 1;";
			var pSel = sel.CreateParameter();
			pSel.ParameterName = "@name";
			pSel.Value = name;
			sel.Parameters.Add(pSel);

			var scalar = await sel.ExecuteScalarAsync(ct);
			if (scalar != null && scalar != DBNull.Value)
				return Convert.ToInt32(scalar);

			// not found -> insert minimal row
			using var ins = conn.CreateCommand();
			ins.Transaction = tx;

			if (string.IsNullOrEmpty(additionalInsertColumns))
			{
				ins.CommandText = $"INSERT INTO \"{table}\" (name) VALUES (@name); SELECT last_insert_rowid();";
			}
			else
			{
				ins.CommandText = $"INSERT INTO \"{table}\" (name, {additionalInsertColumns}) VALUES (@name, {additionalInsertValues}); SELECT last_insert_rowid();";
			}

			var pIns = ins.CreateParameter();
			pIns.ParameterName = "@name";
			pIns.Value = name;
			ins.Parameters.Add(pIns);

			var newId = await ins.ExecuteScalarAsync(ct);
			return Convert.ToInt32(newId);
		}
		#endregion
	}
}