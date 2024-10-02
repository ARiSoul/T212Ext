using Arisoul.Core;
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

        if (response.Success)
            return Result<T>.Succeeded(response.Value!);

        if (response.IsUnauthorized)
            return Result<T>.Failed(LOC.Unauthorized);

        return Result<T>.Failed(response.Message!, response.ErrorDetails);
    }

    public async Task<Result<IEnumerable<T>>> GetItems<T>(string endpoint)
        where T : class
    {
        RestRequest request = new(endpoint, RestSharp.Method.Get);
        AddOrUpdateRequestHeaders(request);

        var response = await ExecuteRequestAndGetResponse<IEnumerable<T>>(request);

        if (response.Success)
            return Result<IEnumerable<T>>.Succeeded(response.Value!);

        if (response.IsUnauthorized)
            return Result<IEnumerable<T>>.Failed(LOC.Unauthorized);

        return Result<IEnumerable<T>>.Failed(response.Message!, response.ErrorDetails);
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
