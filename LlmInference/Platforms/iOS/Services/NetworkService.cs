using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Foundation;

namespace LlmInference;

public sealed class NetworkService
{
    public static NetworkService Shared { get; } = new();

    private NetworkService() { }

    public async Task<NSDictionary> PostRequestAsync(NSUrl url, NSData body, NSDictionary headers)
    {
        using var request = new NSMutableUrlRequest(url)
        {
            HttpMethod = "POST",
            Body = body,
            Headers = headers
        };

        NSData data;
        NSUrlResponse response;
        try
        {
            var session = await NSUrlSession.SharedSession.CreateDataTaskAsync(request);
            response = session.Response;
            data = session.Data;
        }
        catch (Exception ex)
        {
            throw new NetworkException(NetworkError.RequestFailed, ex);
        }

        var httpResponse = response as NSHttpUrlResponse;
        if (httpResponse == null)
        {
            throw new NetworkException(NetworkError.NoResponse);
        }

        Validate(httpResponse);

        var json = NSJsonSerialization.Deserialize(data, 0, out var parseError) as NSDictionary;
        if (json == null || parseError != null)
        {
            throw new NetworkException(NetworkError.InvalidJson, parseError?.LocalizedDescription);
        }

        return json;
    }

    // Downloads the file at `sourceUrl`and saves it to the `destinationURL`.  Returns an cancellable
    // `IAsyncEnumerable`
    public async IAsyncEnumerable<DownloadEvent> DownloadFileAsync(NSUrl from, NSUrl to, NSDictionary headers,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var sourceUrl = from;
        var destinationUrl = to;

        using var request = new NSMutableUrlRequest(sourceUrl)
        {
            HttpMethod = "GET",
            Headers = headers
        };

        var channel = Channel.CreateUnbounded<DownloadEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });

        var downloadTask = NSUrlSession.SharedSession.CreateDownloadTask(request, (tempUrl, response, error) =>
        {
            if (error != null)
            {
                channel.Writer.TryComplete(new NetworkException(NetworkError.RequestFailed, error.LocalizedDescription));
                return;
            }

            var httpResponse = response as NSHttpUrlResponse;
            if (httpResponse == null || tempUrl == null)
            {
                channel.Writer.TryComplete(new NetworkException(NetworkError.NoResponse));
                return;
            }

            try
            {
                Validate(httpResponse);
            }
            catch (Exception ex)
            {
                channel.Writer.TryComplete(ex);
            }

            try
            {
                MoveFile(tempUrl, destinationUrl);
                channel.Writer.TryWrite(new DownloadEvent.Progress(100.0));
                channel.Writer.TryWrite(new DownloadEvent.Completed());
                channel.Writer.TryComplete();
            }
            catch (Exception ex)
            {
                channel.Writer.TryComplete(new NetworkException(NetworkError.PostprocessingFailed, ex));
            }
        });

        var observation = downloadTask.Progress.AddObserver("fractionCompleted", NSKeyValueObservingOptions.New, _ =>
        {
            var percentage = downloadTask.Progress.FractionCompleted * 100.0;
            channel.Writer.TryWrite(new DownloadEvent.Progress(percentage));
        });

        // Handle cancellation
        using var registration = cancellationToken.Register(() =>
        {
            downloadTask.Cancel();
            observation.Dispose();
            channel.Writer.TryComplete(new OperationCanceledException(cancellationToken));
        });

        // Start the download
        downloadTask.Resume();

        await foreach (var downloadEvent in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return downloadEvent;
        }
    }

    // Utility to move file from source to destination URL.
    private static void MoveFile(NSUrl sourceUrl, NSUrl destinationUrl)
    {
        var fileManager = NSFileManager.DefaultManager;
        var directoryUrl = destinationUrl.RemoveLastPathComponent();

        fileManager.CreateDirectory(directoryUrl, true, null, out var createError);
        if (createError != null)
        {
            throw new NetworkException(NetworkError.PostprocessingFailed, createError.LocalizedDescription);
        }

        if (fileManager.FileExists(destinationUrl.Path))
        {
            fileManager.Remove(destinationUrl.Path, out var removeError);
            if (removeError != null)
            {
                throw new NetworkException(NetworkError.PostprocessingFailed, removeError.LocalizedDescription);
            }
        }

        fileManager.Move(sourceUrl.Path, destinationUrl.Path, out var moveError);
        if (moveError != null)
        {
            throw new NetworkException(NetworkError.PostprocessingFailed, moveError.LocalizedDescription);
        }
    }

    private static void Validate(NSHttpUrlResponse httpResponse)
    {
        var message = NSHttpUrlResponse.LocalizedStringForStatusCode((int)httpResponse.StatusCode);
        var statusCode = (int)httpResponse.StatusCode;

        if (statusCode == 401)
        {
            throw new NetworkException(NetworkError.Unauthorized, message);
        }

        if (statusCode == 403)
        {
            throw new NetworkException(NetworkError.Forbidden, message);
        }

        if (statusCode < 200 || statusCode > 299)
        {
            throw new NetworkException(NetworkError.InvalidResponseCode, message);
        }
    }

    // MARK: - Network Errors
    public enum NetworkError
    {
        InvalidURL,
        Unauthorized,
        Forbidden,
        InvalidResponseCode,
        InvalidJson,
        NoResponse,
        PostprocessingFailed,
        RequestFailed
    }

    public class NetworkException : Exception
    {
        public NetworkError Error { get; }
        
        public string Response { get; }
        public Exception UnderlyingError { get; }

        public NetworkException(NetworkError error)
        {
            Error = error;
        }

        public NetworkException(NetworkError error, string response)
        {
            Error = error;
            Response = response;
        }

        public NetworkException(NetworkError error, Exception underlyingError)
        {
            Error= error;
            UnderlyingError = underlyingError;
        }

        public string ErrorDescription => Error switch
        {
            NetworkError.InvalidURL => "Invalid URL",
            NetworkError.Unauthorized => "Unauthorized Request",
            NetworkError.Forbidden => "Forbidden Request",
            NetworkError.InvalidResponseCode => "Invalid Server Response",
            NetworkError.InvalidJson => "Invalid JSON Response",
            NetworkError.NoResponse => "No Response Received",
            NetworkError.PostprocessingFailed => "Post processing failed.",
            NetworkError.RequestFailed => "Request Failed",
            _ => ""
        };

        public string FailureReason => Error switch
        {
            NetworkError.InvalidURL => "The request URL provided was invalid.",
            NetworkError.Unauthorized => "Authentication failed (401). " + Response,
            NetworkError.Forbidden => "Access denied (403). " + Response,
            NetworkError.InvalidResponseCode => "The server returned an unexpected status code. " + Response,
            NetworkError.InvalidJson => "The server's response could not be parsed as valid JSON.",
            NetworkError.NoResponse => "No response was received from the server.",
            NetworkError.PostprocessingFailed => "Some error occurred while handling the request. Error: " + UnderlyingError?.Message,
            NetworkError.RequestFailed => "The file download could not be initiated. Error: " + UnderlyingError?.Message,
            _ => ""
        };
    }

    // MARK: - Download Events
    public record DownloadEvent
    {
        public enum DownloadEnum
        {
            Progress,
            Completed
        }

        public record Progress(double progress) : DownloadEvent;
        public record Completed : DownloadEvent;
    }
}
