using Google.Protobuf;
using IotGrpcLearning.Infrastructure;
using IotGrpcLearning.Interfaces;
using IotGrpcLearning.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IotGrpcLearning.Controllers
{
	[ApiController]
	[Route("api/test_suite")]
	public sealed class TestSuiteController : ControllerBase
	{
		private readonly ITestSuite _service;

		public TestSuiteController(ITestSuite service)
		{
			_service = service;
		}

		// POST /api/test_suite/create
		[HttpPost("create")]
		public async Task<TestSuiteDto> Create(TestSuiteDto dto)
		{
			var d = new TestSuiteDto(
				dto.Id,
				dto.Name,
				dto.MachineId,
				dto.Path,
				dto.Detail
				);
			var created = await _service.CreateAsync(d);
			return new TestSuiteDto(created.Id, created.Name, created.MachineId, created.Path, created.Detail);
		}

		// POST /api/testsuite/list
		[HttpPost("list")]
		public async Task<ActionResult<ListDto<TestSuiteDto>>> List(PaginationDto body)
		{
			ListDto<TestSuiteDto> result = await _service.GetAllAsync(body);
			return Ok(result);
		}

		// GET /api/testsuite/{testsuiteId}/detail
		[HttpGet("{testsuiteId}/detail")]
		public async Task<ActionResult<IEnumerable<TestSuiteDto>>> Detail(int testsuiteId)
		{
			var devices = await _service.GetAsync(testsuiteId);
			return Ok(devices);
		}

		// PUT /api/testsuite/{testsuiteId}/update
		[HttpPut("{testsuiteId}/update")]
		public async Task<ActionResult<bool>> Update(int testsuiteId, TestSuiteDto dto)
		{
			var d = new TestSuiteDto(
				dto.Id,
				dto.Name,
				dto.MachineId,
				dto.Path,
				dto.Detail
				);
			var updated = await _service.UpdateAsync(testsuiteId, d);
			if (!updated)
				return NotFound(new { error = "test suite not found" });
			return Ok(new { message = "test suite updated successfully" });
		}

		// DELETE /api/testsuite/{testsuiteId}/delete
		[HttpDelete("{testsuiteId}/delete")]
		public async Task<ActionResult<bool>> Delete(int testsuiteId)
		{
			var deleted = await _service.DeleteAsync(testsuiteId);
			if (!deleted)
				return NotFound(new { error = "testsuite not found" });
			return Ok(new { message = "testsuite deleted successfully" });
		}
	}
}