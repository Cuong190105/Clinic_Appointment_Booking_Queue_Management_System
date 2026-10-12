using Azure.Core;
using Clinic.API.Models;
using Clinic.Application.Contracts;
using Clinic.Application.Features.Specialties.Commands;
using Clinic.Application.Features.Specialties.Queries;
using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.API.Controllers
{
    [ApiController]
    [Route("/specialties")]
    public class SpecialtiesController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly IMapper _mapper;

        public SpecialtiesController(ISender sender, IMapper mapper)
        {
            _sender = sender;
            _mapper = mapper;
        }

        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(typeof(PaginationResponse<SpecialtyDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll([FromQuery] GetSpecialtiesRequest request)
        {
            var command = _mapper.Map<GetSpecialtiesQuery>(request);
            var result = await _sender.Send(command);
            return Ok(result);
        }

        [HttpGet("{specialtyId}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(SpecialtyDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById([FromRoute] string specialtyId)
        {
            var command = _mapper.Map<GetSpecialtyQuery>(new { Id = specialtyId });
            var result = await _sender.Send(command);
            return Ok(result);
        }

        [HttpPost]
        [Authorize(Policy = "Permission:specialty.manage")]
        [ProducesResponseType(typeof(SpecialtyDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] CreateSpecialtyRequest request)
        {
            var command = _mapper.Map<CreateSpecialtyCommand>(request);
            var result = await _sender.Send(command);
            return CreatedAtAction(nameof(GetById), new { specialtyId = result.Id }, result);
        }

        [HttpPut("{specialtyId}")]
        [Authorize(Policy = "Permission:specialty.manage")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Update([FromRoute] string specialtyId, [FromBody] UpdateSpecialtyRequest request)
        {
            var command = _mapper.Map<UpdateSpecialtyCommand>(new
            {
                Id = specialtyId,
                request.Name,
                request.Description,
                request.EstablishedDate
            });
            await _sender.Send(command);
            return NoContent();
        }

        [HttpPatch("{specialtyId}/status")]
        [Authorize(Policy = "Permission:specialty.manage")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateStatus([FromRoute] string specialtyId)
        {
            var command = _mapper.Map<DeleteSpecialtyCommand>(new { Id = specialtyId });
            await _sender.Send(command);
            return NoContent();
        }
    }
}
