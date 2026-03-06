using IotGrpcLearning.Interfaces;
using IotGrpcLearning.Models;
using IotGrpcLearning.Services;
using Microsoft.AspNetCore.Mvc;

namespace IotGrpcLearning.Controllers
{
	[ApiController]
	[Route("api/vendors")]
	public class VendorController : ControllerBase
	{

		private readonly IVendor _service;

		public VendorController(IVendor service)
		{
			_service = service;
		}

		// POST /api/vendors/create
		[HttpPost("create")]
		public async Task<VendorDto> Create(VendorDto dto)
		{
			var d = new VendorDto(
				dto.Id,
				dto.Name
				);
			var created = await _service.CreateAsync(d);
			return new VendorDto(created.Id, created.Name);
		}

		// POST /api/vendors/list
		[HttpPost("list")]
		public async Task<ActionResult<ListDto<VendorDto>>> List(PaginationDto body)
		{
			var devices = await _service.GetAllAsync(body);
			return Ok(devices);
		}

		// GET /api/vendors/{vendorId}/detail
		[HttpGet("{vendorId}/detail")]
		public async Task<ActionResult<IEnumerable<VendorDto>>> Detail(int vendorId)
		{
			var devices = await _service.GetAsync(vendorId);
			return Ok(devices);
		}

		// PUT /api/vendors/{vendorId}/update
		[HttpPut("{vendorId}/update")]
		public async Task<ActionResult<bool>> Update(int vendorId, VendorDto dto)
		{
			var d = new VendorDto(
				dto.Id,
				dto.Name
				);
			var updated = await _service.UpdateAsync(vendorId, d);
			if (!updated)
				return NotFound(new { error = "vendor not found" });
			return Ok(new { message = "vendor updated successfully" });
		}

		// DELETE /api/vendors/{vendorId}/delete
		[HttpDelete("{vendorId}/delete")]
		public async Task<ActionResult<bool>> Delete(int vendorId)
		{
			var deleted = await _service.DeleteAsync(vendorId);
			if (!deleted)
				return NotFound(new { error = "vendor not found" });
			return Ok(new { message = "vendor deleted successfully" });
		}
	}
}