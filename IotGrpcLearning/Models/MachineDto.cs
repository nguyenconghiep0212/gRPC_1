namespace IotGrpcLearning.Models;

public record MachineDto(
    int Id,
    string Name,
    string Alias,
	string Details,
    int Vendor,
    double PurchasePrice,
    DateTime PurchaseDate,
    int Site);

 
public record MachineResponse(
	int Id,
	string Name,
	string Alias,
	string Details,
	int VendorId,
	string Vendor,
	double PurchasePrice,
	DateTime PurchaseDate,
	int SiteId,
	string Site
	);