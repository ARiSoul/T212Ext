using Arisoul.Core.Root.Models;
using Arisoul.Core.Root.Models.Results;
using Arisoul.T212.Models;
using RestSharp;
using System.Net;
using System.Text.Json;

namespace Arisoul.T212.Client.Services;

public class HistoryRestService(IT212ClientOptions clientOptions)
    : T212BaseRestService(clientOptions)
{
    public async Task<Result<PaginatedResponseHistoryDividendItem>> GetDividendsAsync(CursorLimitTickerRequestParams? requestParams)
    {
        requestParams ??= new CursorLimitTickerRequestParams();

        return await GetItem<PaginatedResponseHistoryDividendItem>($"/api/v{ApiVersion}/history/dividends{requestParams}");
    }

    public override async Task<Result<T>> ExecuteRequestAndGetResponse<T>(RestRequest request)
    {
        RestResponse restResponse = await Client.ExecuteAsync(request).ConfigureAwait(continueOnCapturedContext: false);
        if (restResponse.IsSuccessful)
        {
            return Result<T>.Succeeded(Client.Deserialize<T>(restResponse).Data);
        }

        if (restResponse.StatusCode == HttpStatusCode.Unauthorized)
        {
            return Result<T>.Unauthorized();
        }

        if (!string.IsNullOrEmpty(restResponse.Content))
        {
            ClientProblemDetails clientProblemDetails = JsonSerializer.Deserialize<ClientProblemDetails>(restResponse.Content);
            if (clientProblemDetails != null && !string.IsNullOrWhiteSpace(clientProblemDetails.Title))
            {
                Dictionary<string, List<string>> dictionary = new Dictionary<string, List<string>>();
                foreach (KeyValuePair<string, object> error in clientProblemDetails.Errors)
                {
                    dictionary.Add(error.Key, JsonSerializer.Deserialize<List<string>>(error.Value.ToString()));
                }

                return Result<T>.Failed(clientProblemDetails.Title, dictionary);
            }

            return Result<T>.Failed(restResponse.ErrorMessage ?? restResponse.ErrorException.Message, restResponse.Content.ToString());
        }

        return Result<T>.Failed(restResponse.ErrorMessage ?? restResponse.ErrorException.Message);
    }
}
