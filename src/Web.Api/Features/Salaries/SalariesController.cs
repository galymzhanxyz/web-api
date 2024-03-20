﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domain.Database;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Salaries;
using Domain.ValueObjects.Pagination;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechInterviewer.Controllers.Labels;
using TechInterviewer.Features.Salaries.AddSalary;
using TechInterviewer.Features.Salaries.Admin.GetApprovedSalaries;
using TechInterviewer.Features.Salaries.Admin.GetExcludedFromStatsSalaries;
using TechInterviewer.Features.Salaries.GetAdminChart;
using TechInterviewer.Features.Salaries.GetSalariesChart;
using TechInterviewer.Features.Salaries.GetSalariesChart.Charts;
using TechInterviewer.Features.Salaries.Models;
using TechInterviewer.Features.Salaries.UpdateSalary;
using TechInterviewer.Setup.Attributes;

namespace TechInterviewer.Features.Salaries;

[ApiController]
[Route("api/salaries")]
public class SalariesController : ControllerBase
{
    private readonly DatabaseContext _context;
    private readonly IMediator _mediator;

    public SalariesController(
        DatabaseContext context,
        IMediator mediator)
    {
        _context = context;
        _mediator = mediator;
    }

    [HttpGet("select-box-items")]
    public async Task<SelectBoxItemsResponse> GetSelectBoxItems(
        CancellationToken cancellationToken)
    {
        return new SelectBoxItemsResponse
        {
            Skills = await _context.Skills
                .Select(x => new LabelEntityDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    HexColor = x.HexColor,
                })
                .AsNoTracking()
                .ToListAsync(cancellationToken),
            Industries = await _context.WorkIndustries
                .Select(x => new LabelEntityDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    HexColor = x.HexColor,
                })
                .AsNoTracking()
                .ToListAsync(cancellationToken),
            Professions = await _context.Professions
                .Select(x => new LabelEntityDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    HexColor = x.HexColor,
                })
                .AsNoTracking()
                .ToListAsync(cancellationToken),
        };
    }

    [HttpGet("all")]
    [HasAnyRole(Role.Admin)]
    public async Task<Pageable<UserSalaryAdminDto>> AllAsync(
        [FromQuery] GetApprovedSalariesQuery request,
        CancellationToken cancellationToken)
    {
        return await _mediator.Send(
            request,
            cancellationToken);
    }

    [HttpGet("not-in-stats")]
    [HasAnyRole(Role.Admin)]
    public async Task<Pageable<UserSalaryAdminDto>> AllNotShownInStatsAsync(
        [FromQuery] GetExcludedFromStatsSalariesQuery request,
        CancellationToken cancellationToken)
    {
        return await _mediator.Send(
            request,
            cancellationToken);
    }

    [HttpGet("salaries-adding-trend-chart")]
    [HasAnyRole(Role.Admin)]
    public async Task<AdminChartResponse> AdminChart(
        CancellationToken cancellationToken)
    {
        return await _mediator.Send(
            new GetAdminChartQuery(),
            cancellationToken);
    }

    [HttpGet("chart")]
    public Task<SalariesChartResponse> ChartAsync(
        [FromQuery] GetSalariesChartQuery request,
        CancellationToken cancellationToken)
    {
        return _mediator.Send(
            new GetSalariesChartQuery
            {
                Grade = request.Grade,
                ProfessionsToInclude = new DeveloperProfessionsCollection(request.ProfessionsToInclude).ToList(),
                Cities = request.Cities,
            },
            cancellationToken);
    }

    [HttpPost("")]
    [HasAnyRole]
    public async Task<CreateOrEditSalaryRecordResponse> Create(
        [FromBody] AddSalaryCommand request,
        CancellationToken cancellationToken)
    {
        return await _mediator.Send(
            request,
            cancellationToken);
    }

    [HttpPost("{id:guid}")]
    [HasAnyRole]
    public async Task<CreateOrEditSalaryRecordResponse> Update(
        [FromRoute] Guid id,
        [FromBody] EditSalaryRequest request,
        CancellationToken cancellationToken)
    {
        return await _mediator.Send(
            new UpdateSalaryCommand(
                id,
                request),
            cancellationToken);
    }

    [HttpPost("{id:guid}/approve")]
    [HasAnyRole(Role.Admin)]
    public async Task<IActionResult> Approve(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var salary = await _context.Salaries
                         .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                     ?? throw new ResourceNotFoundException("Salary record not found");

        salary.Approve();
        await _context.SaveChangesAsync(cancellationToken);

        return Ok();
    }

    [HttpPost("{id:guid}/exclude-from-stats")]
    [HasAnyRole(Role.Admin)]
    public async Task<IActionResult> ExcludeFromStats(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var salary = await _context.Salaries
                         .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                     ?? throw new ResourceNotFoundException("Salary record not found");

        salary.ExcludeFromStats();
        await _context.SaveChangesAsync(cancellationToken);

        return Ok();
    }

    [HttpDelete("{id:guid}")]
    [HasAnyRole(Role.Admin)]
    public async Task<IActionResult> Delete(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var salary = await _context.Salaries
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ResourceNotFoundException("Salary record not found");

        _context.Salaries.Remove(salary);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok();
    }
}