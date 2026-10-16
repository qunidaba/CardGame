using System.Collections.Generic;
using YooAsset;

/// <summary>
/// YooAsset 远端资源地址服务：把资源文件名拼成远端 URL。
/// 传入多个 host 时按顺序作为候选（第一个失败会尝试下一个）。
/// </summary>
public class RemoteService : IRemoteService
{
    private readonly string[] _hosts;

    public RemoteService(params string[] hosts)
    {
        _hosts = hosts;
    }

    public IReadOnlyList<string> GetRemoteUrls(string fileName)
    {
        var list = new List<string>();
        if (_hosts != null)
        {
            foreach (var h in _hosts)
            {
                if (string.IsNullOrEmpty(h)) continue;
                list.Add(h.TrimEnd('/') + "/" + fileName);
            }
        }
        return list;
    }
}
