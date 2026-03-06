using IotGrpcLearning.Models;

namespace IotGrpcLearning.Interfaces
{
    public interface IEmployee
	{
		Task<ListDto<EmployeeResponse>> GetAllAsync(PaginationDto body, CancellationToken ct = default);
		Task<EmployeeResponse?> GetAsync(int id, CancellationToken ct = default);
		Task<EmployeesDto> CreateAsync(EmployeesDto dto, CancellationToken ct = default);
		Task<bool> UpdateAsync(int id, EmployeesDto dto, CancellationToken ct = default);
		Task<bool> DeleteAsync(int id,	CancellationToken ct = default);
	}
}
