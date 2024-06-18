using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace TechInterviewer.Features.Telegram.AdminTools
{
    public class AdminCommandHandler : IRequestHandler<AdminToolsCommand, string>
    {
        public async Task<string> Handle(AdminToolsCommand request, CancellationToken cancellationToken)
        {

            return "Ok";
        }
    }
}
