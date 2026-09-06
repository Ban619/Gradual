using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace Gradual.Models.IdentifierEn;

/// <summary>
/// C# Integration layer for file identification system
/// Coordinates between C++ fast checker and Python deep analyzer
/// </summary>
public class Integ
{
    private readonly string _pythonScriptPath;
    private readonly string _cppDllPath;
    private bool _useCppOptimization = false;

    public Integ()
    {
        // Get paths relative to application directory
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _pythonScriptPath = Path.Combine(baseDir, "Models", "IdentifierEn", "Tfile.py");
        _cppDllPath = Path.Combine(baseDir, "Models", "IdentifierEn", "T", "internalT.dll");
        
        // Check if C++ DLL is available
        _useCppOptimization = File.Exists(_cppDllPath);
    }

    /// <summary>
    /// Validates if a directory is a legitimate software project
    /// </summary>
    public async Task<ProjectValidationResult> ValidateProjectDirectoryAsync(string directoryPath)
    {
        try
        {
            // Step 1: Fast pre-filter with C++ (if available)
            if (_useCppOptimization)
            {
                bool shouldAnalyze = CppQuickCheck(directoryPath);
                if (!shouldAnalyze)
                {
                    return new ProjectValidationResult
                    {
                        IsValid = false,
                        Confidence = 0,
                        DirectoryPath = directoryPath,
                        RejectionReason = "Fast pre-filter rejected: No code indicators found",
                        ValidationMethod = "C++ Quick Check"
                    };
                }
            }

            // Step 2: Deep analysis with Python
            var pythonResult = await RunPythonAnalysisAsync(directoryPath);
            return pythonResult;
        }
        catch (Exception ex)
        {
            return new ProjectValidationResult
            {
                IsValid = false,
                Confidence = 0,
                DirectoryPath = directoryPath,
                RejectionReason = $"Validation error: {ex.Message}",
                ValidationMethod = "Error"
            };
        }
    }

    /// <summary>
    /// Quick synchronous check using C++ layer
    /// </summary>
    public bool QuickValidate(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return false;

        if (_useCppOptimization)
        {
            return CppQuickCheck(directoryPath);
        }

        // Fallback: basic file check
        return HasCodeFiles(directoryPath);
    }

    /// <summary>
    /// Runs Python analyzer and returns detailed results
    /// </summary>
    private async Task<ProjectValidationResult> RunPythonAnalysisAsync(string directoryPath)
    {
        try
        {
            // Find Python executable
            string pythonExe = FindPythonExecutable();
            if (string.IsNullOrEmpty(pythonExe))
            {
                return CreateFallbackResult(directoryPath, "Python not found - using basic validation");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = $"\"{_pythonScriptPath}\" \"{directoryPath}\" 1",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            // Parse JSON result
            if (!string.IsNullOrWhiteSpace(output))
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var pythonResult = JsonSerializer.Deserialize<PythonAnalysisResult>(output, options);
                return ConvertPythonResult(pythonResult, directoryPath);
            }
            else
            {
                return CreateFallbackResult(directoryPath, $"Python analysis failed: {error}");
            }
        }
        catch (Exception ex)
        {
            return CreateFallbackResult(directoryPath, $"Python execution error: {ex.Message}");
        }
    }

    /// <summary>
    /// C++ quick check via P/Invoke
    /// </summary>
    private bool CppQuickCheck(string directoryPath)
    {
        try
        {
            int result = NativeMethods.ShouldAnalyzeDirectory(directoryPath);
            return result == 1;
        }
        catch
        {
            // If C++ call fails, fall back to basic check
            return HasCodeFiles(directoryPath);
        }
    }

