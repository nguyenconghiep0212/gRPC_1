using IotGrpcLearning.Models;

namespace IotGrpcLearning.Interfaces
{
    public interface ITestSuite
	{
		Task<ListDto<TestSuiteDto>> GetAllAsync(PaginationDto body,CancellationToken ct = default);
		Task<TestSuiteDto?> GetAsync(int id, CancellationToken ct = default);
		Task<TestSuiteDto> CreateAsync(TestSuiteDto dto, CancellationToken ct = default);
		Task<bool> UpdateAsync(int id, TestSuiteDto dto, CancellationToken ct = default);
		Task<bool> DeleteAsync(int id, CancellationToken ct = default);
	}
}
