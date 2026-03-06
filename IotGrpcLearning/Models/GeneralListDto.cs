namespace IotGrpcLearning.Models;

public record ListDto<T>(
	List<T> data,
	int total);


