using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Warning! this is only sample, may cause problems or some issues
/// </summary>
public class HttpConnecter : MonoBehaviour
{
    private static int unityWebRequestTimeout = 10;
    /*
        StartCoroutine(HttpConnecter.UnityWebSendRequest(
            "",
            HttpMethod.Post,
            new Dictionary<string, string>()
            {
            },
            (string data) =>
            {
            },
            (string error) =>
            {
            },
            (string state) =>
            {
            },
            useTimeout: ,
            dataEncodeEscape: ));



                StartCoroutine(Web.SimplePost(
                    ));



        yield return Web.SimplePost(
                    );

     */

    #region Basic Request
    public static IEnumerator SendRequest(
        string url,
        HttpMethod method,
        Dictionary<string, string> data,
        Action<string> onSuccess,
        Action<string> onError)
    {
        if (Application.platform != RuntimePlatform.WebGLPlayer)
        {
            yield return CsharpSendRequest(url, method, data, onSuccess, onError);
        }
        else
        {
            yield return UnityWebSendRequest(url, method, data, onSuccess, onError);
        }
    }
    /// <summary>
    /// Not works at WebGL
    /// </summary>
    private static IEnumerator CsharpSendRequest(
        string url,
        HttpMethod method,
        Dictionary<string, string> data,
        Action<string> onSuccess,
        Action<string> onError)
    {
        using (HttpClient client = new HttpClient(new HttpClientHandler() { AllowAutoRedirect = false }))
        {
            Task<HttpResponseMessage> requestTask;
            if (method == HttpMethod.Post)
            {
                Dictionary<string, string> parameters = new Dictionary<string, string>();
                foreach (var kvp in data)
                {
                    parameters.Add(kvp.Key, kvp.Value);
                }
                FormUrlEncodedContent encodedContent = new FormUrlEncodedContent(parameters);
                requestTask = client.PostAsync(url, encodedContent);
            }
            else
            {
                url += "?" + string.Join("&", data.Select(pair => $"{pair.Key}={pair.Value}"));
                requestTask = client.GetAsync(url);
            }

            yield return new WaitUntil(() => requestTask.IsCompleted);
            HttpResponseMessage response = requestTask.Result;

            if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400)
            {
                string redirectUrl = response.Headers.Location.ToString();
                Debug.LogError("Redirected URL: " + redirectUrl);
                onError?.Invoke("Redirected URL: " + redirectUrl);
            }
            else if (response.StatusCode == HttpStatusCode.OK)
            {
                Task<string> valueTask = response.Content.ReadAsStringAsync();
                yield return new WaitUntil(() => valueTask.IsCompleted);
                string responseValue = valueTask.Result;
                onSuccess?.Invoke(responseValue);
            }
            else
            {
                Debug.LogError($"{response.StatusCode}");
                onError?.Invoke($"{response.StatusCode}");
            }
        }
    }

    /// <summary>
    /// Works at any platform
    /// </summary>
    /// <param name="result">
    /// Concern about multiple coroutine race condition...<br/>
    /// Use this to make coroutine chain properly<br/>
    /// BUT, don't use with onSuccess
    /// </param>
    public static IEnumerator UnityWebSendRequest(
        string url,
        HttpMethod method,
        Dictionary<string, string> queryParams,
        Action<string> onSuccess,
        Action<string> onError,
        Action<string> stateEvent = null,
        bool useTimeout = false,
        bool dataEncodeEscape = true,
        RequestResultWrap result = null)
    {
        UnityWebRequest request = null;
        try
        {
            if (method == HttpMethod.Post)
            {
                WWWForm form = new WWWForm();
                if (queryParams != null)
                {
                    foreach (var kvp in queryParams)
                    {
                        form.AddField(kvp.Key, kvp.Value);
                    }
                }
                request = UnityWebRequest.Post(url, form);
            }
            else
            {
                if (queryParams != null)
                {
                    if (dataEncodeEscape)
                    {
                        url += "?" + string.Join("&", queryParams.Select(pair => $"{UnityWebRequest.EscapeURL(pair.Key)}={UnityWebRequest.EscapeURL(pair.Value)}"));
                    }
                    else
                    {
                        url += "?" + string.Join("&", queryParams.Select(pair => $"{pair.Key}={pair.Value}"));
                    }
                }
                request = UnityWebRequest.Get(url);
            }

            //request.certificateHandler = new BypassCertificate();
            request.redirectLimit = 0;
            if (useTimeout)
            {
                request.timeout = unityWebRequestTimeout;
            }
        }
        catch (Exception ex)
        {
            onError?.Invoke($"Error: {ex.Message}");
            request?.Dispose();
        }

        yield return request.SendWebRequest();
        if (request.result == UnityWebRequest.Result.Success && result != null)
        {
            result.success = true;
            result.data = request.downloadHandler.text;
        }

        try
        {
            switch (request.result)
            {
                case UnityWebRequest.Result.Success:
                    {
                        onSuccess?.Invoke(request.downloadHandler.text);
                        stateEvent?.Invoke("Success");
                    }
                    break;

                case UnityWebRequest.Result.InProgress:
                    {
                        stateEvent?.Invoke("InProgress");
                    }
                    break;

                case UnityWebRequest.Result.ConnectionError:
                    {
                        stateEvent?.Invoke("ConnectionError");
                    }
                    break;

                case UnityWebRequest.Result.ProtocolError:
                    {
                        stateEvent?.Invoke("ProtocolError");
                    }
                    break;

                case UnityWebRequest.Result.DataProcessingError:
                    {
                        stateEvent?.Invoke("DataProcessingError");
                    }
                    break;

                default:
                    Debug.LogError(request.result);
                    break;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(request.error);
                onError?.Invoke(request.error);
            }
        }
        catch (Exception ex)
        {
            onError?.Invoke($"Error: {ex.Message}");
        }
        request?.Dispose();
    }

    public static IEnumerator SimplePost(
        string url,
        string key,
        string inputData,
        Action<string> onSuccess = null,
        RequestResultWrap result = null)
    {
        yield return UnityWebSendRequest(
            url,
            HttpMethod.Post,
            new Dictionary<string, string>()
            {
                    { key, inputData }
            },
            onSuccess,
            null,
            result: result);
    }

    /// <summary>
    /// TODO : need to convert IEnumerator for WebGL
    /// </summary>
    public async Task<string> DownloadAndSaveFileFromWeb(string url)
    {
        string savePath;
        string fileName = Path.GetFileName(url);

        using (var request = UnityWebRequest.Get(url))
        {
            // Same as HttpMethod.Get?
            //request.method = UnityWebRequest.kHttpVerbGET;
            UnityWebRequestAsyncOperation asyncOperation = request.SendWebRequest();
            while (!asyncOperation.isDone) await Task.Yield();

            //check for errors
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(request.error);
                savePath = null;
            }
            else
            {
                savePath = Path.Combine(Application.streamingAssetsPath, fileName);
                File.WriteAllBytes(savePath, request.downloadHandler.data);

#if UNITY_EDITOR
                    UnityEditor.AssetDatabase.Refresh();
#endif
            }

            request.Dispose();
        }

        return savePath;
    }
    #endregion
}

public class RequestResultWrap
{
    public bool success;
    public string data;

    public RequestResultWrap()
    {
        success = false;
        data = "";
    }
}