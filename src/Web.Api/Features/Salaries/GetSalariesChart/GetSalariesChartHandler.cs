﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domain.Entities.Enums;
using Domain.Entities.Salaries;
using Infrastructure.Authentication.Contracts;
using Infrastructure.Database;
using Infrastructure.Salaries;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TechInterviewer.Features.Salaries.GetSalariesChart.Charts;
using TechInterviewer.Features.Salaries.Models;
using TechInterviewer.Features.Surveys.Services;

namespace TechInterviewer.Features.Salaries.GetSalariesChart
{
    public class GetSalariesChartHandler : IRequestHandler<GetSalariesChartQuery, SalariesChartResponse>
    {
        public static readonly List<DeveloperGrade> GradesToBeUsedInChart = new ()
        {
            DeveloperGrade.Junior,
            DeveloperGrade.Middle,
            DeveloperGrade.Senior,
            DeveloperGrade.Lead,
        };

        private readonly IAuthorization _auth;
        private readonly DatabaseContext _context;

        public GetSalariesChartHandler(
            IAuthorization auth,
            DatabaseContext context)
        {
            _auth = auth;
            _context = context;
        }

        public async Task<SalariesChartResponse> Handle(
            ISalariesChartQueryParams request,
            CancellationToken cancellationToken)
        {
            var currentUser = await _auth.CurrentUserOrNullAsync(cancellationToken);

            var userSalariesForLastYear = new List<UserSalary>();

            var salariesQuery = new SalariesForChartQuery(
                _context,
                request);

            if (currentUser != null)
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(x => x.Id == currentUser.Id, cancellationToken);

                userSalariesForLastYear = await _context.Salaries
                    .Where(x => x.UserId == user.Id)
                    .Where(x => x.Year == salariesQuery.CurrentQuarter.Year || x.Year == salariesQuery.CurrentQuarter.Year - 1)
                    .AsNoTracking()
                    .OrderByDescending(x => x.Year)
                    .ThenByDescending(x => x.Quarter)
                    .ToListAsync(cancellationToken);
            }

            var query = salariesQuery.ToQueryable();
            if (currentUser == null || !userSalariesForLastYear.Any())
            {
                var salaryValues = await query
                    .Select(x => new { x.Company, x.Value })
                    .ToListAsync(cancellationToken);

                var totalCount = await query.CountAsync(cancellationToken);
                return SalariesChartResponse.RequireOwnSalary(
                    salaryValues.Select(x => (x.Company, x.Value)).ToList(),
                    totalCount,
                    true,
                    currentUser is not null);
            }

            var salaries = await query.ToListAsync(cancellationToken);
            var hasSurveyRecentReply = await new SalariesSurveyUserService(_context)
                .HasFilledSurveyAsync(currentUser, cancellationToken);

            return new SalariesChartResponse(
                salaries,
                new UserSalaryAdminDto(userSalariesForLastYear.First()),
                hasSurveyRecentReply,
                salariesQuery.SalaryAddedEdge,
                DateTimeOffset.Now,
                salaries.Count);
        }

        public Task<SalariesChartResponse> Handle(
            GetSalariesChartQuery request,
            CancellationToken cancellationToken)
        {
            return Handle((ISalariesChartQueryParams)request, cancellationToken);
        }
    }
}