# RobustDownload.NET 🚀

**Your Fallback Hero & Swiss Army Knife for HTTP(S) Downloads in Legacy .NET Applications**

Keep your legacy .NET apps connected! Reliable HTTPS downloads using curl/wget/PowerShell wrappers to overcome TLS errors.

[![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.0+-blue.svg)](https://dotnet.microsoft.com/download/dotnet-framework)
[![License](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](https://www.apache.org/licenses/LICENSE-2.0)

## The Problem: When .NET Just Won't Connect 😩

Have you ever stared in frustration at this error message?

```
System.Net.WebException: The request was aborted: Could not create SSL/TLS secure channel.
```

If you maintain applications built on older .NET Framework versions, you're probably nodding vigorously right now. This error is the bane of developers working with legacy .NET systems trying to connect to modern websites and APIs that demand up-to-date TLS security protocols.

**Why does this happen?** The modern web requires **TLS 1.2 or higher** for secure HTTPS connections. Older protocols (TLS 1.0, 1.1, SSLv3) are being phased out due to security vulnerabilities. Unfortunately, older .NET Framework versions and specific Windows versions often struggle:

*   **.NET Framework 4.0 and 4.5.x** do not enable TLS 1.2 by default.
*   While you can try setting `ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12` in .NET 4.5+, this often fails due to underlying **OS limitations**.
*   **Problematic Operating Systems:** Windows 7, Windows Server 2008 R2, and **Windows Server 2012** (non-R2) often lack the necessary protocol support or enabled cipher suites, even after installing updates. Windows Server 2012 R2 is generally better but may still require configuration.

I created RobustDownload.NET after battling these exact issues on a client's mission-critical application that couldn't be immediately migrated to .NET Core or .NET 6+. When the standard approaches failed:

*   Setting `ServicePointManager.SecurityProtocol`
*   Installing OS security updates (like KB3140245 and the Easy Fix for Win7/2008 R2)
*   Modifying registry settings (`SchUseStrongCrypto`, `SystemDefaultTlsVersions`)
*   Dangerously bypassing certificate validation (`ServerCertificateValidationCallback`)

...we needed a solution that *actually works* in the real world, reliably, across these challenging environments.

## The Solution: Delegate to the Battle-Tested Experts! 🦸‍♂️

RobustDownload.NET takes a pragmatic and robust approach by leveraging the power of mature, widely-used command-line tools that handle modern TLS negotiations flawlessly:

1.  **`curl`**: A ubiquitous and powerful tool for transferring data with URLs.
2.  **`wget`**: Another highly reliable utility designed specifically for network retrieval.
3.  **`PowerShell`**: Utilizing `Invoke-WebRequest` for a native Windows approach with modern TLS support (requires PowerShell 3.0+).

This library provides simple VB.NET and C# classes (`RobustDownload`) that intelligently wraps these tools, offering:

*   **Multiple fallback methods** - If one approach fails, it automatically tries another.
*   **Clean, modern .NET API** - Interact with simple VB.NET or C# classes, hiding the command-line complexity.
*   **Detailed error reporting** - Understand exactly *why* a download failed, including errors from all attempted methods.
*   **Production-ready reliability** - Designed for real-world legacy applications that can't afford download failures.

## Features 📋

*   ✅ **Solves TLS 1.2/1.3 Issues:** Works on older .NET Framework versions (4.0+) and Windows versions where native methods fail.
*   ✅ **Automatic Fallback:** Intelligently tries `curl` -> `wget` -> `PowerShell` (or your preferred order) until success.
*   ✅ **Comprehensive Error Handling:** Returns detailed `DownloadResult` object with status, content, errors, status code, and duration. Aggregates errors from all failed attempts.
*   ✅ **Flexible Control:** Supports User-Agent, Custom Headers, Basic Authentication, Timeouts, and Proxies.
*   ✅ **Multiple Output Formats:** Download as String, save directly to File, or retrieve raw Byte Array.
*   ✅ **Text Encoding Control:** Explicitly handle text encoding for international content (`DownloadStringWithEncoding`).
*   ✅ **Secure by Default:** Performs proper SSL/TLS certificate validation unless explicitly disabled.
*   ✅ **Optional Insecure Mode:** `allowInsecureSSL` flag for specific scenarios (use with extreme caution!).
*   ✅ **Dependency Checks:** Helper functions to verify tool availability.
*   ✅ **URL Building Helper:** Easily construct URLs with query parameters.
*   ✅ **Thread-Safe:** Static methods are inherently thread-safe.

## Requirements 🔩

1.  **.NET Framework 4.0 or later**. (Note: While the library targets .NET 4.0+, the *need* for it is highest on systems running .NET 4.0-4.5.x combined with older Windows versions).
2.  **Windows Operating System** (Tested primarily on Windows 7, Server 2008 R2, Server 2012/R2, Windows 10).
3.  **External Tools (At least one required, more recommended for fallback):**
    *   **`curl.exe`**: Must be installed and accessible via the system's `PATH` for the `Curl` method.
    *   **`wget.exe`**: Must be installed and accessible via the system's `PATH` for the `Wget` method.
    *   **`powershell.exe`**: Required for the `PowerShell` method (PowerShell 3.0 or later is recommended for best TLS support, usually available by default on Windows 8 / Server 2012 and later).

## Installation 💻

### Step 1: Add the RobustDownload.NET Library

#### Option A: Via NuGet (Coming Soon!)

```powershell
Install-Package RobustDownload.NET
```
*(Note: Replace with actual package name once published)*

#### Option B: Add Source Code Directly

1.  Clone or download this repository.
2.  Copy the `RobustDownload.vb` file into your VB.NET project or the `RobustDownload.cs` file into your C# project. Ensure the necessary `System.Web` assembly reference is added if not present (for `HttpUtility.UrlEncode`).

### Step 2: Install & Configure Dependencies (curl and/or wget)

For the `Curl` and `Wget` methods to work from your .NET application, the respective `.exe` files must be:
1.  **Installed** on the machine running your application.
2.  **Findable** via the system's `PATH` environment variable *by the application process*.

---

⭐ **Troubleshooting Common Issue: "System cannot find the file specified"** ⭐

A very common problem is that `curl --version` or `wget --version` works perfectly fine when you type it into a Command Prompt or PowerShell window, but your .NET application (especially when running/debugging in Visual Studio) throws a `System.ComponentModel.Win32Exception: The system cannot find the file specified` when trying to use `RobustDownload.IsCurlAvailable()` or execute a download.

**Why does this happen?**

*   Your interactive command prompt (cmd, PowerShell) and your running .NET application process often have **different views of the `PATH` environment variable**.
*   When you update the System PATH (e.g., by running an installer or a script), running processes **do not automatically pick up the change**. They inherit the environment variables that existed *when they were started*.
*   Visual Studio, in particular, needs to be restarted to inherit updated System PATH variables.

**The Solution:**

1.  **Ensure the *correct directory* containing `curl.exe` or `wget.exe` (usually a `bin` subfolder) is added to the *System* PATH variable.** Adding it to the User PATH might not be sufficient if your application runs under a different context (e.g., a Windows Service).
2.  **CRITICAL STEP: Restart Visual Studio** after updating the PATH. If the error persists (especially for services or scheduled tasks), **restart the computer** or the specific service to ensure the changes are applied system-wide and inherited by all new processes.

---

#### Installing curl

*   **Windows 10 (v1803+)/Windows 11:** Curl is usually pre-installed. Verify with `curl --version` in Command Prompt.
*   **Older Windows / To Install/Update (Recommended Methods):**
    *   **Winget (Windows 10 v1709+):** (Usually handles PATH automatically)
        ```powershell
        winget install --id=Curl.Curl.EXE -e
        ```
    *   **Chocolatey:** (Usually handles PATH automatically)
        ```powershell
        choco install curl
        ```
    *   **Manual:** Download from [curl.se/windows/](https://curl.se/windows/), extract (e.g., to `C:\Program Files\curl`), and **manually add the `bin` subdirectory** (e.g., `C:\Program Files\curl\bin`) to the System PATH (see below).

#### Installing wget

*   **Recommended Methods:**
    *   **Winget:** (Usually handles PATH automatically)
        ```powershell
        winget install --id=GnuWin32.Wget -e
        ```
    *   **Chocolatey:** (Usually handles PATH automatically)
        ```powershell
        choco install wget
        ```
    *   **Manual:** Download from a trusted source like [Eternally Bored](https://eternallybored.org/misc/wget/), extract (e.g., to `C:\Program Files\GnuWin32`), and **manually add the `bin` subdirectory** (e.g., `C:\Program Files\GnuWin32\bin`) to the System PATH (see below).

#### Adding to System PATH Environment Variable (If Needed)

If you installed manually or if an installer didn't update the PATH correctly:

1.  Search for "Edit the system environment variables" in Windows search and open it.
2.  Click the "Environment Variables..." button.
3.  In the **"System variables"** section (bottom pane), find the variable named `Path` and select it.
4.  Click "Edit...".
5.  Click "New" and paste the **full path** to the directory containing the `.exe` file (e.g., `C:\Program Files\curl\bin`).
6.  Click OK on all dialog windows to save the changes.
7.  **➡️ IMPORTANT: Restart Visual Studio and/or your computer/service! ⬅️**

**Using PowerShell to Add to System PATH (Run as Administrator):**

```powershell
# --- ADJUST THESE PATHS TO YOUR ACTUAL INSTALLATION ---
$curlBinPath = "C:\Program Files\curl\bin"
$wgetBinPath = "C:\Program Files\GnuWin32\bin"
# ---

# Check if running as Admin
if (-NOT ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "Please run this script as Administrator to modify the System PATH." ; Read-Host "Press Enter to exit..." ; exit 1
}

$updateNeeded = $false
$currentSystemPath = [Environment]::GetEnvironmentVariable("Path", "Machine")
$pathEntries = $currentSystemPath -split ';' | Where-Object { $_ -ne '' }

# Add Curl Path if missing and exists
if (($pathEntries -notcontains $curlBinPath) -and (Test-Path $curlBinPath)) {
    Write-Host "Adding Curl path: $curlBinPath"
    $currentSystemPath = ($currentSystemPath.TrimEnd(';') + ';' + $curlBinPath).TrimStart(';')
    [Environment]::SetEnvironmentVariable("Path", $currentSystemPath, "Machine")
    $updateNeeded = $true
}

# Add Wget Path if missing and exists
if (($pathEntries -notcontains $wgetBinPath) -and (Test-Path $wgetBinPath)) {
    Write-Host "Adding Wget path: $wgetBinPath"
    # Re-read path in case Curl was just added
    $currentSystemPath = [Environment]::GetEnvironmentVariable("Path", "Machine")
    $currentSystemPath = ($currentSystemPath.TrimEnd(';') + ';' + $wgetBinPath).TrimStart(';')
    [Environment]::SetEnvironmentVariable("Path", $currentSystemPath, "Machine")
    $updateNeeded = $true
}

if ($updateNeeded) {
    Write-Host "`nSystem PATH updated." -ForegroundColor Green
    Write-Host ">>> IMPORTANT: You MUST restart Visual Studio, relevant services, or your computer for changes to take effect! <<<" -ForegroundColor Yellow
} else {
    Write-Host "`nRequired paths already exist in System PATH or specified directories not found."
}
Read-Host "Press Enter to exit..."
```

#### Verification

After installing and potentially restarting:
1.  Open a **new** Command Prompt or PowerShell window and type `curl --version` and `wget --version`. They should execute correctly.
2.  Run your .NET application. `RobustDownload.VerifyDependencies()` or `RobustDownload.IsCurlAvailable()` / `IsWgetAvailable()` should now return `True`.

## Quick Start & Usage Examples 💡

### VB.NET Example

```vb.net
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text
' Ensure RobustDownload class file is in your project
' Or reference the namespace if using a DLL/NuGet package

Public Module ExampleUsageVB
    Public Sub Main()
        Dim targetUrl As String = "https://example.com" ' Use a URL that requires TLS 1.2+

        ' --- Example 1: Simple String Download (Auto Method) ---
        Console.WriteLine("--- VB Example 1: Simple String Download ---")
        Dim result1 As DownloadResult = RobustDownload.Download(targetUrl)

        If result1.Success Then
            Console.WriteLine($"Success using {result1.UsedMethod}! Status: {result1.StatusCode}, Duration: {result1.DurationMs}ms")
            Console.WriteLine($"Content Length: {If(result1.Content IsNot Nothing, result1.Content.Length, 0)}")
        Else
            Console.WriteLine($"Download failed!")
            Console.WriteLine($"Error: {result1.ErrorMessage}")
        End If
        Console.WriteLine(New String("-"c, 40))

        ' --- Example 2: Download to File using Curl (with Fallback) ---
        Console.WriteLine("--- VB Example 2: Download to File (Curl w/ Fallback) ---")
        Dim savePath As String = Path.Combine(Path.GetTempPath(), "downloaded_page_vb.html")
        Dim result2 As DownloadResult = RobustDownload.DownloadFile(targetUrl, savePath, DownloadMethod.Curl, enableFallback:=True)

        If result2.Success Then
            Console.WriteLine($"Success using {result2.UsedMethod}! Status: {result2.StatusCode}, Duration: {result2.DurationMs}ms")
            Console.WriteLine($"File saved to: {result2.FilePath}")
            ' If File.Exists(savePath) Then File.Delete(savePath) ' Optional cleanup
        Else
            Console.WriteLine($"Download failed!")
            Console.WriteLine($"Error: {result2.ErrorMessage}")
            For Each err In result2.AllErrors
                 Console.WriteLine($" - Method {err.Key} failed: {err.Value}")
            Next
        End If
        Console.WriteLine(New String("-"c, 40))

        ' --- Example 3: Download Data (Bytes) with Auth, Headers ---
        Console.WriteLine("--- VB Example 3: Download Data (Advanced) ---")
        Dim authUrl As String = "https://httpbin.org/basic-auth/user/passwd"
        Dim headers As New Dictionary(Of String, String) From {
            {"X-Custom-Header", "MyValueVB"},
            {"Accept", "application/json"}
        }
        Dim dataBytes As Byte() = RobustDownload.DownloadData(
                                        url:=authUrl,
                                        method:=DownloadMethod.Auto,
                                        enableFallback:=True,
                                        useragent:="RobustDownload.NET Client VB/1.0",
                                        username:="user",
                                        password:="passwd",
                                        headers:=headers,
                                        timeoutSeconds:=20)

        If dataBytes IsNot Nothing Then
            Console.WriteLine($"Success downloading data! Bytes received: {dataBytes.Length}")
            ' Dim responseString = Encoding.UTF8.GetString(dataBytes)
        Else
            Console.WriteLine($"Failed to download data.")
        End If
        Console.WriteLine(New String("-"c, 40))

        ' --- Example 4: Verify Dependencies ---
        Console.WriteLine("--- VB Example 4: Verify Dependencies ---")
        Dim dependencies = RobustDownload.VerifyDependencies()
        For Each dep In dependencies
            Console.WriteLine($"{dep.Key}: {(If(dep.Value, "Available", "NOT Available"))}")
        Next
        Console.WriteLine($"Best available method: {RobustDownload.GetBestAvailableMethod()}")
        Console.WriteLine(New String("-"c, 40))
    End Sub
End Module
```

### C# Example

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
// Ensure RobustDownload class file is in your project
// Or reference the namespace if using a DLL/NuGet package

public class ExampleUsageCS
{
    public static void Main(string[] args)
    {
        string targetUrl = "https://example.com"; // Use a URL that requires TLS 1.2+

        // --- Example 1: Simple String Download (Auto Method) ---
        Console.WriteLine("--- C# Example 1: Simple String Download ---");
        DownloadResult result1 = RobustDownload.Download(targetUrl);

        if (result1.Success)
        {
            Console.WriteLine($"Success using {result1.UsedMethod}! Status: {result1.StatusCode}, Duration: {result1.DurationMs}ms");
            Console.WriteLine($"Content Length: {(result1.Content != null ? result1.Content.Length : 0)}");
            // Console.WriteLine(result1.Content?.Substring(0, Math.Min(result1.Content?.Length ?? 0, 200)) + "...");
        }
        else
        {
            Console.WriteLine($"Download failed!");
            Console.WriteLine($"Error: {result1.ErrorMessage}");
        }
        Console.WriteLine(new string('-', 40));

        // --- Example 2: Download to File using Curl (with Fallback) ---
        Console.WriteLine("--- C# Example 2: Download to File (Curl w/ Fallback) ---");
        string savePath = Path.Combine(Path.GetTempPath(), "downloaded_page_cs.html");
        DownloadResult result2 = RobustDownload.DownloadFile(targetUrl, savePath, DownloadMethod.Curl, enableFallback: true);

        if (result2.Success)
        {
            Console.WriteLine($"Success using {result2.UsedMethod}! Status: {result2.StatusCode}, Duration: {result2.DurationMs}ms");
            Console.WriteLine($"File saved to: {result2.FilePath}");
            // if (File.Exists(savePath)) { File.Delete(savePath); } // Optional cleanup
        }
        else
        {
            Console.WriteLine($"Download failed!");
            Console.WriteLine($"Error: {result2.ErrorMessage}");
            // Display errors from all attempted methods
            foreach (var err in result2.AllErrors)
            {
                 Console.WriteLine($" - Method {err.Key} failed: {err.Value}");
            }
        }
        Console.WriteLine(new string('-', 40));

        // --- Example 3: Download Data (Bytes) with Auth, Headers ---
        Console.WriteLine("--- C# Example 3: Download Data (Advanced) ---");
        string authUrl = "https://httpbin.org/basic-auth/user/passwd"; // Test URL
        var headers = new Dictionary<string, string> {
            {"X-Custom-Header", "MyValueCS"},
            {"Accept", "application/json"}
        };
        byte[] dataBytes = RobustDownload.DownloadData(
                                        url: authUrl,
                                        method: DownloadMethod.Auto,
                                        enableFallback: true,
                                        useragent: "RobustDownload.NET Client CS/1.0",
                                        username: "user",
                                        password: "passwd",
                                        headers: headers,
                                        timeoutSeconds: 20,
                                        proxyUrl: "" // e.g., "http://proxyserver:8080"
                                        );

        if (dataBytes != null)
        {
            Console.WriteLine($"Success downloading data! Bytes received: {dataBytes.Length}");
            // string responseString = Encoding.UTF8.GetString(dataBytes);
        }
        else
        {
            Console.WriteLine($"Failed to download data.");
            // Check Debug Output for detailed error from DownloadData
        }
        Console.WriteLine(new string('-', 40));

         // --- Example 4: Verify Dependencies ---
        Console.WriteLine("--- C# Example 4: Verify Dependencies ---");
        var dependencies = RobustDownload.VerifyDependencies();
        foreach (var dep in dependencies)
        {
            Console.WriteLine($"{dep.Key}: {(dep.Value ? "Available" : "NOT Available")}");
        }
        Console.WriteLine($"Best available method: {RobustDownload.GetBestAvailableMethod()}");
        Console.WriteLine(new string('-', 40));
    }
}
```

## Why Use External Tools Instead of Pure .NET?

You might wonder why we resort to external command-line tools. The answer is simple: **proven reliability and compatibility across difficult environments.**

Tools like `curl` and `wget` have been developed and battle-tested for decades across countless systems. They are actively maintained and updated to handle the latest TLS protocols, cipher suites, and web server quirks. Crucially, they often work correctly on older Windows versions (like Win 7, Server 2008 R2, Server 2012) where the native .NET stack (especially Framework 4.0-4.5.x) struggles immensely with modern TLS requirements, even with registry hacks and OS updates applied. PowerShell's `Invoke-WebRequest` also leverages more modern Windows components.

By leveraging these tools, RobustDownload.NET inherits their robustness and provides a reliable download mechanism when the native .NET methods fail due to these underlying system limitations.

When your production application *must* reliably connect to external services from a legacy environment and cannot fail due to TLS negotiation issues, this pragmatic approach delivers results.

## Troubleshooting 🔍

### Checking Tool Availability

*(Example in C#)*
```csharp
// Check which download methods are available
var dependencies = RobustDownload.VerifyDependencies();
foreach (var dep in dependencies)
{
    Console.WriteLine($"{dep.Key}: {(dep.Value ? "Available" : "Not available")}");
}

// Get the best available method based on checks
var bestMethod = RobustDownload.GetBestAvailableMethod();
Console.WriteLine($"Best method available: {bestMethod}");
```

### Common Issues

1.  **`System.ComponentModel.Win32Exception: The system cannot find the file specified`**: This is the most frequent issue. It means your .NET application cannot locate `curl.exe` or `wget.exe` when trying to execute them. There are two main causes and solutions:
    *   **Cause A: PATH Environment Variable Issue:** The directory containing the executable (e.g., `C:\Program Files\curl\bin`) is not included in the **System `PATH` environment variable**, OR the `PATH` was updated *after* Visual Studio (or your application process/service) was started.
        *   **Solution A:** Ensure the correct directory is added to the System `PATH` (see [Setting Up Dependencies](#step-2-install--configure-dependencies-curl-andor-wget) section) and **critically, restart Visual Studio, the relevant service, or your computer** for the changes to be recognized by the application process.
    *   **Cause B: Executable Not Accessible:** The tools are installed, but not globally accessible via PATH.
        *   **Solution B (Copy Local):** A simpler alternative, especially for deployment, is to **copy `curl.exe` or `wget.exe` (and any associated `.dll` files found in their installation directory, like `libcurl.dll`) directly into your application's output directory** (e.g., the `bin\Debug` or `bin\Release` folder, alongside your application's `.exe`). `Process.Start` will find executables in the application's own directory without needing the system PATH. Remember to set the "Copy to Output Directory" property for these files in Visual Studio (e.g., to "Copy if newer").

2.  **Download Fails with All Methods**: Check the `AllErrors` dictionary in the returned `DownloadResult` for specific error messages from each tool. Common reasons include:
    *   Network connectivity issues (firewalls, DNS).
    *   Incorrect proxy settings (`proxyUrl` parameter).
    *   Invalid target URL.
    *   Server-side errors.
    *   Permissions issues preventing the execution of `curl.exe`, `wget.exe`, or `powershell.exe`.
3.  **Garbled Text Content**: The website might be using an encoding different from your system's default or UTF-8. Use `DownloadStringWithEncoding` and specify the correct `System.Text.Encoding` (e.g., `Encoding.UTF8`, `Encoding.GetEncoding("ISO-8859-1")`, `Encoding.GetEncoding("Shift_JIS")`).
4.  **Slow Downloads / Timeouts**: Increase the `timeoutSeconds` parameter. Check network speed and latency. The target server itself might be slow or rate-limiting.
5.  **PowerShell Method Fails on Old Systems**: `Invoke-WebRequest` requires PowerShell 3.0 or later. Windows 7 / Server 2008 R2 ship with PowerShell 2.0 by default. You may need to upgrade PowerShell on those systems (via Windows Management Framework updates) for the `PowerShell` method to work reliably.

## Security Considerations ⚠️

By default, RobustDownload.NET performs proper TLS certificate validation via the underlying tools (`curl`, `wget`, `Invoke-WebRequest` all do this by default).

The `allowInsecureSSL` parameter **disables this validation**. This makes your application vulnerable to **Man-in-the-Middle (MitM) attacks**.

**DO NOT set `allowInsecureSSL = True` in production environments unless you have a very specific, understood need (e.g., connecting to a trusted internal device with a self-signed certificate) and accept the significant security risks.**

*(Example in C#)*
```csharp
// --- Example: Using Insecure SSL (Use with Extreme Caution!) ---
Console.WriteLine("--- C# Example: Insecure SSL Download ---");
string selfSignedUrl = "https://self-signed.badssl.com/"; // Test URL with bad cert
DownloadResult resultInsecure = RobustDownload.Download(selfSignedUrl, allowInsecureSSL: true);

if (resultInsecure.Success)
{
     Console.WriteLine($"Success using {resultInsecure.UsedMethod} with Insecure SSL! Status: {resultInsecure.StatusCode}");
}
else
{
     Console.WriteLine($"Download failed even with Insecure SSL!");
     Console.WriteLine($"Error: {resultInsecure.ErrorMessage}");
}
```

## Contributing 🤝

Contributions are welcome! If you find bugs, have feature requests, or want to improve the documentation, please open an issue on GitHub. If you'd like to contribute code:

1.  Fork the repository.
2.  Create a feature branch (`git checkout -b feature/YourFeature`).
3.  Make your changes.
4.  Commit your changes (`git commit -m 'Add some feature'`).
5.  Push to the branch (`git push origin feature/YourFeature`).
6.  Open a Pull Request.

## Acknowledgements 🙏

*   Inspired by the real-world challenges of maintaining legacy .NET applications in a modern web environment.
*   Thanks to the developers of `curl`, `wget`, and `PowerShell` for their invaluable tools.
*   To all the developers who have spent hours troubleshooting TLS issues in .NET Framework 4.0/4.5 on Windows 7/Server 2008 R2/Server 2012 – hopefully, this helps!

---

*RobustDownload.NET: Because sometimes, you just need your downloads to **work**.*
