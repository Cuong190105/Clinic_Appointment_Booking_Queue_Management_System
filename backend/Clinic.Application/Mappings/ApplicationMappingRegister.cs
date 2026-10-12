using Clinic.Application.Contracts;
using Clinic.Domain.Entities;
using Mapster;

namespace Clinic.Application.Mappings
{
    public class ApplicationMappingRegister : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            // Quan hệ 1 - N: Farm -> FarmDto (kèm danh sách Crop) và Crop -> CropDto.
            //config.NewConfig<Farm, FarmDto>();
            //config.NewConfig<Crop, CropDto>();

            // Add custom mapping rules here as the application grows.
        }
    }
}
