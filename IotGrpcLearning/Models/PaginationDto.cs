namespace IotGrpcLearning.Models;

public record PaginationDto(
	int limit,
	int offset,
	Dictionary<string, string[]>? filters
	);

