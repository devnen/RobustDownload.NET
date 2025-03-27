using System;
using System.IO;                // For File, Path, FileInfo classes
using System.Text;              // For StringBuilder class
using System.Threading;         // For AutoResetEvent, Thread classes
using System.Net;               // For network-related classes
using System.Diagnostics;       // For Process, ProcessStartInfo classes
using System.Collections.Generic; // For Dictionary and List classes

/// <summary>
/// A robust class for downloading content from URLs using various methods including curl, wget, and PowerShell.
/// DEPENDENCIES: Requires curl.exe and/or wget.exe to be available in the system's PATH for the respective 
/// download methods to function. PowerShell.exe is required for the PowerShell method.
/// </summary>
public class RobustDownload
{
    /// <summary>
    /// Defines the available download methods.
    /// </summary>
    public enum DownloadMethod
    {
        Auto = 0,        // Automatically choose the best method
        Curl = 1,        // Use curl as primary method
        Wget = 2,        // Use wget as primary method
        PowerShell = 3   // Use PowerShell WebClient as primary method
    }

    /// <summary>
    /// Class to hold the result of a download operation.
    /// </summary>
    public class DownloadResult
    {
        private bool _success = false;
        private string _content = "";
        private byte[] _data = null;
        private string _filePath = "";
        private string _errorMessage = "";
        private DownloadMethod _usedMethod = DownloadMethod.Auto;
        private int _statusCode = 0;
        private long _durationMs = 0;
        private Dictionary<DownloadMethod, string> _allErrors = new Dictionary<DownloadMethod, string>();

        /// <summary>
        /// Whether the download was successful.
        /// </summary>
        public bool Success
        {
            get { return _success; }
            set { _success = value; }
        }

        /// <summary>
        /// The downloaded content as a string (if text content was downloaded).
        /// NOTE: When content is captured directly from command-line tools (not from file),
        /// the encoding depends on the console output encoding of the external tool.
        /// Use DownloadStringWithEncoding for explicit encoding control.
        /// </summary>
        public string Content
        {
            get { return _content; }
            set { _content = value; }
        }

        /// <summary>
        /// The downloaded content as a byte array (if binary content was downloaded).
        /// </summary>
        public byte[] Data
        {
            get { return _data; }
            set { _data = value; }
        }

        /// <summary>
        /// Path to the saved file (if content was saved to a file).
        /// </summary>
        public string FilePath
        {
            get { return _filePath; }
            set { _filePath = value; }
        }

        /// <summary>
        /// Error message if the download failed.
        /// </summary>
        public string ErrorMessage
        {
            get { return _errorMessage; }
            set { _errorMessage = value; }
        }

        /// <summary>
        /// The download method that was successfully used.
        /// </summary>
        public DownloadMethod UsedMethod
        {
            get { return _usedMethod; }
            set { _usedMethod = value; }
        }

        /// <summary>
        /// HTTP status code if available. Note: This might be approximate as not all tools 
        /// provide direct access to the status code. Default is 200 for success, 0 for failure.
        /// </summary>
        public int StatusCode
        {
            get { return _statusCode; }
            set { _statusCode = value; }
        }

        /// <summary>
        /// Duration of the download operation in milliseconds.
        /// </summary>
        public long DurationMs
        {
            get { return _durationMs; }
            set { _durationMs = value; }
        }

        /// <summary>
        /// Collection of errors from all attempted methods if multiple methods were tried.
        /// </summary>
        public Dictionary<DownloadMethod, string> AllErrors
        {
            get { return _allErrors; }
            set { _allErrors = value; }
        }

        /// <summary>
        /// Creates a new success result.
        /// </summary>
        public static DownloadResult CreateSuccess(DownloadMethod method, string content = "",
                                             byte[] data = null, string filePath = "",
                                             int statusCode = 200,
                                             long durationMs = 0)
        {
            DownloadResult result = new DownloadResult();
            result.Success = true;
            result.Content = content;
            result.Data = data;
            result.FilePath = filePath;
            result.UsedMethod = method;
            result.StatusCode = statusCode;
            result.DurationMs = durationMs;
            return result;
        }

        /// <summary>
        /// Creates a new failure result.
        /// </summary>
        public static DownloadResult CreateFailure(string errorMessage, DownloadMethod method,
                                             int statusCode = 0,
                                             long durationMs = 0)
        {
            DownloadResult result = new DownloadResult();
            result.Success = false;
            result.ErrorMessage = errorMessage;
            result.UsedMethod = method;
            result.StatusCode = statusCode;
            result.DurationMs = durationMs;
            return result;
        }
    }

