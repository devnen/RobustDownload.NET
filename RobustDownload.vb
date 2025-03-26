''' <summary>
''' A robust class for downloading content from URLs using various methods including curl, wget, and PowerShell.
''' DEPENDENCIES: Requires curl.exe and/or wget.exe to be available in the system's PATH for the respective 
''' download methods to function. PowerShell.exe is required for the PowerShell method.
''' </summary>
Public Class RobustDownload
    ''' <summary>
    ''' Defines the available download methods.
    ''' </summary>
    Public Enum DownloadMethod
        Auto = 0        ' Automatically choose the best method
        Curl = 1        ' Use curl as primary method
        Wget = 2        ' Use wget as primary method
        PowerShell = 3  ' Use PowerShell WebClient as primary method
    End Enum
    
    ''' <summary>
    ''' Class to hold the result of a download operation.
    ''' </summary>
    Public Class DownloadResult
        ''' <summary>
        ''' Whether the download was successful.
        ''' </summary>
        Public Property Success As Boolean = False
        
        ''' <summary>
        ''' The downloaded content as a string (if text content was downloaded).
        ''' NOTE: When content is captured directly from command-line tools (not from file),
        ''' the encoding depends on the console output encoding of the external tool.
        ''' Use DownloadStringWithEncoding for explicit encoding control.
        ''' </summary>
        Public Property Content As String = ""
        
        ''' <summary>
        ''' The downloaded content as a byte array (if binary content was downloaded).
        ''' </summary>
        Public Property Data As Byte() = Nothing
        
        ''' <summary>
        ''' Path to the saved file (if content was saved to a file).
        ''' </summary>
        Public Property FilePath As String = ""
        
        ''' <summary>
        ''' Error message if the download failed.
        ''' </summary>
        Public Property ErrorMessage As String = ""
        
        ''' <summary>
        ''' The download method that was successfully used.
        ''' </summary>
        Public Property UsedMethod As DownloadMethod = DownloadMethod.Auto
        
        ''' <summary>
        ''' HTTP status code if available. Note: This might be approximate as not all tools 
        ''' provide direct access to the status code. Default is 200 for success, 0 for failure.
        ''' </summary>
        Public Property StatusCode As Integer = 0
        
        ''' <summary>
        ''' Duration of the download operation in milliseconds.
        ''' </summary>
        Public Property DurationMs As Long = 0
        
        ''' <summary>
        ''' Collection of errors from all attempted methods if multiple methods were tried.
        ''' </summary>
        Public Property AllErrors As New Dictionary(Of DownloadMethod, String)
        
        ''' <summary>
        ''' Creates a new success result.
        ''' </summary>
        Public Shared Function CreateSuccess(method As DownloadMethod, Optional content As String = "", 
                                          Optional data As Byte() = Nothing, Optional filePath As String = "",
                                          Optional statusCode As Integer = 200,
                                          Optional durationMs As Long = 0) As DownloadResult
            Return New DownloadResult() With {
                .Success = True,
                .Content = content,
                .Data = data,
                .FilePath = filePath,
                .UsedMethod = method,
                .StatusCode = statusCode,
                .DurationMs = durationMs
            }
        End Function
        
        ''' <summary>
        ''' Creates a new failure result.
        ''' </summary>
        Public Shared Function CreateFailure(errorMessage As String, method As DownloadMethod, 
                                          Optional statusCode As Integer = 0,
                                          Optional durationMs As Long = 0) As DownloadResult
            Return New DownloadResult() With {
                .Success = False,
                .ErrorMessage = errorMessage,
                .UsedMethod = method,
                .StatusCode = statusCode,
                .DurationMs = durationMs
            }
        End Function
    End Class
    
    ''' <summary>
    ''' Downloads content from a URL using the specified method with fallback options.
    ''' Returns a DownloadResult object containing the result status and content.
    ''' </summary>
    ''' <param name="url">The URL to download content from.</param>
    ''' <param name="method">The primary download method to use.</param>
    ''' <param name="enableFallback">Whether to try alternative methods if the primary method fails.</param>
    ''' <param name="useragent">The User-Agent header value.</param>
    ''' <param name="username">Optional username for authentication.</param>
    ''' <param name="password">Optional password for authentication.</param>
    ''' <param name="headers">Optional additional HTTP headers.</param>
    ''' <param name="timeoutSeconds">Timeout in seconds.</param>
    ''' <param name="proxyUrl">Optional proxy URL.</param>
    ''' <param name="outputFile">Optional path to save the downloaded content.</param>
    ''' <param name="allowInsecureSSL">Whether to allow insecure SSL connections (not recommended for security reasons).</param>
    ''' <returns>A DownloadResult object containing the result of the operation.</returns>
    Public Shared Function Download(
                                  url As String,
                                  Optional method As DownloadMethod = DownloadMethod.Auto,
                                  Optional enableFallback As Boolean = True,
                                  Optional useragent As String = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                                  Optional username As String = "",
                                  Optional password As String = "",
                                  Optional headers As Dictionary(Of String, String) = Nothing,
                                  Optional timeoutSeconds As Integer = 60,
                                  Optional proxyUrl As String = "",
                                  Optional outputFile As String = "",
                                  Optional allowInsecureSSL As Boolean = False) As DownloadResult
        
        ' Track operation time
        Dim startTime As Long = DateTime.Now.Ticks
        
        ' Flag to determine if we're saving to a file
        Dim isSavingToFile As Boolean = Not String.IsNullOrEmpty(outputFile)
        
        ' Create a temporary output file if needed
        Dim tempOutputFile As String = ""
        If isSavingToFile Then
            tempOutputFile = outputFile
        End If
        
        ' Determine methods to try based on primary method and fallback setting
        Dim methodsToTry As New List(Of DownloadMethod)
        
        If method = DownloadMethod.Auto Then
            ' Try curl first, then wget, then PowerShell
            methodsToTry.Add(DownloadMethod.Curl)
            methodsToTry.Add(DownloadMethod.Wget)
            methodsToTry.Add(DownloadMethod.PowerShell)
        Else
            ' Start with the specified method
            methodsToTry.Add(method)
            
            ' Add fallbacks if enabled
            If enableFallback Then
                If method <> DownloadMethod.Curl Then methodsToTry.Add(DownloadMethod.Curl)
                If method <> DownloadMethod.Wget Then methodsToTry.Add(DownloadMethod.Wget)
                If method <> DownloadMethod.PowerShell Then methodsToTry.Add(DownloadMethod.PowerShell)
            End If
        End If
        
        ' Create a result to collect errors if we need to try multiple methods
        Dim finalResult As New DownloadResult()
        
        ' Try each method in order until one succeeds
        For Each downloadMethod In methodsToTry
            Dim result As DownloadResult = Nothing
            
            Select Case downloadMethod
                Case DownloadMethod.Curl
                    If IsCurlAvailable() Then
                        result = DownloadWithCurl(url, tempOutputFile, useragent, username, password, headers, timeoutSeconds, proxyUrl, allowInsecureSSL)
                        If result.Success Then
                            ' Calculate duration
                            result.DurationMs = (DateTime.Now.Ticks - startTime) \ 10000
                            Return result
                        Else
                            ' Store the error for later reporting
                            finalResult.AllErrors(DownloadMethod.Curl) = result.ErrorMessage
                        End If
                    End If
                
                Case DownloadMethod.Wget
                    If IsWgetAvailable() Then
                        result = DownloadWithWget(url, tempOutputFile, useragent, username, password, headers, timeoutSeconds, proxyUrl, allowInsecureSSL)
                        If result.Success Then
                            ' Calculate duration
                            result.DurationMs = (DateTime.Now.Ticks - startTime) \ 10000
                            Return result
                        Else
                            ' Store the error for later reporting
                            finalResult.AllErrors(DownloadMethod.Wget) = result.ErrorMessage
                        End If
                    End If
                
                Case DownloadMethod.PowerShell
                    result = DownloadWithPowerShell(url, tempOutputFile, useragent, username, password, headers, timeoutSeconds, proxyUrl, allowInsecureSSL)
                    If result.Success Then
                        ' Calculate duration
                        result.DurationMs = (DateTime.Now.Ticks - startTime) \ 10000
                        Return result
                    Else
                        ' Store the error for later reporting
                        finalResult.AllErrors(DownloadMethod.PowerShell) = result.ErrorMessage
                    End If
            End Select
        Next
        
        ' If we get here, all methods failed
        Dim errorMsg As New StringBuilder("All download methods failed for URL: " & url)
        
        ' Add detailed errors from each method that was tried
        If finalResult.AllErrors.Count > 0 Then
            errorMsg.AppendLine()
            errorMsg.AppendLine("Detailed errors by method:")
            For Each err In finalResult.AllErrors
                errorMsg.AppendLine($"- {err.Key}: {err.Value}")
            Next
        End If
        
        Return DownloadResult.CreateFailure(errorMsg.ToString(), method, 0, (DateTime.Now.Ticks - startTime) \ 10000)
    End Function
    
    ''' <summary>
    ''' Downloads a string from a URL using the best available method.
    ''' This is a simplified version of Download that returns just the string content.
    ''' NOTE: When capturing content directly from command-line tools, the text encoding
    ''' depends on the console output encoding. Use DownloadStringWithEncoding for 
    ''' explicit encoding control.
    ''' </summary>
    ''' <param name="url">The URL to download from.</param>
    ''' <param name="method">The primary download method to use.</param>
    ''' <param name="defaultValue">Default value to return if download fails.</param>
    ''' <param name="useragent">The User-Agent header value.</param>
    ''' <returns>The downloaded string or defaultValue if download failed.</returns>
    Public Shared Function DownloadString(
                                       url As String,
                                       Optional method As DownloadMethod = DownloadMethod.Auto,
                                       Optional defaultValue As String = "",
                                       Optional useragent As String = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0.0.0") As String
        
        Dim result As DownloadResult = Download(url, method, True, useragent)
        If result.Success Then
            Return result.Content
        Else
            Debug.Print("DownloadString failed: " & result.ErrorMessage)
            Return defaultValue
        End If
    End Function
    
    ''' <summary>
    ''' Downloads content from a URL using curl.
    ''' </summary>
    ''' <returns>A DownloadResult object containing the result of the operation.</returns>
    Private Shared Function DownloadWithCurl(
                                          url As String,
                                          outputFile As String,
                                          useragent As String,
                                          username As String,
                                          password As String,
                                          headers As Dictionary(Of String, String),
                                          timeoutSeconds As Integer,
                                          proxyUrl As String,
                                          allowInsecureSSL As Boolean) As DownloadResult
        
        Dim stdOutput As New StringBuilder()
        Dim stdError As New StringBuilder()
        Dim statusCode As Integer = 0
        Dim isSavingToFile As Boolean = Not String.IsNullOrEmpty(outputFile)
        
        Try
            ' Create process start info
            Dim startInfo As New ProcessStartInfo()
            startInfo.FileName = "curl.exe"
            startInfo.CreateNoWindow = True
            startInfo.UseShellExecute = False
            startInfo.RedirectStandardOutput = True
            startInfo.RedirectStandardError = True
            
            ' Build arguments
            Dim args As New StringBuilder()
            
            ' Basic curl options
            args.Append("-s -L ")  ' -s: silent, -L: follow redirects
            
            ' Add write-out option to get status code
            args.Append("-w ""%{http_code}"" ")
            
            ' Add insecure SSL flag only if explicitly allowed
            If allowInsecureSSL Then
                args.Append("-k ")  ' -k: insecure, allows connections to SSL sites without certificates
                Debug.Print("WARNING: Using insecure SSL connections with curl")
            End If
            
            ' User agent
            args.Append("-A """).Append(useragent).Append(""" ")
            
            ' Authentication
            If Not String.IsNullOrEmpty(username) AndAlso Not String.IsNullOrEmpty(password) Then
                args.Append("--user ").Append(username).Append(":").Append(password).Append(" ")
            End If
            
            ' Proxy
            If Not String.IsNullOrEmpty(proxyUrl) Then
                args.Append("--proxy ").Append(proxyUrl).Append(" ")
            End If
            
            ' Headers
            If headers IsNot Nothing AndAlso headers.Count > 0 Then
                For Each header In headers
                    args.Append("-H """).Append(header.Key).Append(": ").Append(header.Value).Append(""" ")
                Next
            End If
            
            ' Timeout - add both connect timeout and maximum operation time
            args.Append("--connect-timeout ").Append(timeoutSeconds).Append(" ")
            args.Append("--max-time ").Append(timeoutSeconds).Append(" ")
            
            ' URL
            args.Append("""").Append(url).Append(""" ")
            
            ' Output file if specified
            If isSavingToFile Then
                args.Append("-o """).Append(outputFile).Append(""" ")
            End If
            
            startInfo.Arguments = args.ToString()
            
            ' Execute curl with proper stream handling
            Using process As New Process()
                process.StartInfo = startInfo
                
                ' Set up output and error handling
                Dim outputWaitHandle As New AutoResetEvent(False)
                Dim errorWaitHandle As New AutoResetEvent(False)
                
                AddHandler process.OutputDataReceived,
                    Sub(sender, e)
                        If e.Data IsNot Nothing Then
                            ' The last line should be the status code when using -w "%{http_code}"
                            If Integer.TryParse(e.Data.Trim(), statusCode) Then
                                ' This is the status code, don't add to content
                            Else
                                stdOutput.AppendLine(e.Data)
                            End If
                        Else
                            outputWaitHandle.Set()
                        End If
                    End Sub
                
                AddHandler process.ErrorDataReceived,
                    Sub(sender, e)
                        If e.Data IsNot Nothing Then
                            stdError.AppendLine(e.Data)
                        Else
                            errorWaitHandle.Set()
                        End If
                    End Sub
                
                ' Start the process
                process.Start()
                
                ' Begin reading stdout and stderr asynchronously
                process.BeginOutputReadLine()
                process.BeginErrorReadLine()
                
                ' Wait for the process to exit
                If process.WaitForExit(timeoutSeconds * 1000) Then
                    ' Wait for async reads to complete
                    outputWaitHandle.WaitOne(1000)
                    errorWaitHandle.WaitOne(1000)
                    
                    ' Check result
                    Dim exitCode As Integer = process.ExitCode
                    Dim errorText As String = stdError.ToString().Trim()
                    
                    If exitCode = 0 Then
                        ' Set default status code if we couldn't parse it
                        If statusCode = 0 Then statusCode = 200
                        
                        ' Check if HTTP status indicates success (2xx)
                        If statusCode >= 200 AndAlso statusCode < 300 Then
                            ' Success
                            If isSavingToFile Then
                                ' Check if file exists and has content
                                If File.Exists(outputFile) AndAlso New FileInfo(outputFile).Length > 0 Then
                                    Return DownloadResult.CreateSuccess(DownloadMethod.Curl, "", Nothing, outputFile, statusCode)
                                Else
                                    Return DownloadResult.CreateFailure("Curl reported success but output file is empty or missing", DownloadMethod.Curl, statusCode)
                                End If
                            Else
                                ' Return the content from stdout
                                Dim content As String = stdOutput.ToString()
                                If Not String.IsNullOrEmpty(content) Then
                                    Return DownloadResult.CreateSuccess(DownloadMethod.Curl, content, Nothing, "", statusCode)
                                Else
                                    Return DownloadResult.CreateFailure("Curl reported success but no content was returned", DownloadMethod.Curl, statusCode)
                                End If
                            End If
                        Else
                            ' HTTP error
                            Return DownloadResult.CreateFailure("HTTP error: " & statusCode, DownloadMethod.Curl, statusCode)
                        End If
                    Else
                        ' Process error
                        Dim msg As String = "Curl process exited with code: " & exitCode
                        If Not String.IsNullOrEmpty(errorText) Then
                            msg &= Environment.NewLine & "Curl error: " & errorText
                        End If
                        Return DownloadResult.CreateFailure(msg, DownloadMethod.Curl, statusCode)
                    End If
                Else
                    ' Process timed out
                    Try
                        If Not process.HasExited Then
                            process.Kill()
                        End If
                    Catch ex As Exception
                        ' Ignore errors killing the process
                    End Try
                    
                    Return DownloadResult.CreateFailure("Curl process timed out after " & timeoutSeconds & " seconds", DownloadMethod.Curl)
                End If
            End Using
        Catch ex As Exception
            Return DownloadResult.CreateFailure("Curl execution error: " & ex.Message, DownloadMethod.Curl)
        End Try
    End Function
    
    ''' <summary>
    ''' Downloads content from a URL using wget.
    ''' </summary>
    ''' <returns>A DownloadResult object containing the result of the operation.</returns>
    Private Shared Function DownloadWithWget(
                                          url As String,
                                          outputFile As String,
                                          useragent As String,
                                          username As String,
                                          password As String,
                                          headers As Dictionary(Of String, String),
                                          timeoutSeconds As Integer,
                                          proxyUrl As String,
                                          allowInsecureSSL As Boolean) As DownloadResult
        
        Dim stdOutput As New StringBuilder()
        Dim stdError As New StringBuilder()
        Dim isSavingToFile As Boolean = Not String.IsNullOrEmpty(outputFile)
        Dim tempOutputFile As String = If(isSavingToFile, outputFile, Path.GetTempFileName())
        Dim statusCode As Integer = 0
        
        Try
            ' Create process start info
            Dim startInfo As New ProcessStartInfo()
            startInfo.FileName = "wget.exe"
            startInfo.CreateNoWindow = True
            startInfo.UseShellExecute = False
            startInfo.RedirectStandardOutput = True
            startInfo.RedirectStandardError = True
            
            ' Set proxy environment variables if specified
            If Not String.IsNullOrEmpty(proxyUrl) Then
                startInfo.EnvironmentVariables("http_proxy") = proxyUrl
                startInfo.EnvironmentVariables("https_proxy") = proxyUrl
            End If
            
            ' Build arguments
            Dim args As New StringBuilder()
            
            ' Basic wget options - quiet mode but show server response
            args.Append("-q -S ")  ' -q: quiet, -S: show server response in stderr
            
            ' Add insecure SSL flag only if explicitly allowed
            If allowInsecureSSL Then
                args.Append("--no-check-certificate ")
                Debug.Print("WARNING: Using insecure SSL connections with wget")
            End If
            
            ' User agent
            args.Append("--user-agent=""").Append(useragent).Append(""" ")
            
            ' Authentication
            If Not String.IsNullOrEmpty(username) AndAlso Not String.IsNullOrEmpty(password) Then
                args.Append("--http-user=").Append(username).Append(" ")
                args.Append("--http-password=").Append(password).Append(" ")
            End If
            
            ' Headers
            If headers IsNot Nothing AndAlso headers.Count > 0 Then
                For Each header In headers
                    args.Append("--header=""").Append(header.Key).Append(": ").Append(header.Value).Append(""" ")
                Next
            End If
            
            ' Timeout
            args.Append("-T ").Append(timeoutSeconds).Append(" ")
            
            ' Output to stdout if not saving to file
            If Not isSavingToFile Then
                args.Append("-O - ")  ' Output to stdout
            Else
                args.Append("-O """).Append(tempOutputFile).Append(""" ")
            End If
            
            ' URL
            args.Append("""").Append(url).Append("""")
            
            startInfo.Arguments = args.ToString()
            
            ' Execute wget with proper stream handling
            Using process As New Process()
                process.StartInfo = startInfo
                
                ' Set up output and error handling
                Dim outputWaitHandle As New AutoResetEvent(False)
                Dim errorWaitHandle As New AutoResetEvent(False)
                
                AddHandler process.OutputDataReceived,
                    Sub(sender, e)
                        If e.Data IsNot Nothing Then
                            stdOutput.AppendLine(e.Data)
                        Else
                            outputWaitHandle.Set()
                        End If
                    End Sub
                
                AddHandler process.ErrorDataReceived,
                    Sub(sender, e)
                        If e.Data IsNot Nothing Then
                            ' Try to extract the status code from server response
                            ' Wget shows HTTP responses in stderr with -S flag
                            If e.Data.Contains("HTTP/") AndAlso e.Data.Contains(" ") Then
                                Try
                                    Dim parts = e.Data.Trim().Split(" "c)
                                    If parts.Length >= 2 Then
                                        Integer.TryParse(parts(1), statusCode)
                                    End If
                                Catch
                                    ' Ignore parsing errors
                                End Try
                            End If
                            
                            stdError.AppendLine(e.Data)
                        Else
                            errorWaitHandle.Set()
                        End If
                    End Sub
                
                ' Start the process
                process.Start()
                
                ' Begin reading stdout and stderr asynchronously
                process.BeginOutputReadLine()
                process.BeginErrorReadLine()
                
                ' Wait for the process to exit
                If process.WaitForExit(timeoutSeconds * 1000) Then
                    ' Wait for async reads to complete
                    outputWaitHandle.WaitOne(1000)
                    errorWaitHandle.WaitOne(1000)
                    
                    ' Check result
                    Dim exitCode As Integer = process.ExitCode
                    Dim errorText As String = stdError.ToString().Trim()
                    
                    ' Set default status code if we couldn't extract it
                    If statusCode = 0 AndAlso exitCode = 0 Then
                        statusCode = 200  ' Assume 200 OK if process succeeded
                    End If
                    
                    If exitCode = 0 Then
                        ' Success
                        If isSavingToFile Then
                            ' Check if file exists and has content
                            If File.Exists(tempOutputFile) AndAlso New FileInfo(tempOutputFile).Length > 0 Then
                                Return DownloadResult.CreateSuccess(DownloadMethod.Wget, "", Nothing, tempOutputFile, statusCode)
                            Else
                                Return DownloadResult.CreateFailure("Wget reported success but output file is empty or missing", DownloadMethod.Wget, statusCode)
                            End If
                        Else
                            ' Return the content from stdout
                            Dim content As String = stdOutput.ToString()
                            
                            ' Clean up temporary file
                            Try
                                If File.Exists(tempOutputFile) Then
                                    File.Delete(tempOutputFile)
                                End If
                            Catch ex As Exception
                                ' Ignore temp file cleanup errors
                            End Try
                            
                            If Not String.IsNullOrEmpty(content) Then
                                Return DownloadResult.CreateSuccess(DownloadMethod.Wget, content, Nothing, "", statusCode)
                            Else
                                Return DownloadResult.CreateFailure("Wget reported success but no content was returned", DownloadMethod.Wget, statusCode)
                            End If
                        End If
                    Else
                        ' Failure
                        ' Clean up temporary file if we created one
                        If Not isSavingToFile Then
                            Try
                                If File.Exists(tempOutputFile) Then
                                    File.Delete(tempOutputFile)
                                End If
                            Catch ex As Exception
                                ' Ignore temp file cleanup errors
                            End Try
                        End If
                        
                        Dim msg As String = "Wget process exited with code: " & exitCode
                        If Not String.IsNullOrEmpty(errorText) Then
                            msg &= Environment.NewLine & "Wget error: " & errorText
                        End If
                        Return DownloadResult.CreateFailure(msg, DownloadMethod.Wget, statusCode)
                    End If
                Else
                    ' Process timed out
                    Try
                        If Not process.HasExited Then
                            process.Kill()
                        End If
                    Catch ex As Exception
                        ' Ignore errors killing the process
                    End Try
                    
                    ' Clean up temporary file if we created one
                    If Not isSavingToFile Then
                        Try
                            If File.Exists(tempOutputFile) Then
                                File.Delete(tempOutputFile)
                            End If
                        Catch ex As Exception
                            ' Ignore temp file cleanup errors
                        End Try
                    End If
                    
                    Return DownloadResult.CreateFailure("Wget process timed out after " & timeoutSeconds & " seconds", DownloadMethod.Wget)
                End If
            End Using
        Catch ex As Exception
            ' Clean up temporary file if we created one
            If Not isSavingToFile Then
                Try
                    If File.Exists(tempOutputFile) Then
                        File.Delete(tempOutputFile)
                    End If
                Catch
                    ' Ignore temp file cleanup errors
                End Try
            End If
            
            Return DownloadResult.CreateFailure("Wget execution error: " & ex.Message, DownloadMethod.Wget)
        End Try
    End Function
    
    ''' <summary>
    ''' Downloads content from a URL using PowerShell.
    ''' </summary>
    ''' <returns>A DownloadResult object containing the result of the operation.</returns>
    Private Shared Function DownloadWithPowerShell(
                                                url As String,
                                                outputFile As String,
                                                useragent As String,
                                                username As String,
                                                password As String,
                                                headers As Dictionary(Of String, String),
                                                timeoutSeconds As Integer,
                                                proxyUrl As String,
                                                allowInsecureSSL As Boolean) As DownloadResult
        
        Dim isSavingToFile As Boolean = Not String.IsNullOrEmpty(outputFile)
        Dim tempOutputFile As String = If(isSavingToFile, outputFile, Path.GetTempFileName())
        Dim scriptFile As String = Path.GetTempFileName() & ".ps1"
        
        Try
            ' Build PowerShell script content
            Dim script As New StringBuilder()
            
            ' Set security protocol to support multiple TLS versions
            script.AppendLine("[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12 -bor [System.Net.SecurityProtocolType]::Tls11 -bor [System.Net.SecurityProtocolType]::Tls")
            
            ' Skip certificate validation if requested (INSECURE)
            If allowInsecureSSL Then
                script.AppendLine("Add-Type @'")
                script.AppendLine("using System.Net;")
                script.AppendLine("using System.Security.Cryptography.X509Certificates;")
                script.AppendLine("public class TrustAllCertsPolicy : ICertificatePolicy {")
                script.AppendLine("    public bool CheckValidationResult(ServicePoint srvPoint, X509Certificate certificate, WebRequest request, int certificateProblem) {")
                script.AppendLine("        return true;")
                script.AppendLine("    }")
                script.AppendLine("}")
                script.AppendLine("'@")
                script.AppendLine("[System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy")
                Debug.Print("WARNING: Using insecure SSL connections with PowerShell")
            End If
            
            ' Use Invoke-WebRequest for better status code reporting
            script.AppendLine("try {")
            script.AppendLine("    $params = @{")
            script.AppendLine("        Uri = '" & url.Replace("'", "''") & "'")
            script.AppendLine("        UseBasicParsing = $true")
            script.AppendLine("        TimeoutSec = " & timeoutSeconds)
            script.AppendLine("        UserAgent = '" & useragent.Replace("'", "''") & "'")
            
            ' Add headers
            If headers IsNot Nothing AndAlso headers.Count > 0 Then
                script.AppendLine("        Headers = @{")
                Dim isFirst As Boolean = True
                For Each header In headers
                    If Not isFirst Then script.AppendLine(";")
                    script.Append("            '" & header.Key.Replace("'", "''") & "' = '" & header.Value.Replace("'", "''") & "'")
                    isFirst = False
                Next
                script.AppendLine("")
                script.AppendLine("        }")
            End If
            
            ' Add credentials
            If Not String.IsNullOrEmpty(username) AndAlso Not String.IsNullOrEmpty(password) Then
                script.AppendLine("        Credential = (New-Object System.Management.Automation.PSCredential('" & username.Replace("'", "''") & "', (ConvertTo-SecureString -String '" & password.Replace("'", "''") & "' -AsPlainText -Force)))")
            End If
            
            ' Add proxy
            If Not String.IsNullOrEmpty(proxyUrl) Then
                script.AppendLine("        Proxy = '" & proxyUrl.Replace("'", "''") & "'")
            End If
            
            script.AppendLine("    }")
            script.AppendLine("    $response = Invoke-WebRequest @params")
            
            ' Output status code on first line, content follows
            script.AppendLine("    Write-Output ('STATUS_CODE:' + $response.StatusCode)")
            
            ' Save content
            If isSavingToFile Then
                script.AppendLine("    $response.Content | Set-Content -Path '" & tempOutputFile.Replace("'", "''") & "' -Encoding Byte")
                script.AppendLine("    if (Test-Path '" & tempOutputFile.Replace("'", "''") & "') { Write-Output 'Download completed successfully' }")
                script.AppendLine("    else { throw 'File was not created' }")
            Else
                script.AppendLine("    $response.Content")  ' Output content to stdout
            End If
            
            script.AppendLine("} catch {")
            script.AppendLine("    if ($_.Exception.Response -ne $null) {")
            script.AppendLine("        Write-Output ('STATUS_CODE:' + [int]$_.Exception.Response.StatusCode)")
            script.AppendLine("    }")
            script.AppendLine("    Write-Error $_.Exception.Message")
            script.AppendLine("    exit 1")
            script.AppendLine("}")
            
            ' Write script to file
            File.WriteAllText(scriptFile, script.ToString())
            
            ' Create process start info for PowerShell
            Dim startInfo As New ProcessStartInfo()
            startInfo.FileName = "powershell.exe"
            startInfo.Arguments = "-ExecutionPolicy Bypass -File """ & scriptFile & """"
            startInfo.CreateNoWindow = True
            startInfo.UseShellExecute = False
            startInfo.RedirectStandardOutput = True
            startInfo.RedirectStandardError = True
            
            ' Execute PowerShell script
            Dim stdOutput As New StringBuilder()
            Dim stdError As New StringBuilder()
            Dim statusCode As Integer = 0
            
            Using process As New Process()
                process.StartInfo = startInfo
                
                ' Set up output and error handling
                Dim outputWaitHandle As New AutoResetEvent(False)
                Dim errorWaitHandle As New AutoResetEvent(False)
                
                AddHandler process.OutputDataReceived,
                    Sub(sender, e)
                        If e.Data IsNot Nothing Then
                            ' Check for status code output
                            If e.Data.StartsWith("STATUS_CODE:") Then
                                Try
                                    statusCode = Integer.Parse(e.Data.Substring("STATUS_CODE:".Length))
                                Catch
                                    ' Ignore parsing errors
                                End Try
                            Else
                                stdOutput.AppendLine(e.Data)
                            End If
                        Else
                            outputWaitHandle.Set()
                        End If
                    End Sub
                
                AddHandler process.ErrorDataReceived,
                    Sub(sender, e)
                        If e.Data IsNot Nothing Then
                            stdError.AppendLine(e.Data)
                        Else
                            errorWaitHandle.Set()
                        End If
                    End Sub
                
                ' Start the process
                process.Start()
                
                ' Begin reading stdout and stderr asynchronously
                process.BeginOutputReadLine()
                process.BeginErrorReadLine()
                
                ' Wait for the process to exit
                If process.WaitForExit(timeoutSeconds * 2000) Then ' Give PowerShell extra time
                    ' Wait for async reads to complete
                    outputWaitHandle.WaitOne(1000)
                    errorWaitHandle.WaitOne(1000)
                    
                    ' Check result
                    Dim exitCode As Integer = process.ExitCode
                    Dim outputText As String = stdOutput.ToString().Trim()
                    Dim errorText As String = stdError.ToString().Trim()
                    
                    ' Set default status code if we couldn't extract it
                    If statusCode = 0 AndAlso exitCode = 0 Then
                        statusCode = 200  ' Assume 200 OK if process succeeded
                    End If
                    
                    If exitCode = 0 Then
                        ' Success
                        If isSavingToFile Then
                            ' Check if file exists and has content
                            If File.Exists(tempOutputFile) AndAlso New FileInfo(tempOutputFile).Length > 0 Then
                                Return DownloadResult.CreateSuccess(DownloadMethod.PowerShell, "", Nothing, tempOutputFile, statusCode)
                            Else
                                Return DownloadResult.CreateFailure("PowerShell reported success but output file is empty or missing", DownloadMethod.PowerShell, statusCode)
                            End If
                        Else
                            If Not String.IsNullOrEmpty(outputText) Then
                                Return DownloadResult.CreateSuccess(DownloadMethod.PowerShell, outputText, Nothing, "", statusCode)
                            Else
                                Return DownloadResult.CreateFailure("PowerShell reported success but no content was returned", DownloadMethod.PowerShell, statusCode)
                            End If
                        End If
                    Else
                        ' Failure
                        Dim msg As String = "PowerShell process exited with code: " & exitCode
                        If Not String.IsNullOrEmpty(errorText) Then
                            msg &= Environment.NewLine & "PowerShell error: " & errorText
                        End If
                        Return DownloadResult.CreateFailure(msg, DownloadMethod.PowerShell, statusCode)
                    End If
                Else
                    ' Process timed out
                    Try
                        If Not process.HasExited Then
                            process.Kill()
                        End If
                    Catch ex As Exception
                        ' Ignore errors killing the process
                    End Try
                    
                    Return DownloadResult.CreateFailure("PowerShell process timed out after " & (timeoutSeconds * 2) & " seconds", DownloadMethod.PowerShell)
                End If
            End Using
        Catch ex As Exception
            Return DownloadResult.CreateFailure("PowerShell execution error: " & ex.Message, DownloadMethod.PowerShell)
        Finally
            ' Clean up script file
            Try
                If File.Exists(scriptFile) Then
                    File.Delete(scriptFile)
                End If
                
                ' Clean up temp file if not saving to a file
                If Not isSavingToFile AndAlso File.Exists(tempOutputFile) Then
                    File.Delete(tempOutputFile)
                End If
            Catch ex As Exception
                ' Ignore cleanup errors
            End Try
        End Try
    End Function
    
    ''' <summary>
    ''' Downloads a file directly to disk.
    ''' </summary>
    ''' <param name="url">The URL to download from.</param>
    ''' <param name="outputFile">The local file path to save the downloaded content.</param>
    ''' <param name="method">The primary download method to use.</param>
    ''' <param name="enableFallback">Whether to try alternative methods if the primary method fails.</param>
    ''' <param name="useragent">The User-Agent header value.</param>
    ''' <param name="username">Optional username for authentication.</param>
    ''' <param name="password">Optional password for authentication.</param>
    ''' <param name="headers">Optional additional HTTP headers.</param>
    ''' <param name="timeoutSeconds">Timeout in seconds.</param>
    ''' <param name="proxyUrl">Optional proxy URL.</param>
    ''' <param name="allowInsecureSSL">Whether to allow insecure SSL connections (not recommended for security reasons).</param>
    ''' <returns>A DownloadResult object containing the result of the operation.</returns>
    Public Shared Function DownloadFile(
                                     url As String,
                                     outputFile As String,
                                     Optional method As DownloadMethod = DownloadMethod.Auto,
                                     Optional enableFallback As Boolean = True,
                                     Optional useragent As String = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                                     Optional username As String = "",
                                     Optional password As String = "",
                                     Optional headers As Dictionary(Of String, String) = Nothing,
                                     Optional timeoutSeconds As Integer = 60,
                                     Optional proxyUrl As String = "",
                                     Optional allowInsecureSSL As Boolean = False) As DownloadResult
        
        ' Use Download method with specified output file
        Return Download(url, method, enableFallback, useragent, username, password, headers, timeoutSeconds, proxyUrl, outputFile, allowInsecureSSL)
    End Function
    
    ''' <summary>
    ''' Downloads binary data from a URL.
    ''' </summary>
    ''' <param name="url">The URL to download from.</param>
    ''' <param name="method">The primary download method to use.</param>
    ''' <param name="enableFallback">Whether to try alternative methods if the primary method fails.</param>
    ''' <param name="useragent">The User-Agent header value.</param>
    ''' <param name="username">Optional username for authentication.</param>
    ''' <param name="password">Optional password for authentication.</param>
    ''' <param name="headers">Optional additional HTTP headers.</param>
    ''' <param name="timeoutSeconds">Timeout in seconds.</param>
    ''' <param name="proxyUrl">Optional proxy URL.</param>
    ''' <param name="allowInsecureSSL">Whether to allow insecure SSL connections (not recommended for security reasons).</param>
    ''' <returns>The downloaded content as a byte array, or null if download failed.</returns>
    Public Shared Function DownloadData(
                                     url As String,
                                     Optional method As DownloadMethod = DownloadMethod.Auto,
                                     Optional enableFallback As Boolean = True,
                                     Optional useragent As String = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                                     Optional username As String = "",
                                     Optional password As String = "",
                                     Optional headers As Dictionary(Of String, String) = Nothing,
                                     Optional timeoutSeconds As Integer = 60,
                                     Optional proxyUrl As String = "",
                                     Optional allowInsecureSSL As Boolean = False) As Byte()
        
        Try
            ' Create temporary file
            Dim tempFile As String = Path.GetTempFileName()
            
            ' Download to temporary file
            Dim result As DownloadResult = Download(url, method, enableFallback, useragent, username, password, headers, timeoutSeconds, proxyUrl, tempFile, allowInsecureSSL)
            
            ' Check if download was successful
            If result.Success Then
                ' Read file as binary data
                If File.Exists(tempFile) Then
                    Dim data As Byte() = File.ReadAllBytes(tempFile)
                    
                    ' Clean up temporary file
                    Try
                        File.Delete(tempFile)
                    Catch
                        ' Ignore cleanup errors
                    End Try
                    
                    Return data
                End If
            End If
            
            ' Log error details
            Debug.Print("DownloadData failed: " + result.ErrorMessage)
            
            ' Clean up temporary file if download failed
            Try
                If File.Exists(tempFile) Then
                    File.Delete(tempFile)
                End If
            Catch
                ' Ignore cleanup errors
            End Try
            
            Return Nothing
        Catch ex As Exception
            Debug.Print("DownloadData error: " & ex.Message)
            Return Nothing
        End Try
    End Function
    
    ''' <summary>
    ''' Checks if curl is available on the system.
    ''' </summary>
    ''' <returns>True if curl is available, False otherwise.</returns>
    Public Shared Function IsCurlAvailable() As Boolean
        Try
            Dim startInfo As New ProcessStartInfo("curl.exe", "--version")
            startInfo.CreateNoWindow = True
            startInfo.UseShellExecute = False
            startInfo.RedirectStandardOutput = True
            startInfo.RedirectStandardError = True
            
            Using process As Process = Process.Start(startInfo)
                process.WaitForExit(5000)  ' Wait up to 5 seconds
                Return process.ExitCode = 0
            End Using
        Catch
            Return False
        End Try
    End Function
    
    ''' <summary>
    ''' Checks if wget is available on the system.
    ''' </summary>
    ''' <returns>True if wget is available, False otherwise.</returns>
    Public Shared Function IsWgetAvailable() As Boolean
        Try
            Dim startInfo As New ProcessStartInfo("wget.exe", "--version")
            startInfo.CreateNoWindow = True
            startInfo.UseShellExecute = False
            startInfo.RedirectStandardOutput = True
            startInfo.RedirectStandardError = True
            
            Using process As Process = Process.Start(startInfo)
                process.WaitForExit(5000)  ' Wait up to 5 seconds
                Return process.ExitCode = 0
            End Using
        Catch
            Return False
        End Try
    End Function
    
    ''' <summary>
    ''' Gets the best available download method on the current system.
    ''' </summary>
    ''' <returns>The best available download method.</returns>
    Public Shared Function GetBestAvailableMethod() As DownloadMethod
        If IsCurlAvailable() Then
            Return DownloadMethod.Curl
        ElseIf IsWgetAvailable() Then
            Return DownloadMethod.Wget
        Else
            Return DownloadMethod.PowerShell
        End If
    End Function
    
    ''' <summary>
    ''' Creates a URL with properly encoded query parameters.
    ''' </summary>
    ''' <param name="baseUrl">The base URL without query parameters.</param>
    ''' <param name="parameters">Dictionary of parameter names and values.</param>
    ''' <returns>A properly formatted URL with encoded query parameters.</returns>
    Public Shared Function BuildUrl(baseUrl As String, parameters As Dictionary(Of String, String)) As String
        If parameters Is Nothing OrElse parameters.Count = 0 Then
            Return baseUrl
        End If
        
        Dim url As New StringBuilder(baseUrl)
        
        ' Add ? or & depending on whether the base URL already has parameters
        If baseUrl.Contains("?") Then
            If Not baseUrl.EndsWith("?") AndAlso Not baseUrl.EndsWith("&") Then
                url.Append("&")
            End If
        Else
            url.Append("?")
        End If
        
        ' Add encoded parameters
        Dim isFirst As Boolean = True
        For Each param In parameters
            If Not isFirst Then
                url.Append("&")
            End If
            
            url.Append(Uri.EscapeDataString(param.Key))
            url.Append("=")
            url.Append(Uri.EscapeDataString(param.Value))
            
            isFirst = False
        Next
        
        Return url.ToString()
    End Function
    
    ''' <summary>
    ''' Downloads a string with explicit encoding from a URL.
    ''' Use this method when specific text encoding is important.
    ''' </summary>
    ''' <param name="url">The URL to download from.</param>
    ''' <param name="encoding">The encoding to use for the downloaded text.</param>
    ''' <param name="method">The primary download method to use.</param>
    ''' <param name="defaultValue">Default value to return if download fails.</param>
    ''' <param name="allowInsecureSSL">Whether to allow insecure SSL connections (not recommended for security reasons).</param>
    ''' <returns>The downloaded content as a string or the default value if download failed.</returns>
    Public Shared Function DownloadStringWithEncoding(
                                                   url As String,
                                                   encoding As System.Text.Encoding,
                                                   Optional method As DownloadMethod = DownloadMethod.Auto,
                                                   Optional defaultValue As String = "",
                                                   Optional allowInsecureSSL As Boolean = False) As String
        
        ' Create temporary file
        Dim tempFile As String = Path.GetTempFileName()
        
        Try
            ' Download to temporary file
            Dim result As DownloadResult = Download(url, method, True, "Mozilla/5.0", "", "", Nothing, 60, "", tempFile, allowInsecureSSL)
            
            ' Check if download was successful
            If result.Success Then
                ' Read file with specified encoding
                If File.Exists(tempFile) Then
                    Dim content As String = File.ReadAllText(tempFile, encoding)
                    Return content
                End If
            End If
            
            Debug.Print("DownloadStringWithEncoding failed: " & result.ErrorMessage)
            Return defaultValue
        Catch ex As Exception
            Debug.Print("DownloadStringWithEncoding error: " & ex.Message)
            Return defaultValue
        Finally
            ' Clean up temporary file
            Try
                If File.Exists(tempFile) Then
                    File.Delete(tempFile)
                End If
            Catch
                ' Ignore cleanup errors
            End Try
        End Try
    End Function
    
    ''' <summary>
    ''' Verifies if all required external tools are available for each download method.
    ''' </summary>
    ''' <returns>A dictionary with each method and its availability status.</returns>
    Public Shared Function VerifyDependencies() As Dictionary(Of DownloadMethod, Boolean)
        Dim result As New Dictionary(Of DownloadMethod, Boolean)
        
        ' Check curl
        result(DownloadMethod.Curl) = IsCurlAvailable()
        
        ' Check wget
        result(DownloadMethod.Wget) = IsWgetAvailable()
        
        ' Check PowerShell (always available on modern Windows)
        Try
            Dim startInfo As New ProcessStartInfo("powershell.exe", "-Command ""exit 0""")
            startInfo.CreateNoWindow = True
            startInfo.UseShellExecute = False
            
            Using process As Process = Process.Start(startInfo)
                process.WaitForExit(5000)
                result(DownloadMethod.PowerShell) = (process.ExitCode = 0)
            End Using
        Catch
            result(DownloadMethod.PowerShell) = False
        End Try
        
        Return result
    End Function
End Class
