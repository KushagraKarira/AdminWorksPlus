using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AdminWorks.Models;
using Microsoft.UI.Dispatching;

namespace AdminWorks.Services
{
    public class ExecutionService
    {
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public ObservableCollection<LogEntry> Logs { get; } = new();
        public bool IsExecuting { get; private set; }
        public string CurrentRunningTask { get; private set; } = "Ready";

        public event Action<bool, string>? ExecutionStateChanged;

        public ExecutionService(DispatcherQueue dispatcherQueue)
        {
            _dispatcherQueue = dispatcherQueue;
        }

        public void Log(string message, LogLevel level = LogLevel.Info)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                Logs.Add(new LogEntry(DateTime.Now, level, message));
                // Cap log history to 1500 entries to prevent memory growth
                if (Logs.Count > 1500)
                {
                    Logs.RemoveAt(0);
                }
            });
        }

        public async Task<bool> ExecuteTweakAsync(TweakItem item, bool isEnabling = true)
        {
            if (IsExecuting)
            {
                Log($"Another task is already in progress: {CurrentRunningTask}", LogLevel.Warning);
                return false;
            }

            await _semaphore.WaitAsync();
            try
            {
                IsExecuting = true;
                CurrentRunningTask = item.Title;
                item.IsBusy = true;
                ExecutionStateChanged?.Invoke(true, CurrentRunningTask);

                if (item.IsToggle)
                {
                    if (isEnabling && item.EnableAction != null)
                    {
                        await item.EnableAction(this);
                        item.IsActive = true;
                    }
                    else if (!isEnabling && item.DisableAction != null)
                    {
                        await item.DisableAction(this);
                        item.IsActive = false;
                    }
                }
                else if (item.Action != null)
                {
                    await item.Action(this);
                }

                return true;
            }
            catch (Exception ex)
            {
                Log($"Error executing {item.Title}: {ex.Message}", LogLevel.Error);
                return false;
            }
            finally
            {
                item.IsBusy = false;
                IsExecuting = false;
                CurrentRunningTask = "Ready";
                ExecutionStateChanged?.Invoke(false, CurrentRunningTask);
                _semaphore.Release();
            }
        }

        public async Task RunProcessAsync(string command, string arguments, string taskTitle = "")
        {
            if (!string.IsNullOrEmpty(taskTitle))
            {
                Log($"Starting: {taskTitle}...", LogLevel.Exec);
            }

            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    Log(e.Data, LogLevel.Info);
                }
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    Log(e.Data, LogLevel.Warning);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                Log($"{taskTitle} completed successfully.", LogLevel.Success);
            }
            else
            {
                Log($"{taskTitle} exited with code {process.ExitCode}.", LogLevel.Warning);
            }
        }

        public async Task RunPowerShellScriptAsync(string script, string taskTitle = "")
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"AdminWorks_{Guid.NewGuid():N}.ps1");
            await File.WriteAllTextAsync(tempFile, script);
            try
            {
                await RunProcessAsync("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -File \"{tempFile}\"", taskTitle);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }
    }
}