    /// <summary>
    /// Downloads content from a URL using the specified method with fallback options.
    /// Returns a DownloadResult object containing the result status and content.
    /// </summary>
    /// <param name="url">The URL to download content from.</param>
    /// <param name="method">The primary download method to use.</param>
    /// <param name="enableFallback">Whether to try alternative methods if the primary method fails.</param>
    /// <param name="useragent">The User-Agent header value.</param>
    /// <param name="username">Optional username for authentication.</param>
    /// <param name="password">Optional password for authentication.</param>
    /// <param name="headers">Optional additional HTTP headers.</param>
    /// <param name="timeoutSeconds">Timeout in seconds.</param>
    /// <param name="proxyUrl">Optional proxy URL.</param>
    /// <param name="outputFile">Optional path to save the downloaded content.</param>
    /// <param name="allowInsecureSSL">Whether to allow insecure SSL connections (not recommended for security reasons).</param>
    /// <returns>A DownloadResult object containing the result of the operation.</returns>
    public static DownloadResult Download(
                              string url,
                              DownloadMethod method = DownloadMethod.Auto,
                              bool enableFallback = true,
                              string useragent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                              string username = "",
                              string password = "",
                              Dictionary<string, string> headers = null,
                              int timeoutSeconds = 60,
                              string proxyUrl = "",
                              string outputFile = "",
                              bool allowInsecureSSL = false)
    {
        // Track operation time
        long startTime = DateTime.Now.Ticks;

        // Flag to determine if we're saving to a file
        bool isSavingToFile = !string.IsNullOrEmpty(outputFile);

        // Create a temporary output file if needed
        string tempOutputFile = "";
        if (isSavingToFile)
        {
            tempOutputFile = outputFile;
        }

        // Determine methods to try based on primary method and fallback setting
        List<DownloadMethod> methodsToTry = new List<DownloadMethod>();

        if (method == DownloadMethod.Auto)
        {
            // Try curl first, then wget, then PowerShell
            methodsToTry.Add(DownloadMethod.Curl);
            methodsToTry.Add(DownloadMethod.Wget);
            methodsToTry.Add(DownloadMethod.PowerShell);
        }
        else
        {
            // Start with the specified method
            methodsToTry.Add(method);

            // Add fallbacks if enabled
            if (enableFallback)
            {
                if (method != DownloadMethod.Curl) methodsToTry.Add(DownloadMethod.Curl);
                if (method != DownloadMethod.Wget) methodsToTry.Add(DownloadMethod.Wget);
                if (method != DownloadMethod.PowerShell) methodsToTry.Add(DownloadMethod.PowerShell);
            }
        }

        // Create a result to collect errors if we need to try multiple methods
        DownloadResult finalResult = new DownloadResult();

        // Try each method in order until one succeeds
        foreach (DownloadMethod downloadMethod in methodsToTry)
        {
            DownloadResult result = null;

            switch (downloadMethod)
            {
                case DownloadMethod.Curl:
                    if (IsCurlAvailable())
                    {
                        result = DownloadWithCurl(url, tempOutputFile, useragent, username, password, headers, timeoutSeconds, proxyUrl, allowInsecureSSL);
                        if (result.Success)
                        {
                            // Calculate duration
                            result.DurationMs = (DateTime.Now.Ticks - startTime) / 10000;
                            return result;
                        }
                        else
                        {
                            // Store the error for later reporting
                            finalResult.AllErrors[DownloadMethod.Curl] = result.ErrorMessage;
                        }
                    }
                    break;

                case DownloadMethod.Wget:
                    if (IsWgetAvailable())
                    {
                        result = DownloadWithWget(url, tempOutputFile, useragent, username, password, headers, timeoutSeconds, proxyUrl, allowInsecureSSL);
                        if (result.Success)
                        {
                            // Calculate duration
                            result.DurationMs = (DateTime.Now.Ticks - startTime) / 10000;
                            return result;
                        }
                        else
                        {
                            // Store the error for later reporting
                            finalResult.AllErrors[DownloadMethod.Wget] = result.ErrorMessage;
                        }
                    }
                    break;

                case DownloadMethod.PowerShell:
                    result = DownloadWithPowerShell(url, tempOutputFile, useragent, username, password, headers, timeoutSeconds, proxyUrl, allowInsecureSSL);
                    if (result.Success)
                    {
                        // Calculate duration
                        result.DurationMs = (DateTime.Now.Ticks - startTime) / 10000;
                        return result;
                    }
                    else
                    {
                        // Store the error for later reporting
                        finalResult.AllErrors[DownloadMethod.PowerShell] = result.ErrorMessage;
                    }
                    break;
            }
        }

        // If we get here, all methods failed
        StringBuilder errorMsg = new StringBuilder("All download methods failed for URL: " + url);

        // Add detailed errors from each method that was tried
        if (finalResult.AllErrors.Count > 0)
        {
            errorMsg.AppendLine();
            errorMsg.AppendLine("Detailed errors by method:");
            foreach (KeyValuePair<DownloadMethod, string> err in finalResult.AllErrors)
            {
                errorMsg.AppendLine("- " + err.Key.ToString() + ": " + err.Value);
            }
        }

        return DownloadResult.CreateFailure(errorMsg.ToString(), method, 0, (DateTime.Now.Ticks - startTime) / 10000);
    }

