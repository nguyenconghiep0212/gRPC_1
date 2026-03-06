using IotGrpcLearning.Interfaces;
using IotGrpcLearning.Models;
using IotGrpcLearning.Services;
using Microsoft.AspNetCore.Mvc;

namespace IotGrpcLearning.Controllers
{
	[ApiController]
	[Route("api/employees")]
	public class EmployeesController : ControllerBase
	{

		private readonly IEmployee _service;

		public EmployeesController(IEmployee service)
		{
			_service = service;
		}

		// POST /api/employees/create
		[HttpPost("create")]
		public async Task<EmployeesDto> Create(EmployeesDto dto)
		{
			var d = new EmployeesDto(
				dto.Id,
				dto.AvatarUrl,
				dto.Name,
				dto.Email,
				dto.RoleId,
				dto.DivisionId,
				dto.SupervisorId,
				dto.SiteId
				);
			var created = await _service.CreateAsync(d);
			return new EmployeesDto(created.Id, created.AvatarUrl, created.Name, created.Email, created.RoleId, created.DivisionId, created.SupervisorId, created.SiteId);
		}

		// POST /api/employees/list
		[HttpPost("list")]
		public async Task<ActionResult<ListDto<EmployeeResponse>>> List(PaginationDto body)
		{
			ListDto<EmployeeResponse> result = await _service.GetAllAsync(body);
			return Ok(result);
		}

		// GET /api/employees/{employeeId}/detail
		[HttpGet("{employeeId}/detail")]
		public async Task<ActionResult<IEnumerable<EmployeeResponse>>> Detail(int employeeId)
		{
			var employee = await _service.GetAsync(employeeId);
			return Ok(employee);
		}

		// PUT /api/employees/{employeeId}/update
		[HttpPut("{employeeId}/update")]
		public async Task<ActionResult<bool>> Update(int employeeId, EmployeesDto dto)
		{
			var d = new EmployeesDto(
				dto.Id,
				dto.AvatarUrl,
				dto.Name,
				dto.Email,
				dto.RoleId,
				dto.DivisionId,
				dto.SupervisorId,
				dto.SiteId
				);
			var updated = await _service.UpdateAsync(employeeId, d);
			if (!updated)
				return NotFound(new { error = "employee not found" });
			return Ok(new { message = "employee updated successfully" });
		}

		// DELETE /api/employees/{employeeId}/delete
		[HttpDelete("{employeeId}/delete")]
		public async Task<ActionResult<bool>> Delete(int employeeId)
		{
			var deleted = await _service.DeleteAsync(employeeId);
			if (!deleted)
				return NotFound(new { error = "employees not found" });
			return Ok(new { message = "employees deleted successfully" });
		}
	}
}
