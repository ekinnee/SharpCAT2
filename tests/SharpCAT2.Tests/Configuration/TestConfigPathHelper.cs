using System;
using System.IO;

namespace SharpCAT2.Tests.Configuration;

/// <summary>
/// Helper class for managing test configuration file paths and cleanup.
/// Provides cross-platform compatible paths and automatic cleanup functionality.
/// </summary>
public class TestConfigPathHelper : IDisposable
{
    private readonly string _testDirectory;
    private bool _disposed = false;

    /// <summary>
    /// Initializes a new instance of TestConfigPathHelper.
    /// Creates the test directory if it doesn't exist.
    /// </summary>
    public TestConfigPathHelper()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "config_tests");
        EnsureTestDirectoryExists();
    }

    /// <summary>
    /// Gets the full path for a test configuration file.
    /// </summary>
    /// <param name="fileName">The name of the configuration file</param>
    /// <returns>The full path to the test configuration file</returns>
    public string GetTestConfigPath(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            throw new ArgumentException("File name cannot be null or empty", nameof(fileName));

        return Path.Combine(_testDirectory, fileName);
    }

    /// <summary>
    /// Ensures the test directory exists, creating it if necessary.
    /// </summary>
    public void EnsureTestDirectoryExists()
    {
        if (!Directory.Exists(_testDirectory))
        {
            Directory.CreateDirectory(_testDirectory);
        }
    }

    /// <summary>
    /// Cleans up all files in the test directory.
    /// </summary>
    public void CleanupTestFiles()
    {
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                var files = Directory.GetFiles(_testDirectory);
                foreach (var file in files)
                {
                    File.Delete(file);
                }
            }
            catch (Exception)
            {
                // Ignore cleanup errors - they shouldn't fail tests
            }
        }
    }

    /// <summary>
    /// Disposes the helper and cleans up test files.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            CleanupTestFiles();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}