    /// <summary>
    /// Basic fallback validation without Python
    /// </summary>
    private bool HasCodeFiles(string directoryPath)
    {
        try
        {
            var codeExtensions = new[] { ".cs", ".py", ".cpp", ".c", ".h", ".java", ".js", ".ts" };
            var configFiles = new[] { "package.json", "requirements.txt", "*.csproj", "*.sln" };

            // Check for code files
            foreach (var ext in codeExtensions)
            {
                if (Directory.GetFiles(directoryPath, $"*{ext}", SearchOption.TopDirectoryOnly).Length > 0)
                    return true;
            }

            // Check for project marker files
            foreach (var pattern in configFiles)
            {
                if (Directory.GetFiles(directoryPath, pattern, SearchOption.TopDirectoryOnly).Length > 0)
                    return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private ProjectValidationResult CreateFallbackResult(string directoryPath, string message)
    {
        bool hasCode = HasCodeFiles(directoryPath);

        return new ProjectValidationResult
        {
            IsValid = hasCode,
            Confidence = hasCode ? 50 : 0,
            DirectoryPath = directoryPath,
            RejectionReason = hasCode ? null : "No code files detected",
            ValidationMethod = $"Fallback ({message})",
            Statistics = null
        };
    }

    private ProjectValidationResult ConvertPythonResult(PythonAnalysisResult? pythonResult, string directoryPath)
    {
        if (pythonResult == null)
        {
            return CreateFallbackResult(directoryPath, "Invalid Python response");
        }

        return new ProjectValidationResult
        {
            IsValid = pythonResult.Valid,
            Confidence = pythonResult.Confidence,
            DirectoryPath = pythonResult.Directory ?? directoryPath,
            RejectionReason = pythonResult.RejectionReason,
            ValidationMethod = "Python Deep Analysis",
            Classification = pythonResult.Classification,
            Statistics = pythonResult.Statistics,
            ProjectMarkers = pythonResult.ProjectMarkers,
            Samples = pythonResult.Samples
        };
    }

    private string FindPythonExecutable()
    {
        // Try common Python locations
        string[] pythonPaths = {
            "python",
            "python3",
            "py",
            @"C:\Python310\python.exe",
            @"C:\Python39\python.exe",
            @"C:\Program Files\Python310\python.exe",
            Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Programs\Python\Python310\python.exe")
        };

        foreach (var path in pythonPaths)
        {
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                });

                if (process != null)
                {
                    process.WaitForExit(1000);
                    if (process.ExitCode == 0)
                        return path;
                }
            }
            catch
            {
                continue;
            }
        }

        return string.Empty;
    }

    // Native methods for C++ interop
    private static class NativeMethods
    {
        [DllImport("internalT.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int ShouldAnalyzeDirectory([MarshalAs(UnmanagedType.LPStr)] string path);

        [DllImport("internalT.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetQuickScanStats([MarshalAs(UnmanagedType.LPStr)] string path);

        [DllImport("internalT.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void FreeString(IntPtr str);
    }
}

/// <summary>
/// Result of project validation
/// </summary>
public class ProjectValidationResult
{
    public bool IsValid { get; set; }
    public int Confidence { get; set; }
    public string DirectoryPath { get; set; } = string.Empty;
    public string? RejectionReason { get; set; }
    public string ValidationMethod { get; set; } = string.Empty;
    public string? Classification { get; set; }
    public FileStatistics? Statistics { get; set; }
    public List<string>? ProjectMarkers { get; set; }
    public FileSamples? Samples { get; set; }
}

/// <summary>
/// Python analysis result (deserialized from JSON)
/// </summary>
internal class PythonAnalysisResult
{
    [System.Text.Json.Serialization.JsonPropertyName("valid")]
    public bool Valid { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("confidence")]
    public int Confidence { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("directory")]
    public string? Directory { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("statistics")]
    public FileStatistics? Statistics { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("project_markers")]
    public List<string> ProjectMarkers { get; set; } = new();
    
    [System.Text.Json.Serialization.JsonPropertyName("samples")]
    public FileSamples? Samples { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("rejection_reason")]
    public string? RejectionReason { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("classification")]
    public string? Classification { get; set; }
}

public class FileStatistics
{
    [System.Text.Json.Serialization.JsonPropertyName("total_files")]
    public int TotalFiles { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("accepted_code")]
    public int CodeFiles { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("accepted_config")]
    public int ConfigFiles { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("rejected_media")]
    public int MediaFiles { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("unknown_files")]
    public int UnknownFiles { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("programming_percentage")]
    public double ProgrammingPercentage { get; set; }
}

public class FileSamples
{
    [System.Text.Json.Serialization.JsonPropertyName("code")]
    public List<string> Code { get; set; } = new();
    
    [System.Text.Json.Serialization.JsonPropertyName("rejected")]
    public List<string> Rejected { get; set; } = new();
    
    [System.Text.Json.Serialization.JsonPropertyName("unknown")]
    public List<string> Unknown { get; set; } = new();
}
