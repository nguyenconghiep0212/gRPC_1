using Microsoft.AspNetCore.Mvc;
using IotGrpcLearning.Models;
using IotGrpcLearning.Services;
using IotGrpcLearning.Proto;
using IotGrpcLearning.Interfaces;

namespace IotGrpcLearning.Controllers;

[ApiController]
[Route("api/machine_info")]
public class MachineInfoController : ControllerBase
{
	private readonly IMachineInfoService _service;

	public MachineInfoController(IMachineInfoService service)
	{
		_service = service;
	}

	// POST /api/machine_info/list
	[HttpPost("list")]
	public async Task<ActionResult<ListDto<MachineInfoDto>>> List(PaginationDto body)
	{
		var result = await _service.GetAllAsync(body);
		return Ok(result);
	}

	// GET /api/machine_info/{machineInfoId}/detail
	[HttpGet("{machineInfoId}/detail")]
	public async Task<ActionResult<MachineInfoDto>> Detail(int machineInfoId)
	{
		var d = await _service.GetAsync(machineInfoId);
		if (d is null)
			return NotFound(new { error = "machine not found" });

		return Ok(new MachineInfoDto(d.Id,
			d.MachineId,
			d.LineOverseer,
			d.TestSuite
			));
	}

	// POST /api/machine_info/create
	[HttpPost("create")]
	public async Task<MachineInfoDto> Create(MachineInfoDto dto)
	{
		var d = new MachineInfoDto(
			dto.Id,
			dto.MachineId,
			dto.LineOverseer,
			dto.TestSuite);
		var created = await _service.CreateAsync(d);
		return new MachineInfoDto(created.Id, created.MachineId, created.LineOverseer, created.TestSuite);
	}

	// PUT /api/machine_info/{machineInfoId}/update
	[HttpPut("{machineInfoId}/update")]
	public async Task<ActionResult<bool>> Update(int machineInfoId, MachineInfoDto dto)
	{
		var d = new MachineInfoDto(
			dto.Id,
			dto.MachineId,
			dto.LineOverseer,
			dto.TestSuite
			);
		var updated = await _service.UpdateAsync(machineInfoId, d);
		if (!updated)
			return NotFound(new { error = "machine info not found" });
		return Ok(new { message = "machine info updated successfully" });
	}

	// DELETE /api/machine_info/{machineInfoId}/delete
	[HttpDelete("{machineInfoId}/delete")]
	public async Task<ActionResult<bool>> Delete(int machineInfoId)
	{
		var deleted = await _service.DeleteAsync(machineInfoId);
		if (!deleted)
			return NotFound(new { error = "machine info not found" });
		return Ok(new { message = "machine info deleted successfully" });
	}
}