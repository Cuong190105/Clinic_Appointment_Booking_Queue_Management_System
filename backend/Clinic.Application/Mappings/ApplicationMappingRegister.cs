using Clinic.Application.Contracts;
using Clinic.Domain.Entities;
using Mapster;

namespace Clinic.Application.Mappings
{
    public class ApplicationMappingRegister : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            // Quan há»‡ 1 - N: Farm -> FarmDto (kÃ¨m danh sÃ¡ch Crop) vÃ  Crop -> CropDto.
            //config.NewConfig<Farm, FarmDto>();
            //config.NewConfig<Crop, CropDto>();

            // Add custom mapping rules here as the application grows.
        }
    }
}
