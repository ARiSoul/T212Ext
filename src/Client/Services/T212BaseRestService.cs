using Arisoul.Core.BaseDataServices;
using Arisoul.Core.Root.Models;
using RestSharp;

namespace Arisoul.T212.Client.Services;

public class T212BaseRestService(IT212ClientOptions clientOptions) 
    : BaseRestService(clientOptions.BaseAddress)
{
    private readonly IT212ClientOptions _clientOptions = clientOptions;

    protected string ApiVersion => _clientOptions.ApiVersion;

    public async Task<Result<T>> GetItem<T>(string endpoint)
       where T : class
    {
        RestRequest request = new(endpoint, RestSharp.Method.Get);
        AddOrUpdateRequestHeaders(request);

        var response = await ExecuteRequestAndGetResponse<T>(request);

        if (response.Succeeded)
            return response.Value!;

        return response.Error!;
    }

    public async Task<Result<IEnumerable<T>>> GetItems<T>(string endpoint)
        where T : class
    {
        RestRequest request = new(endpoint, RestSharp.Method.Get);
        AddOrUpdateRequestHeaders(request);

        var response = await ExecuteRequestAndGetResponse<IEnumerable<T>>(request);

        if (response.Succeeded)
            return Result<IEnumerable<T>>.Success(response.Value);

        return response.Error!;
    }

    private void AddOrUpdateRequestHeaders(RestRequest request)
    {
        try
        {
            request.AddOrUpdateHeader(KnownHeaders.Authorization, _clientOptions.ApiKey);
        }
        catch (Exception)
        {
            throw;
        }
    }
}
