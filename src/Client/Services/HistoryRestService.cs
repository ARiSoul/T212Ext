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

    public async Task<Result<PaginatedResponseHistoryOrder>> GetOrdersAsync(CursorLimitTickerRequestParams? requestParams)
    {
        requestParams ??= new CursorLimitTickerRequestParams();
        return await GetItem<PaginatedResponseHistoryOrder>($"/api/v{ApiVersion}/equity/history/orders{requestParams}");
    }

    public override async Task<Result<T>> ExecuteRequestAndGetResponse<T>(RestRequest request)
    {
        RestResponse restResponse = await Client.ExecuteAsync(request).ConfigureAwait(continueOnCapturedContext: false);
        if (restResponse.IsSuccessful)
        {
            var result = await Client.Deserialize<T>(restResponse, default).ConfigureAwait(false);
            return result.Data!;
        }

        if (restResponse.StatusCode == HttpStatusCode.Unauthorized)
            return new ResultUnauthorizedError();

        if (restResponse.StatusCode == HttpStatusCode.NotFound)
            return new ResultNotFoundError(restResponse.Content!);

        if (!string.IsNullOrEmpty(restResponse.Content))
        {
            ClientProblemDetails? clientProblemDetails = JsonSerializer.Deserialize<ClientProblemDetails>(restResponse.Content);
            if (clientProblemDetails != null && !string.IsNullOrWhiteSpace(clientProblemDetails.Title))
                return ResultValidationError.FromClientProblemDetails(clientProblemDetails);

            return new ResultValidationError(restResponse.ErrorMessage ?? restResponse.ErrorException!.Message, restResponse.Content.ToString());
        }

        return new ResultValidationError(restResponse.ErrorMessage ?? restResponse.ErrorException!.Message, "UnknownDetails");
    }
}