    /// <summary>
    /// Downloads a string from a URL using the best available method.
    /// This is a simplified version of Download that returns just the string content.
    /// NOTE: When capturing content directly from command-line tools, the text encoding
    /// depends on the console output encoding. Use DownloadStringWithEncoding for 
    /// explicit encoding control.
    /// </summary>
    /// <param name="url">The URL to download from.</param>
    /// <param name="method">The primary download method to use.</param>
    /// <param name="defaultValue">Default value to return if download fails.</param>
    /// <param name="useragent">The User-Agent header value.</param>
    /// <returns>The downloaded string or defaultValue if download failed.</returns>
    public static string DownloadString(
                                   string url,
                                   DownloadMethod method = DownloadMethod.Auto,
                                   string defaultValue = "",
                                   string useragent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0.0.0")
    {
        DownloadResult result = Download(url, method, true, useragent);
        if (result.Success)
        {
            return result.Content;
        }
        else
        {
            Debug.Print("DownloadString failed: " + result.ErrorMessage);
            return defaultValue;
        }
    }

    /// <summary>
    /// Downloads content from a URL using curl.
    /// </summary>
    /// <returns>A DownloadResult object containing the result of the operation.</returns>
    private static DownloadResult DownloadWithCurl(
                                  string url,
                                  string outputFile,
                                  string useragent,
                                  string username,
                                  string password,
                                  Dictionary<string, string> headers,
                                  int timeoutSeconds,
                                  string proxyUrl,
                                  bool allowInsecureSSL)
    {
        StringBuilder stdOutput = new StringBuilder();
        StringBuilder stdError = new StringBuilder();
        int statusCode = 0;
        bool isSavingToFile = !string.IsNullOrEmpty(outputFile);

        try
        {
            // Create process start info
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "curl.exe";
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            // Build arguments
            StringBuilder args = new StringBuilder();

            // Basic curl options
            args.Append("-s -L ");  // -s: silent, -L: follow redirects

            // Add write-out option to get status code
            args.Append("-w \"%{http_code}\" ");

            // Add insecure SSL flag only if explicitly allowed
            if (allowInsecureSSL)
            {
                args.Append("-k ");  // -k: insecure, allows connections to SSL sites without certificates
                Debug.Print("WARNING: Using insecure SSL connections with curl");
            }

            // User agent
            args.Append("-A \"").Append(useragent).Append("\" ");

            // Authentication
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                args.Append("--user ").Append(username).Append(":").Append(password).Append(" ");
            }

            // Proxy
            if (!string.IsNullOrEmpty(proxyUrl))
            {
                args.Append("--proxy ").Append(proxyUrl).Append(" ");
            }

            // Headers
            if (headers != null && headers.Count > 0)
            {
                foreach (KeyValuePair<string, string> header in headers)
                {
                    args.Append("-H \"").Append(header.Key).Append(": ").Append(header.Value).Append("\" ");
                }
            }

            // Timeout - add both connect timeout and maximum operation time
            args.Append("--connect-timeout ").Append(timeoutSeconds).Append(" ");
            args.Append("--max-time ").Append(timeoutSeconds).Append(" ");

            // URL
            args.Append("\"").Append(url).Append("\" ");

            // Output file if specified
            if (isSavingToFile)
            {
                args.Append("-o \"").Append(outputFile).Append("\" ");
            }

            startInfo.Arguments = args.ToString();

            // Execute curl with proper stream handling
            using (Process process = new Process())
            {
                process.StartInfo = startInfo;

                // Set up output and error handling
                AutoResetEvent outputWaitHandle = new AutoResetEvent(false);
                AutoResetEvent errorWaitHandle = new AutoResetEvent(false);

                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null)
                    {
                        // The last line should be the status code when using -w "%{http_code}"
                        int parsedStatusCode;
                        if (int.TryParse(e.Data.Trim(), out parsedStatusCode))
                        {
                            // This is the status code, don't add to content
                            statusCode = parsedStatusCode;
                        }
                        else
                        {
                            stdOutput.AppendLine(e.Data);
                        }
                    }
                    else
                    {
                        outputWaitHandle.Set();
                    }
                };

                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null)
                    {
                        stdError.AppendLine(e.Data);
                    }
                    else
                    {
                        errorWaitHandle.Set();
                    }
                };

                // Start the process
                process.Start();

                // Begin reading stdout and stderr asynchronously
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Wait for the process to exit
                if (process.WaitForExit(timeoutSeconds * 1000))
                {
                    // Wait for async reads to complete
                    outputWaitHandle.WaitOne(1000);
                    errorWaitHandle.WaitOne(1000);

                    // Check result
                    int exitCode = process.ExitCode;
                    string errorText = stdError.ToString().Trim();

                    if (exitCode == 0)
                    {
                        // Set default status code if we couldn't parse it
                        if (statusCode == 0) statusCode = 200;

                        // Check if HTTP status indicates success (2xx)
                        if (statusCode >= 200 && statusCode < 300)
                        {
                            // Success
                            if (isSavingToFile)
                            {
                                // Check if file exists and has content
                                if (File.Exists(outputFile) && new FileInfo(outputFile).Length > 0)
                                {
                                    return DownloadResult.CreateSuccess(DownloadMethod.Curl, "", null, outputFile, statusCode);
                                }
                                else
                                {
                                    return DownloadResult.CreateFailure("Curl reported success but output file is empty or missing", DownloadMethod.Curl, statusCode);
                                }
                            }
                            else
                            {
                                // Return the content from stdout
                                string content = stdOutput.ToString();
                                if (!string.IsNullOrEmpty(content))
                                {
                                    return DownloadResult.CreateSuccess(DownloadMethod.Curl, content, null, "", statusCode);
                                }
                                else
                                {
                                    return DownloadResult.CreateFailure("Curl reported success but no content was returned", DownloadMethod.Curl, statusCode);
                                }
                            }
                        }
                        else
                        {
                            // HTTP error
                            return DownloadResult.CreateFailure("HTTP error: " + statusCode, DownloadMethod.Curl, statusCode);
                        }
                    }
                    else
                    {
                        // Process error
                        string msg = "Curl process exited with code: " + exitCode;
                        if (!string.IsNullOrEmpty(errorText))
                        {
                            msg += Environment.NewLine + "Curl error: " + errorText;
                        }
                        return DownloadResult.CreateFailure(msg, DownloadMethod.Curl, statusCode);
                    }
                }
                else
                {
                    // Process timed out
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                        }
                    }
                    catch (Exception)
                    {
                        // Ignore errors killing the process
                    }

                    return DownloadResult.CreateFailure("Curl process timed out after " + timeoutSeconds + " seconds", DownloadMethod.Curl);
                }
            }
        }
        catch (Exception ex)
        {
            return DownloadResult.CreateFailure("Curl execution error: " + ex.Message, DownloadMethod.Curl);
        }
    }

    /// <summary>
    /// Downloads content from a URL using wget.
    /// </summary>
    /// <returns>A DownloadResult object containing the result of the operation.</returns>
    private static DownloadResult DownloadWithWget(
                                  string url,
                                  string outputFile,
                                  string useragent,
                                  string username,
                                  string password,
                                  Dictionary<string, string> headers,
                                  int timeoutSeconds,
                                  string proxyUrl,
                                  bool allowInsecureSSL)
    {
        StringBuilder stdOutput = new StringBuilder();
        StringBuilder stdError = new StringBuilder();
        bool isSavingToFile = !string.IsNullOrEmpty(outputFile);
        string tempOutputFile = isSavingToFile ? outputFile : Path.GetTempFileName();
        int statusCode = 0;

        try
        {
            // Create process start info
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "wget.exe";
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            // Set proxy environment variables if specified
            if (!string.IsNullOrEmpty(proxyUrl))
            {
                startInfo.EnvironmentVariables["http_proxy"] = proxyUrl;
                startInfo.EnvironmentVariables["https_proxy"] = proxyUrl;
            }

            // Build arguments
            StringBuilder args = new StringBuilder();

            // Basic wget options - quiet mode but show server response
            args.Append("-q -S ");  // -q: quiet, -S: show server response in stderr

            // Add insecure SSL flag only if explicitly allowed
            if (allowInsecureSSL)
            {
                args.Append("--no-check-certificate ");
                Debug.Print("WARNING: Using insecure SSL connections with wget");
            }

            // User agent
            args.Append("--user-agent=\"").Append(useragent).Append("\" ");

            // Authentication
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                args.Append("--http-user=").Append(username).Append(" ");
                args.Append("--http-password=").Append(password).Append(" ");
            }

            // Headers
            if (headers != null && headers.Count > 0)
            {
                foreach (KeyValuePair<string, string> header in headers)
                {
                    args.Append("--header=\"").Append(header.Key).Append(": ").Append(header.Value).Append("\" ");
                }
            }

            // Timeout
            args.Append("-T ").Append(timeoutSeconds).Append(" ");

            // Output to stdout if not saving to file
            if (!isSavingToFile)
            {
                args.Append("-O - ");  // Output to stdout
            }
            else
            {
                args.Append("-O \"").Append(tempOutputFile).Append("\" ");
            }

            // URL
            args.Append("\"").Append(url).Append("\"");

            startInfo.Arguments = args.ToString();

            // Execute wget with proper stream handling
            using (Process process = new Process())
            {
                process.StartInfo = startInfo;

                // Set up output and error handling
                AutoResetEvent outputWaitHandle = new AutoResetEvent(false);
                AutoResetEvent errorWaitHandle = new AutoResetEvent(false);

                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null)
                    {
                        stdOutput.AppendLine(e.Data);
                    }
                    else
                    {
                        outputWaitHandle.Set();
                    }
                };

                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null)
                    {
                        // Try to extract the status code from server response
                        // Wget shows HTTP responses in stderr with -S flag
                        if (e.Data.Contains("HTTP/") && e.Data.Contains(" "))
                        {
                            try
                            {
                                string[] parts = e.Data.Trim().Split(' ');
                                if (parts.Length >= 2)
                                {
                                    int.TryParse(parts[1], out statusCode);
                                }
                            }
                            catch
                            {
                                // Ignore parsing errors
                            }
                        }

                        stdError.AppendLine(e.Data);
                    }
                    else
                    {
                        errorWaitHandle.Set();
                    }
                };

                // Start the process
                process.Start();

                // Begin reading stdout and stderr asynchronously
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Wait for the process to exit
                if (process.WaitForExit(timeoutSeconds * 1000))
                {
                    // Wait for async reads to complete
                    outputWaitHandle.WaitOne(1000);
                    errorWaitHandle.WaitOne(1000);

                    // Check result
                    int exitCode = process.ExitCode;
                    string errorText = stdError.ToString().Trim();

                    // Set default status code if we couldn't extract it
                    if (statusCode == 0 && exitCode == 0)
                    {
                        statusCode = 200;  // Assume 200 OK if process succeeded
                    }

                    if (exitCode == 0)
                    {
                        // Success
                        if (isSavingToFile)
                        {
                            // Check if file exists and has content
                            if (File.Exists(tempOutputFile) && new FileInfo(tempOutputFile).Length > 0)
                            {
                                return DownloadResult.CreateSuccess(DownloadMethod.Wget, "", null, tempOutputFile, statusCode);
                            }
                            else
                            {
                                return DownloadResult.CreateFailure("Wget reported success but output file is empty or missing", DownloadMethod.Wget, statusCode);
                            }
                        }
                        else
                        {
                            // Return the content from stdout
                            string content = stdOutput.ToString();

                            // Clean up temporary file
                            try
                            {
                                if (File.Exists(tempOutputFile))
                                {
                                    File.Delete(tempOutputFile);
                                }
                            }
                            catch (Exception)
                            {
                                // Ignore temp file cleanup errors
                            }

                            if (!string.IsNullOrEmpty(content))
                            {
                                return DownloadResult.CreateSuccess(DownloadMethod.Wget, content, null, "", statusCode);
                            }
                            else
                            {
                                return DownloadResult.CreateFailure("Wget reported success but no content was returned", DownloadMethod.Wget, statusCode);
                            }
                        }
                    }
                    else
                    {
                        // Failure
                        // Clean up temporary file if we created one
                        if (!isSavingToFile)
                        {
                            try
                            {
                                if (File.Exists(tempOutputFile))
                                {
                                    File.Delete(tempOutputFile);
                                }
                            }
                            catch (Exception)
                            {
                                // Ignore temp file cleanup errors
                            }
                        }

                        string msg = "Wget process exited with code: " + exitCode;
                        if (!string.IsNullOrEmpty(errorText))
                        {
                            msg += Environment.NewLine + "Wget error: " + errorText;
                        }
                        return DownloadResult.CreateFailure(msg, DownloadMethod.Wget, statusCode);
                    }
                }
                else
                {
                    // Process timed out
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                        }
                    }
                    catch (Exception)
                    {
                        // Ignore errors killing the process
                    }

                    // Clean up temporary file if we created one
                    if (!isSavingToFile)
                    {
                        try
                        {
                            if (File.Exists(tempOutputFile))
                            {
                                File.Delete(tempOutputFile);
                            }
                        }
                        catch (Exception)
                        {
                            // Ignore temp file cleanup errors
                        }
                    }

                    return DownloadResult.CreateFailure("Wget process timed out after " + timeoutSeconds + " seconds", DownloadMethod.Wget);
                }
            }
        }
        catch (Exception ex)
        {
            // Clean up temporary file if we created one
            if (!isSavingToFile)
            {
                try
                {
                    if (File.Exists(tempOutputFile))
                    {
                        File.Delete(tempOutputFile);
                    }
                }
                catch
                {
                    // Ignore temp file cleanup errors
                }
            }

            return DownloadResult.CreateFailure("Wget execution error: " + ex.Message, DownloadMethod.Wget);
        }
    }

    /// <summary>
    /// Downloads content from a URL using PowerShell.
    /// </summary>
    /// <returns>A DownloadResult object containing the result of the operation.</returns>
    private static DownloadResult DownloadWithPowerShell(
                                        string url,
                                        string outputFile,
                                        string useragent,
                                        string username,
                                        string password,
                                        Dictionary<string, string> headers,
                                        int timeoutSeconds,
                                        string proxyUrl,
                                        bool allowInsecureSSL)
    {
        bool isSavingToFile = !string.IsNullOrEmpty(outputFile);
        string tempOutputFile = isSavingToFile ? outputFile : Path.GetTempFileName();
        string scriptFile = Path.GetTempFileName() + ".ps1";

        try
        {
            // Build PowerShell script content
            StringBuilder script = new StringBuilder();

            // Set security protocol to support multiple TLS versions
            script.AppendLine("[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12 -bor [System.Net.SecurityProtocolType]::Tls11 -bor [System.Net.SecurityProtocolType]::Tls");

            // Skip certificate validation if requested (INSECURE)
            if (allowInsecureSSL)
            {
                script.AppendLine("Add-Type @'");
                script.AppendLine("using System.Net;");
                script.AppendLine("using System.Security.Cryptography.X509Certificates;");
                script.AppendLine("public class TrustAllCertsPolicy : ICertificatePolicy {");
                script.AppendLine("    public bool CheckValidationResult(ServicePoint srvPoint, X509Certificate certificate, WebRequest request, int certificateProblem) {");
                script.AppendLine("        return true;");
                script.AppendLine("    }");
                script.AppendLine("}");
                script.AppendLine("'@");
                script.AppendLine("[System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy");
                Debug.Print("WARNING: Using insecure SSL connections with PowerShell");
            }

            // Use Invoke-WebRequest for better status code reporting
            script.AppendLine("try {");
            script.AppendLine("    $params = @{");
            script.AppendLine("        Uri = '" + url.Replace("'", "''") + "'");
            script.AppendLine("        UseBasicParsing = $true");
            script.AppendLine("        TimeoutSec = " + timeoutSeconds);
            script.AppendLine("        UserAgent = '" + useragent.Replace("'", "''") + "'");

            // Add headers
            if (headers != null && headers.Count > 0)
            {
                script.AppendLine("        Headers = @{");
                bool isFirst = true;
                foreach (KeyValuePair<string, string> header in headers)
                {
                    if (!isFirst) script.AppendLine(";");
                    script.Append("            '" + header.Key.Replace("'", "''") + "' = '" + header.Value.Replace("'", "''") + "'");
                    isFirst = false;
                }
                script.AppendLine("");
                script.AppendLine("        }");
            }

            // Add credentials
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                script.AppendLine("        Credential = (New-Object System.Management.Automation.PSCredential('" + username.Replace("'", "''") + "', (ConvertTo-SecureString -String '" + password.Replace("'", "''") + "' -AsPlainText -Force)))");
            }

            // Add proxy
            if (!string.IsNullOrEmpty(proxyUrl))
            {
                script.AppendLine("        Proxy = '" + proxyUrl.Replace("'", "''") + "'");
            }

            script.AppendLine("    }");
            script.AppendLine("    $response = Invoke-WebRequest @params");

            // Output status code on first line, content follows
            script.AppendLine("    Write-Output ('STATUS_CODE:' + $response.StatusCode)");

            // Save content
            if (isSavingToFile)
            {
                script.AppendLine("    $response.Content | Set-Content -Path '" + tempOutputFile.Replace("'", "''") + "' -Encoding Byte");
                script.AppendLine("    if (Test-Path '" + tempOutputFile.Replace("'", "''") + "') { Write-Output 'Download completed successfully' }");
                script.AppendLine("    else { throw 'File was not created' }");
            }
            else
            {
                script.AppendLine("    $response.Content");  // Output content to stdout
            }

            script.AppendLine("} catch {");
            script.AppendLine("    if ($_.Exception.Response -ne $null) {");
            script.AppendLine("        Write-Output ('STATUS_CODE:' + [int]$_.Exception.Response.StatusCode)");
            script.AppendLine("    }");
            script.AppendLine("    Write-Error $_.Exception.Message");
            script.AppendLine("    exit 1");
            script.AppendLine("}");

            // Write script to file
            File.WriteAllText(scriptFile, script.ToString());

            // Create process start info for PowerShell
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "powershell.exe";
            startInfo.Arguments = "-ExecutionPolicy Bypass -File \"" + scriptFile + "\"";
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            // Execute PowerShell script
            StringBuilder stdOutput = new StringBuilder();
            StringBuilder stdError = new StringBuilder();
            int statusCode = 0;

            using (Process process = new Process())
            {
                process.StartInfo = startInfo;

                // Set up output and error handling
                AutoResetEvent outputWaitHandle = new AutoResetEvent(false);
                AutoResetEvent errorWaitHandle = new AutoResetEvent(false);

                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null)
                    {
                        // Check for status code output
                        if (e.Data.StartsWith("STATUS_CODE:"))
                        {
                            try
                            {
                                statusCode = int.Parse(e.Data.Substring("STATUS_CODE:".Length));
                            }
                            catch
                            {
                                // Ignore parsing errors
                            }
                        }
                        else
                        {
                            stdOutput.AppendLine(e.Data);
                        }
                    }
                    else
                    {
                        outputWaitHandle.Set();
                    }
                };

                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null)
                    {
                        stdError.AppendLine(e.Data);
                    }
                    else
                    {
                        errorWaitHandle.Set();
                    }
                };

                // Start the process
                process.Start();

                // Begin reading stdout and stderr asynchronously
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Wait for the process to exit
                if (process.WaitForExit(timeoutSeconds * 2000)) // Give PowerShell extra time
                {
                    // Wait for async reads to complete
                    outputWaitHandle.WaitOne(1000);
                    errorWaitHandle.WaitOne(1000);

                    // Check result
                    int exitCode = process.ExitCode;
                    string outputText = stdOutput.ToString().Trim();
                    string errorText = stdError.ToString().Trim();

                    // Set default status code if we couldn't extract it
                    if (statusCode == 0 && exitCode == 0)
                    {
                        statusCode = 200;  // Assume 200 OK if process succeeded
                    }

                    if (exitCode == 0)
                    {
                        // Success
                        if (isSavingToFile)
                        {
                            // Check if file exists and has content
                            if (File.Exists(tempOutputFile) && new FileInfo(tempOutputFile).Length > 0)
                            {
                                return DownloadResult.CreateSuccess(DownloadMethod.PowerShell, "", null, tempOutputFile, statusCode);
                            }
                            else
                            {
                                return DownloadResult.CreateFailure("PowerShell reported success but output file is empty or missing", DownloadMethod.PowerShell, statusCode);
                            }
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(outputText))
                            {
                                return DownloadResult.CreateSuccess(DownloadMethod.PowerShell, outputText, null, "", statusCode);
                            }
                            else
                            {
                                return DownloadResult.CreateFailure("PowerShell reported success but no content was returned", DownloadMethod.PowerShell, statusCode);
                            }
                        }
                    }
                    else
                    {
                        // Failure
                        string msg = "PowerShell process exited with code: " + exitCode;
                        if (!string.IsNullOrEmpty(errorText))
                        {
                            msg += Environment.NewLine + "PowerShell error: " + errorText;
                        }
                        return DownloadResult.CreateFailure(msg, DownloadMethod.PowerShell, statusCode);
                    }
                }
                else
                {
                    // Process timed out
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                        }
                    }
                    catch (Exception)
                    {
                        // Ignore errors killing the process
                    }

                    return DownloadResult.CreateFailure("PowerShell process timed out after " + (timeoutSeconds * 2) + " seconds", DownloadMethod.PowerShell);
                }
            }
        }
        catch (Exception ex)
        {
            return DownloadResult.CreateFailure("PowerShell execution error: " + ex.Message, DownloadMethod.PowerShell);
        }
        finally
        {
            // Clean up script file
            try
            {
                if (File.Exists(scriptFile))
                {
                    File.Delete(scriptFile);
                }

                // Clean up temp file if not saving to a file
                if (!isSavingToFile && File.Exists(tempOutputFile))
                {
                    File.Delete(tempOutputFile);
                }
            }
            catch (Exception)
            {
                // Ignore cleanup errors
            }
        }
    }

    /// <summary>
    /// Downloads a file directly to disk.
    /// </summary>
    /// <param name="url">The URL to download from.</param>
    /// <param name="outputFile">The local file path to save the downloaded content.</param>
    /// <param name="method">The primary download method to use.</param>
    /// <param name="enableFallback">Whether to try alternative methods if the primary method fails.</param>
    /// <param name="useragent">The User-Agent header value.</param>
    /// <param name="username">Optional username for authentication.</param>
    /// <param name="password">Optional password for authentication.</param>
    /// <param name="headers">Optional additional HTTP headers.</param>
    /// <param name="timeoutSeconds">Timeout in seconds.</param>
    /// <param name="proxyUrl">Optional proxy URL.</param>
    /// <param name="allowInsecureSSL">Whether to allow insecure SSL connections (not recommended for security reasons).</param>
    /// <returns>A DownloadResult object containing the result of the operation.</returns>
    public static DownloadResult DownloadFile(
                                 string url,
                                 string outputFile,
                                 DownloadMethod method = DownloadMethod.Auto,
                                 bool enableFallback = true,
                                 string useragent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                                 string username = "",
                                 string password = "",
                                 Dictionary<string, string> headers = null,
                                 int timeoutSeconds = 60,
                                 string proxyUrl = "",
                                 bool allowInsecureSSL = false)
    {
        // Use Download method with specified output file
        return Download(url, method, enableFallback, useragent, username, password, headers, timeoutSeconds, proxyUrl, outputFile, allowInsecureSSL);
    }

    /// <summary>
    /// Downloads binary data from a URL.
    /// </summary>
    /// <param name="url">The URL to download from.</param>
    /// <param name="method">The primary download method to use.</param>
    /// <param name="enableFallback">Whether to try alternative methods if the primary method fails.</param>
    /// <param name="useragent">The User-Agent header value.</param>
    /// <param name="username">Optional username for authentication.</param>
    /// <param name="password">Optional password for authentication.</param>
    /// <param name="headers">Optional additional HTTP headers.</param>
    /// <param name="timeoutSeconds">Timeout in seconds.</param>
    /// <param name="proxyUrl">Optional proxy URL.</param>
    /// <param name="allowInsecureSSL">Whether to allow insecure SSL connections (not recommended for security reasons).</param>
    /// <returns>The downloaded content as a byte array, or null if download failed.</returns>
    public static byte[] DownloadData(
                                 string url,
                                 DownloadMethod method = DownloadMethod.Auto,
                                 bool enableFallback = true,
                                 string useragent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                                 string username = "",
                                 string password = "",
                                 Dictionary<string, string> headers = null,
                                 int timeoutSeconds = 60,
                                 string proxyUrl = "",
                                 bool allowInsecureSSL = false)
    {
        try
        {
            // Create temporary file
            string tempFile = Path.GetTempFileName();

            // Download to temporary file
            DownloadResult result = Download(url, method, enableFallback, useragent, username, password, headers, timeoutSeconds, proxyUrl, tempFile, allowInsecureSSL);

            // Check if download was successful
            if (result.Success)
            {
                // Read file as binary data
                if (File.Exists(tempFile))
                {
                    byte[] data = File.ReadAllBytes(tempFile);

                    // Clean up temporary file
                    try
                    {
                        File.Delete(tempFile);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }

                    return data;
                }
            }

            // Log error details
            Debug.Print("DownloadData failed: " + result.ErrorMessage);

            // Clean up temporary file if download failed
            try
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }

            return null;
        }
        catch (Exception ex)
        {
            Debug.Print("DownloadData error: " + ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Checks if curl is available on the system.
    /// </summary>
    /// <returns>True if curl is available, False otherwise.</returns>
    public static bool IsCurlAvailable()
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo("curl.exe", "--version");
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            using (Process process = Process.Start(startInfo))
            {
                process.WaitForExit(5000);  // Wait up to 5 seconds
                return process.ExitCode == 0;
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if wget is available on the system.
    /// </summary>
    /// <returns>True if wget is available, False otherwise.</returns>
    public static bool IsWgetAvailable()
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo("wget.exe", "--version");
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            using (Process process = Process.Start(startInfo))
            {
                process.WaitForExit(5000);  // Wait up to 5 seconds
                return process.ExitCode == 0;
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the best available download method on the current system.
    /// </summary>
    /// <returns>The best available download method.</returns>
    public static DownloadMethod GetBestAvailableMethod()
    {
        if (IsCurlAvailable())
        {
            return DownloadMethod.Curl;
        }
        else if (IsWgetAvailable())
        {
            return DownloadMethod.Wget;
        }
        else
        {
            return DownloadMethod.PowerShell;
        }
    }

    /// <summary>
    /// Creates a URL with properly encoded query parameters.
    /// </summary>
    /// <param name="baseUrl">The base URL without query parameters.</param>
    /// <param name="parameters">Dictionary of parameter names and values.</param>
    /// <returns>A properly formatted URL with encoded query parameters.</returns>
    public static string BuildUrl(string baseUrl, Dictionary<string, string> parameters)
    {
        if (parameters == null || parameters.Count == 0)
        {
            return baseUrl;
        }

        StringBuilder url = new StringBuilder(baseUrl);

        // Add ? or & depending on whether the base URL already has parameters
        if (baseUrl.Contains("?"))
        {
            if (!baseUrl.EndsWith("?") && !baseUrl.EndsWith("&"))
            {
                url.Append("&");
            }
        }
        else
        {
            url.Append("?");
        }

        // Add encoded parameters
        bool isFirst = true;
        foreach (KeyValuePair<string, string> param in parameters)
        {
            if (!isFirst)
            {
                url.Append("&");
            }

            url.Append(Uri.EscapeDataString(param.Key));
            url.Append("=");
            url.Append(Uri.EscapeDataString(param.Value));

            isFirst = false;
        }

        return url.ToString();
    }

    /// <summary>
    /// Downloads a string with explicit encoding from a URL.
    /// Use this method when specific text encoding is important.
    /// </summary>
    /// <param name="url">The URL to download from.</param>
    /// <param name="encoding">The encoding to use for the downloaded text.</param>
    /// <param name="method">The primary download method to use.</param>
    /// <param name="defaultValue">Default value to return if download fails.</param>
    /// <param name="allowInsecureSSL">Whether to allow insecure SSL connections (not recommended for security reasons).</param>
    /// <returns>The downloaded content as a string or the default value if download failed.</returns>
    public static string DownloadStringWithEncoding(
                                               string url,
                                               System.Text.Encoding encoding,
                                               DownloadMethod method = DownloadMethod.Auto,
                                               string defaultValue = "",
                                               bool allowInsecureSSL = false)
    {
        // Create temporary file
        string tempFile = Path.GetTempFileName();

        try
        {
            // Download to temporary file
            DownloadResult result = Download(url, method, true, "Mozilla/5.0", "", "", null, 60, "", tempFile, allowInsecureSSL);

            // Check if download was successful
            if (result.Success)
            {
                // Read file with specified encoding
                if (File.Exists(tempFile))
                {
                    string content = File.ReadAllText(tempFile, encoding);
                    return content;
                }
            }

            Debug.Print("DownloadStringWithEncoding failed: " + result.ErrorMessage);
            return defaultValue;
        }
        catch (Exception ex)
        {
            Debug.Print("DownloadStringWithEncoding error: " + ex.Message);
            return defaultValue;
        }
        finally
        {
            // Clean up temporary file
            try
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    /// <summary>
    /// Verifies if all required external tools are available for each download method.
    /// </summary>
    /// <returns>A dictionary with each method and its availability status.</returns>
    public static Dictionary<DownloadMethod, bool> VerifyDependencies()
    {
        Dictionary<DownloadMethod, bool> result = new Dictionary<DownloadMethod, bool>();

        // Check curl
        result[DownloadMethod.Curl] = IsCurlAvailable();

        // Check wget
        result[DownloadMethod.Wget] = IsWgetAvailable();

        // Check PowerShell (always available on modern Windows)
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo("powershell.exe", "-Command \"exit 0\"");
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;

            using (Process process = Process.Start(startInfo))
            {
                process.WaitForExit(5000);
                result[DownloadMethod.PowerShell] = (process.ExitCode == 0);
            }
        }
        catch
        {
            result[DownloadMethod.PowerShell] = false;
        }

        return result;
    }
}
