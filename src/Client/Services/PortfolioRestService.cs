using Arisoul.Core.Root.Models;
using Arisoul.T212.Models;

namespace Arisoul.T212.Client.Services;

public class PortfolioRestService(IT212ClientOptions clientOptions)
    : T212BaseRestService(clientOptions)
{
    public async Task<Result<IEnumerable<PositionModel>>> GetOpenPositionsAsync()
    {
        return await GetItems<PositionModel>($"/api/v{ApiVersion}/equity/portfolio");
    }
}
