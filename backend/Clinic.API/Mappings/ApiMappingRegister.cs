using Clinic.API.Models;
using Clinic.Application.Contracts;
using Clinic.Application.Features.Appointments.Queries;
using Clinic.Application.Features.Doctors.Queries;
using Clinic.Application.Features.Employees.Queries;
using Mapster;
using System;

namespace Clinic.API.Mappings
{
    // Ánh xạ giữa model của tầng API (Request/Response) và Command/Query/DTO của tầng Application.
    public class ApiMappingRegister : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            // Request -> Command/Query

            //config.NewConfig<FarmRequest, CreateFarmCommand>();
            //config.NewConfig<Guid, GetFarmByIdQuery>()
            //      .MapWith(id => new GetFarmByIdQuery(id));
            //config.NewConfig<PaginationRequest, GetFarmsQuery>();
            //config.NewConfig<CropRequest, CreateCropCommand>();

            // DTO -> Response
            //config.NewConfig<FarmDto, FarmResponse>();
            //config.NewConfig<CropDto, CropResponse>();
            config.NewConfig<DoctorsQueryRequest, GetDoctorsQuery>().Map(dest => dest.Descending, src => src.OrderBy == "desc");
            config.NewConfig<EmployeesQueryRequest, GetEmployeesQuery>().Map(dest => dest.Descending, src => src.OrderBy == "desc");
            config.NewConfig<PendingAppointmentRequest, GetPendingAppointmentsQuery>().Map(dest => dest.Descending, src => src.OrderBy == "desc");
        }
    }
}
