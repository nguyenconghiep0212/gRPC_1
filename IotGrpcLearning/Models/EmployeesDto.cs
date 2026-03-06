namespace IotGrpcLearning.Models;

public record EmployeesDto(
	int Id,
	string AvatarUrl,
	string Name,
	string Email,
	int RoleId,
	int DivisionId,
	int? SupervisorId,
	int SiteId
	);
 
public record EmployeeResponse(
	int Id,
	string AvatarUrl,
	string Name,
	string Email,
	int RoleId,
	string Role,
	int DivisionId,
	string Division,
	int? SupervisorId,
	string? Supervisor,
	int SiteId,
	string Site
	);