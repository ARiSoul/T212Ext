using Arisoul.Core.Root.Models;
using Arisoul.T212.Models;

namespace Arisoul.T212.Client.Services;

public class AccountRestService(IT212ClientOptions clientOptions) 
    : T212BaseRestService(clientOptions)
{
    public async Task<Result<Account>> GetAccountMetadataAsync()
    {
        return await GetItem<Account>($"api/v{ApiVersion}/equity/account/info");
    }

    public async Task<Result<AccountCash>> GetAccountCashAsync()
    {
        return await GetItem<AccountCash>($"api/v{ApiVersion}/equity/account/cash");
    }
}
