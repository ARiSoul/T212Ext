using Arisoul.Core.Root.Models;
using Arisoul.T212.Models;

namespace Arisoul.T212.Client.Services;

public class InstrumentRestService(IT212ClientOptions clientOptions)
    : T212BaseRestService(clientOptions)
{
    public async Task<Result<IEnumerable<Instrument>>> GetInstrumentsAsync()
    {
        return await GetItems<Instrument>($"/api/v{ApiVersion}/equity/metadata/instruments");
    }
}
