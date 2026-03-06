using IotGrpcLearning.Models;

namespace IotGrpcLearning.Interfaces;

public interface IMachineInfoService
{
	Task<ListDto<MachineInfoDto>> GetAllAsync(PaginationDto body, CancellationToken ct = default);
	Task<MachineInfoDto?> GetAsync(int id, CancellationToken ct = default);
	Task<MachineInfoDto> CreateAsync(MachineInfoDto dto, CancellationToken ct = default);
	Task<bool> UpdateAsync(int id, MachineInfoDto dto, CancellationToken ct = default);
	Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}