using System.Net.Http.Headers;
using System.Text.Json;

namespace SeeedKK.WioLink;

public class WioLinkService
{
    public WioLinkService(string wioLinkServer)
    {
        _WioLinkServer = wioLinkServer;
    }

    public void SetToken(string? token)
    {
        _Client.DefaultRequestHeaders.Authorization = token != null ? new AuthenticationHeaderValue("token", token) : null;
    }

    #region User management APIs

    public async Task<UserLoginResponse> UserLoginAsync(string username, string password)
    {
        var values = new Dictionary<string, string>
            {
                { "email", username },
                { "password", password }
            };

        using var response = await _Client.PostAsync($"{_WioLinkServer}/v1/user/login", new FormUrlEncodedContent(values));
        var responseString = await response.Content.ReadAsStringAsync();

        var responseObject = JsonSerializer.Deserialize<UserLoginResponse>(responseString);
        if (responseObject == null ||
            responseObject.error != null ||
            string.IsNullOrEmpty(responseObject.token) ||
            string.IsNullOrEmpty(responseObject.user_id))
        {
            throw new ApplicationException(responseObject?.error);
        }

        return responseObject!;
    }

    public class UserLoginResponse : ResponseBase
    {
        public string? token { get; set; }
        public string? user_id { get; set; }
    }

    #endregion

    #region Nodes management APIs

    public async Task<CreateNodeResponse> CreateNodeAsync(string name, string board)
    {
        var values = new Dictionary<string, string>
            {
                { "name", name },
                { "board", board }
            };

        using var response = await _Client.PostAsync($"{_WioLinkServer}/v1/nodes/create", new FormUrlEncodedContent(values));
        var responseString = await response.Content.ReadAsStringAsync();

        var responseObject = JsonSerializer.Deserialize<CreateNodeResponse>(responseString);
        if (responseObject == null ||
            responseObject.error != null ||
            string.IsNullOrEmpty(responseObject.node_key) ||
            string.IsNullOrEmpty(responseObject.node_sn))
        {
            throw new ApplicationException(responseObject?.error);
        }

        return responseObject!;
    }

    public class CreateNodeResponse : ResponseBase
    {
        public string? node_key { get; set; }
        public string? node_sn { get; set; }
    }

    public async Task<NodeListResponse> ListAllNodesOfUserAsync()
    {
        using var response = await _Client.GetAsync($"{_WioLinkServer}/v1/nodes/list");
        var responseString = await response.Content.ReadAsStringAsync();

        var responseObject = JsonSerializer.Deserialize<NodeListResponse>(responseString);
        if (responseObject == null ||
            responseObject.error != null ||
            responseObject.nodes == null)
        {
            throw new ApplicationException(responseObject?.error);
        }

        return responseObject!;
    }

    public class Node
    {
        public string name { get; set; } = string.Empty;
        public string node_key { get; set; } = string.Empty;
        public string node_sn { get; set; } = string.Empty;
        public string? dataxserver { get; set; }
        public string board { get; set; } = string.Empty;
        public bool online { get; set; }
    }

    public class NodeListResponse : ResponseBase
    {
        public List<Node>? nodes { get; set; }
    }

    public async Task RenameNodeAsync(string node_sn, string name)
    {
        var values = new Dictionary<string, string>
            {
                { "node_sn", node_sn },
                { "name", name }
            };

        using var response = await _Client.PostAsync($"{_WioLinkServer}/v1/nodes/rename", new FormUrlEncodedContent(values));
        var responseString = await response.Content.ReadAsStringAsync();

        var responseObject = JsonSerializer.Deserialize<ResultResponse>(responseString);
        if (responseObject == null ||
            responseObject.error != null ||
            responseObject.result != "ok")
        {
            throw new ApplicationException(responseObject?.error ?? responseObject?.result);
        }
    }

    public async Task DeleteNodeAsync(string node_sn)
    {
        var values = new Dictionary<string, string>
            {
                { "node_sn", node_sn }
            };

        using var response = await _Client.PostAsync($"{_WioLinkServer}/v1/nodes/delete", new FormUrlEncodedContent(values));
        var responseString = await response.Content.ReadAsStringAsync();

        var responseObject = JsonSerializer.Deserialize<ResultResponse>(responseString);
        if (responseObject == null ||
            responseObject.error != null ||
            responseObject.result != "ok")
        {
            throw new ApplicationException(responseObject?.error ?? responseObject?.result);
        }
    }

    #endregion

    public string BoardWioLink1_0 { get; } = "Wio Link v1.0";
    public string BoardWioNode1_0 { get; } = "Wio Node v1.0";

    public string TemporaryNodeName { get; } = "node000";

    public class ResponseBase
    {
        public string? error { get; set; }
    }

    private class ResultResponse : ResponseBase
    {
        public string? result { get; set; }
    }

    private string _WioLinkServer = string.Empty;
    private HttpClient _Client = new HttpClient();

}
