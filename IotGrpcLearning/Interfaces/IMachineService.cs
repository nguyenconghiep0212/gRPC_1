using IotGrpcLearning.Models;

namespace IotGrpcLearning.Interfaces;

public interface IMachineService
{
	Task<ListDto<MachineResponse>> GetAllAsync(PaginationDto body, CancellationToken ct = default);
	Task<MachineResponse?> GetAsync(int id, CancellationToken ct = default);
	Task<MachineDto> CreateAsync(MachineDto dto, CancellationToken ct = default);
	Task<bool> UpdateAsync(int id, MachineDto dto, CancellationToken ct = default);
	Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}