# BlobTraceListener

Writes diagnostic trace messages to Azure Blob Storage.

## Usage

Basic

```csharp
var listener = new BlobTraceListener(
    config["AZURE_STORAGE_CONNECTIONSTRING"],
    config["AZURE_STORAGE_CONTAINER_NAME"]);

listener.WriteLine("Hello world");
listener.Flush();
```

With options

```csharp
var listener = new BlobTraceListener(
    config["AZURE_STORAGE_CONNECTIONSTRING"],
    config["AZURE_STORAGE_CONTAINER_NAME"],
    string.Empty,
    new BlobTraceListenerOptions
    {
        BackgroundScheduleTimeoutMs = 4000,
        FilenameFormat = "yyyy/MM/dd/HH\\.\\l\\o\\g",
        MaxLogMessagesToKeep = 20000
    });

listener.WriteLine("Hello world!");
listener.Flush();
```

## Links and references

TextWriterTraceListener.cs: <https://github.com/dotnet/runtime/blob/master/src/libraries/System.Diagnostics.TextWriterTraceListener/src/System/Diagnostics/TextWriterTraceListener.cs>

Send log data to Azure Monitor with the HTTP Data Collector API (public preview): <https://docs.microsoft.com/en-us/azure/azure-monitor/platform/data-collector-api>

How to: Create and Initialize Trace Listeners <https://docs.microsoft.com/en-us/dotnet/framework/debug-trace-profile/how-to-create-and-initialize-trace-listeners>

Trace Listener class: <https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.tracelistener?view=netframework-4.8>

Capturing Azure Webapp application log in Azure Log Analytics: <https://blog.adamfurmanek.pl/2017/06/10/capturing-azure-webapp-application-log-in-azure-log-analytics/>

`ApplicationInsightsTraceListener.cs`: <https://github.com/microsoft/ApplicationInsights-dotnet/blob/a26a43f012ef6afca563b43b16ab019698a3f062/LOGGING/src/TraceListener/ApplicationInsightsTraceListener.cs>

TraceListener Class: <https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.tracelistener?view=netcore-3.1>

### Blob storage

About Append Blobs: <https://docs.microsoft.com/en-us/rest/api/storageservices/understanding-block-blobs--append-blobs--and-page-blobs#about-append-blobs>

### HostedService (ASP.NET Core)

Background tasks with hosted services in ASP.NET Core: <https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-3.1&tabs=visual-studio>

Hosted services in ASP.Net Core: <https://danpatrascu.com/hosted-services-in-asp-net-core/>

