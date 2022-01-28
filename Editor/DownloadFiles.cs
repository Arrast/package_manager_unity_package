using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using UnityEngine;

public class Package
{
    public string Repository;
    public string Branch;
}

public static class DownloadFiles
{
    private static string ExternalModulesDirectory 
    {
        get { return Path.Combine(Application.dataPath, Const.ExternalModulesSubdir); }
    }
    private static string gitRepositoryUrlTemplate = "https://api.github.com/repos/{0}/{1}/{2}";

    public static HttpClient BuildHttpClient(string owner, string token)
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(owner, "0.1"));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", token);
        return httpClient;
    }

    public static async Task<HttpContent> RunHttpRequest(HttpClient httpClient, string owner, string token, string repo, string branch, string format, string fileName = "")
    {
        if (httpClient == null) { return null; }

        var url = GetArchieveUrl(owner, repo, branch, format, fileName);
        Debug.Log($"Calling: {url}");
        var resp = await httpClient.GetAsync(url);

        if (resp.StatusCode != System.Net.HttpStatusCode.OK)
        {
            throw new System.Exception($"error happens when downloading the {url}, statusCode={resp.StatusCode}");
        }
        else if (resp.Content != null)
        {
            return resp.Content;
        }


        return null;
    }

    public static async Task DownloadZipArchieveAsync(string owner, string token, List<Package> packages)
    {
        if(packages == null) { return; }

        // We only need to create the client once
        var httpClient = BuildHttpClient(owner, token);
        foreach (var package in packages)
        {
            var content = await RunHttpRequest(httpClient, owner, token, package.Repository, package.Branch, Const.ZipballGithubFileType);
            if (content != null)
            {
                await ProcessExtractZipFile(content, package.Repository);
            }
        }
    }

    public static async Task ProcessExtractZipFile(HttpContent content, string folderName)
    {
        var stream = await content.ReadAsStreamAsync();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            if (!Directory.Exists(ExternalModulesDirectory))
            {
                Directory.CreateDirectory(ExternalModulesDirectory);
            }
            zip.ExtractToDirectory(ExternalModulesDirectory, true);
            if(zip.Entries != null && zip.Entries.Count > 0)
            {
                var zipEntry = zip.Entries[0];
                if(Directory.Exists(Path.Combine(ExternalModulesDirectory, zipEntry.FullName)))
                {
                    Directory.Move(Path.Combine(ExternalModulesDirectory, zipEntry.FullName), Path.Combine(ExternalModulesDirectory, folderName));
                }
            }
            zip.Dispose();
        }
    }


    public static async Task<Dependencies> DownloadDependencyFile(string owner, string token, string repo, string branch)
    {
        var httpClient = BuildHttpClient(owner, token);
        var content = await RunHttpRequest(httpClient, owner, token, repo, branch, Const.ConstantsGithubFileType, $"{Const.DependenciesFolder}/{Const.DependenciesFileName}");
        if (content != null)
        {
            var result = await content.ReadAsStringAsync();
            JObject castedObject = (JObject)JsonConvert.DeserializeObject(result);
            if (castedObject != null && (string)castedObject["type"] == "file")
            {
                var file = await httpClient.GetAsync((string)castedObject["download_url"]);
                if (file.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    var dependencyJson = await file.Content.ReadAsStringAsync();
                    var dependencies = JsonConvert.DeserializeObject<Dependencies>(dependencyJson);
                    return dependencies;
                }
            }
        }
        return null;
    }

    public static string GetArchieveUrl(string owner, string repo, string branch, string format, string fileName)
    {
        string url = string.Format(gitRepositoryUrlTemplate, owner, repo, format);
        if (!string.IsNullOrEmpty(fileName))
        {
            url = $"{url}/{fileName}?ref={branch}";
        }
        else
        {
            url += $"/{branch}";
        }
        return url;
    }
}
