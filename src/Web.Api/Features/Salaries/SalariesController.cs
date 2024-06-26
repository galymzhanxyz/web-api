﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domain.Enums;
using Domain.ValueObjects.Pagination;
using Infrastructure.Database;
using Infrastructure.Salaries;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TechInterviewer.Features.Salaries.AddSalary;
using TechInterviewer.Features.Salaries.Admin.GetApprovedSalaries;
using TechInterviewer.Features.Salaries.Admin.GetExcludedFromStatsSalaries;
using TechInterviewer.Features.Salaries.ApproveSalary;
using TechInterviewer.Features.Salaries.DeleteSalary;
using TechInterviewer.Features.Salaries.ExcludeFromStats;
using TechInterviewer.Features.Salaries.ExportCsv;
using TechInterviewer.Features.Salaries.GetAdminChart;
using TechInterviewer.Features.Salaries.GetSalaries;
using TechInterviewer.Features.Salaries.GetSalariesChart;
using TechInterviewer.Features.Salaries.GetSalariesChart.Charts;
using TechInterviewer.Features.Salaries.GetSelectBoxItems;
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
        return await _mediator.Send(new GetSelectBoxItemsQuery(), cancellationToken);
    }

    [HttpGet("all")]
    [HasAnyRole(Role.Admin)]
    public async Task<Pageable<UserSalaryAdminDto>> GetAllAdmin(
        [FromQuery] GetApprovedSalariesQuery request,
        CancellationToken cancellationToken)
    {
        return await _mediator.Send(
            request,
            cancellationToken);
    }

    [HttpGet("not-in-stats")]
    [HasAnyRole(Role.Admin)]
    public async Task<Pageable<UserSalaryAdminDto>> GetAllNotShownInStats(
        [FromQuery] GetExcludedFromStatsSalariesQuery request,
        CancellationToken cancellationToken)
    {
        return await _mediator.Send(
            request,
            cancellationToken);
    }

    [HttpGet("salaries-adding-trend-chart")]
    public async Task<GetAddingTrendChartResponse> GetAddingTrendChart(
        [FromQuery] GetAddingTrendChartQuery request,
        CancellationToken cancellationToken)
    {
        return await _mediator.Send(
            new GetAddingTrendChartQuery
            {
                Grade = request.Grade,
                ProfessionsToInclude = new DeveloperProfessionsCollection(request.ProfessionsToInclude)
                    .ToList(),
                Cities = request.Cities,
            },
            cancellationToken);
    }

    [HttpGet("chart")]
    public Task<SalariesChartResponse> GetChart(
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

    [HttpGet("")]
    public Task<Pageable<UserSalaryDto>> AllForPublic(
        [FromQuery] GetSalariesPaginatedQuery request,
        CancellationToken cancellationToken)
    {
        return _mediator.Send(
            new GetSalariesPaginatedQuery
            {
                Page = request.Page,
                PageSize = request.PageSize,
                Grade = request.Grade,
                ProfessionsToInclude = new DeveloperProfessionsCollection(request.ProfessionsToInclude).ToList(),
                Cities = request.Cities,
            },
            cancellationToken);
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(
            new ExportCsvQuery(),
            cancellationToken);

        return File(response.GetAsByteArray(), response.FileContentType, response.Filename);
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
        await _mediator.Send(new ApproveSalaryCommand(id), cancellationToken);
        return Ok();
    }

    [HttpPost("{id:guid}/exclude-from-stats")]
    [HasAnyRole(Role.Admin)]
    public async Task<IActionResult> ExcludeFromStats(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new ExcludeFromStatsCommand(id), cancellationToken);
        return Ok();
    }

    [HttpDelete("{id:guid}")]
    [HasAnyRole(Role.Admin)]
    public async Task<IActionResult> Delete(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteSalaryCommand(id), cancellationToken);
        return Ok();
    }
}