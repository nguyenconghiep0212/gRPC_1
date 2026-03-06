namespace IotGrpcLearning.Models;

public record MachineInfoDto(
	int Id,
	int MachineId,
	int LineOverseer,
	int TestSuite
	);