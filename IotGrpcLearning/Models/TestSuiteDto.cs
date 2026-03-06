namespace IotGrpcLearning.Models;

public record TestSuiteDto(
	int Id,
	string Name,
	int MachineId,
	string Path,
	string Detail
	);

public record TestSuiteResponse(
	int Id,
	string Name,
	int MachineId,
	string Machine,
	string Path,
	string Detail
	);

