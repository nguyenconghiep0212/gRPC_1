using IotGrpcLearning.Models;

namespace IotGrpcLearning.Interfaces
{
    public interface IVendor
    {
		Task<ListDto<VendorDto>> GetAllAsync(PaginationDto body, CancellationToken ct = default);
		Task<VendorDto?> GetAsync(int id, CancellationToken ct = default);
		Task<VendorDto> CreateAsync(VendorDto dto, CancellationToken ct = default);
		Task<bool> UpdateAsync(int id, VendorDto dto, CancellationToken ct = default);
		Task<bool> DeleteAsync(int id, CancellationToken ct = default);
	}
}
